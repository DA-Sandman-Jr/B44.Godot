# B44.Godot validation module: the proof runner and the validation ledger B44 Godot games share.
# PowerShell 7. A game imports it from its restored B44.Godot package (tools/B44.Godot.Validation.psm1);
# the game keeps its own scope membership, proof scripts, fixtures and expected diagnostics.
#
# Proof runner: every engine launch is bounded by wall-clock time as well as by --quit-after, so a hung
# engine fails its proof instead of hanging the run, and a proof passes only with exit code zero, its
# completion marker, and no engine diagnostic other than the ones it declares.
#
# Validation ledger: one run's steps and its verdict. Each verdict has its own exit code, and only 0 proves
# the selection: 0 PASSED, 1 FAILED (outranks everything), 2 UNAVAILABLE (the selection could not start
# here), 3 INCOMPLETE (nothing failed, but a step the selection requires cannot run on this platform).

Set-StrictMode -Version Latest

# Godot prints engine and script diagnostics with these prefixes at the start of a line.
$script:EngineDiagnosticPattern = '(?m)^\s*(SCRIPT ERROR|USER ERROR|USER WARNING|ERROR|WARNING):[^\r\n]*'
$script:Validation = $null
# The summary a run carries until Complete-Validation replaces it, so one that stops early still says so.
$script:Unfinished = 'The run has not finished: it stopped before Complete-Validation, so this summary lists only the steps it recorded.'

function Get-GodotUserRoot {
    # Where Godot places config/custom_user_dir_name on this operating system.
    if ($IsWindows) { return [Environment]::GetFolderPath('ApplicationData') }
    if ($IsMacOS) { return Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Library/Application Support' }
    if ($env:XDG_DATA_HOME) { return $env:XDG_DATA_HOME }
    return Join-Path ([Environment]::GetFolderPath('UserProfile')) '.local/share'
}

function Write-ProofOverride {
    # Writes the temporary override.cfg that isolates Godot's user directory for one proof.
    # Refuses to replace an override.cfg the developer already has.
    param(
        [Parameter(Mandatory = $true)][string] $Project,
        [Parameter(Mandatory = $true)][string] $UserDirectory,
        [string[]] $Lines = @())
    $path = Join-Path $Project 'override.cfg'
    if (Test-Path -LiteralPath $path) { throw 'Preserve the existing override.cfg before running this proof.' }
    $content = @('[application]', 'config/use_custom_user_dir=true', ('config/custom_user_dir_name="' + $UserDirectory + '"')) + $Lines
    [IO.File]::WriteAllText($path, ($content -join [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
    return $path
}

function Invoke-BoundedProcess {
    # Runs a process with each argument passed separately (no shell quoting) and reads both pipes as it runs.
    # One wall-clock limit bounds the whole run: the process's exit and the end of its output. A process still
    # running at the limit has its whole tree stopped, then gets a short grace to flush what it wrote. A process
    # that exited while something it started still holds its output open is not waited for past the limit:
    # TimedOut is set, TimeoutReason says which happened, and Output keeps everything read before then.
    # Throws when the executable cannot be started.
    param(
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Arguments,
        [Parameter(Mandatory = $true)][int] $TimeoutSeconds,
        [string] $WorkingDirectory = (Get-Location).Path,
        [string] $LogPath)
    $start = [Diagnostics.ProcessStartInfo]::new($FilePath)
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $deadline = [TimeSpan]::FromSeconds($TimeoutSeconds)
        # Each pipe is read in chunks on this thread, so the reads can be abandoned at the limit without losing
        # what already arrived. A pipe that has ended waits on a task that never completes.
        $streams = @($process.StandardOutput, $process.StandardError)
        $text = @([Text.StringBuilder]::new(), [Text.StringBuilder]::new())
        $buffers = @([char[]]::new(8192), [char[]]::new(8192))
        $ended = [Threading.Tasks.TaskCompletionSource[int]]::new().Task
        $reads = [Threading.Tasks.Task[]]@($streams[0].ReadAsync($buffers[0], 0, 8192), $streams[1].ReadAsync($buffers[1], 0, 8192))
        $open = 2
        $reason = $null
        $graceStarted = $false
        while ($open -gt 0) {
            $remaining = $deadline - $watch.Elapsed
            if ($remaining -le [TimeSpan]::Zero) {
                if ($graceStarted) { break }
                if (-not $process.HasExited) {
                    $reason = 'it did not exit within its time limit and was stopped'
                    try { $process.Kill($true) } catch [InvalidOperationException] { }
                    # The stopped tree closes the pipes; collect what it had already written.
                    $graceStarted = $true
                    $deadline = $watch.Elapsed + [TimeSpan]::FromSeconds(5)
                    continue
                }
                $reason = 'it exited, but a process it started still held its output open at its time limit'
                break
            }
            $index = [Threading.Tasks.Task]::WaitAny($reads, [int][Math]::Min($remaining.TotalMilliseconds, 250))
            if ($index -lt 0) { continue }
            $count = 0
            try { $count = $reads[$index].GetAwaiter().GetResult() } catch { $count = 0 }
            if ($count -eq 0) { $open--; $reads[$index] = $ended }
            else {
                [void]$text[$index].Append($buffers[$index], 0, $count)
                $reads[$index] = $streams[$index].ReadAsync($buffers[$index], 0, $buffers[$index].Length)
            }
        }
        if (-not $reason) {
            # Both pipes closed: the process has exited, or closed them and kept running.
            $remainingMilliseconds = [int][Math]::Max(0, ($deadline - $watch.Elapsed).TotalMilliseconds)
            if (-not $process.WaitForExit($remainingMilliseconds)) {
                $reason = 'it did not exit within its time limit and was stopped'
                try { $process.Kill($true) } catch [InvalidOperationException] { }
            }
        }
        $exited = $process.WaitForExit(5000)
        $stdout = $text[0].ToString()
        $stderr = $text[1].ToString()
        $output = if ($stderr) { $stdout.TrimEnd() + [Environment]::NewLine + $stderr } else { $stdout }
        if ($LogPath) {
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($LogPath))) -Force | Out-Null
            $header = "> $FilePath $($Arguments -join ' ')" + [Environment]::NewLine
            $footer = if ($reason) { [Environment]::NewLine + "[validation] stopped waiting: $reason" } else { '' }
            [IO.File]::WriteAllText($LogPath, $header + $output + $footer, [Text.UTF8Encoding]::new($false))
        }
        $stoppedRunning = $reason -and $reason.StartsWith('it did not exit', [StringComparison]::Ordinal)
        return [pscustomobject]@{
            ExitCode = if ($exited -and -not $stoppedRunning) { $process.ExitCode } else { $null }
            TimedOut = [bool]$reason
            TimeoutReason = $reason
            Output = $output
        }
    }
    # Disposing closes this end of the pipes; a read still pending on a pipe something else holds is abandoned.
    finally { $process.Dispose() }
}

function Invoke-GodotImport {
    # Imports resources once. Import/editor configuration messages are not part of a proof verdict;
    # only the exit code and the time limit decide.
    param(
        [Parameter(Mandatory = $true)][string] $Godot,
        [Parameter(Mandatory = $true)][string] $Project,
        [int] $TimeoutSeconds = 600,
        [string] $LogPath)
    $result = Invoke-BoundedProcess -FilePath $Godot -Arguments @('--headless', '--editor', '--import', '--path', $Project, '--quit') `
        -TimeoutSeconds $TimeoutSeconds -WorkingDirectory $Project -LogPath $LogPath
    if ($result.TimedOut) { throw "Godot import did not finish within $TimeoutSeconds seconds: $($result.TimeoutReason)." }
    if ($result.ExitCode -ne 0) { throw "Godot import failed with exit code $($result.ExitCode)." }
    return $result
}

function Get-EngineDiagnostics {
    # Every engine or script diagnostic line in Output, trimmed.
    param([AllowEmptyString()][AllowNull()][string] $Output)
    return @([regex]::Matches([string]$Output, $script:EngineDiagnosticPattern) | ForEach-Object { $_.Value.Trim() })
}

function Assert-GodotProof {
    # A proof passes only with exit code zero inside its time limit, its completion marker, and no
    # engine diagnostic other than the ones it deliberately provokes. Each expected diagnostic is
    # @{ Pattern = '<regex>'; Count = <exact number of matching lines> }.
    param(
        [Parameter(Mandatory = $true)] $Result,
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $Marker,
        [object[]] $ExpectedDiagnostics = @(),
        [string] $Inspect)
    $ExpectedDiagnostics = @($ExpectedDiagnostics | Where-Object { $null -ne $_ })
    $output = [string]$Result.Output
    $problems = [Collections.Generic.List[string]]::new()
    if ($Result.TimedOut) {
        # Results built by hand may not say why; the runner's own results always do.
        $why = $Result.PSObject.Properties['TimeoutReason']
        $problems.Add($(if ($why -and $why.Value) { 'the engine ' + ([string]$why.Value).Substring(3) } else { 'the engine did not exit within its time limit and was stopped' }))
    }
    elseif ($Result.ExitCode -ne 0) { $problems.Add("the engine exited with code $($Result.ExitCode)") }
    if (-not $output.Contains($Marker)) { $problems.Add("the completion marker '$Marker' is missing") }
    $diagnostics = Get-EngineDiagnostics $output
    foreach ($expected in $ExpectedDiagnostics) {
        $seen = @($diagnostics | Where-Object { $_ -match $expected.Pattern }).Count
        if ($seen -ne $expected.Count) { $problems.Add("expected $($expected.Count) diagnostic(s) matching '$($expected.Pattern)', saw $seen") }
    }
    foreach ($line in $diagnostics) {
        $allowed = @($ExpectedDiagnostics | Where-Object { $line -match $_.Pattern }).Count -gt 0
        if (-not $allowed) { $problems.Add("unexpected engine diagnostic: $line") }
    }
    if ($problems.Count -gt 0) {
        $suffix = if ($Inspect) { " Inspect $Inspect." } else { '' }
        throw "$Name failed: $($problems -join '; ').$suffix"
    }
}

function Remove-ProofDirectory {
    # Deletes one run's own directory, and only when it sits directly beneath the expected parent
    # with the run's unique leaf name.
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Parent,
        [Parameter(Mandatory = $true)][string] $Leaf)
    $resolved = [IO.Path]::GetFullPath($Path)
    $prefix = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (-not $resolved.StartsWith($prefix, $comparison) -or [IO.Path]::GetFileName($resolved) -ne $Leaf -or
        [IO.Path]::GetDirectoryName($resolved) + [IO.Path]::DirectorySeparatorChar -ne $prefix) {
        throw "Refusing to delete $resolved outside $prefix."
    }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}

function Test-GodotEngineVersion {
    # Returns nothing when Godot is the release the host project's Godot.NET.Sdk expects, else the reason.
    # Godot tags a .0 release as "4.7", while NuGet versions always carry three parts.
    param(
        [Parameter(Mandatory = $true)][string] $Godot,
        [Parameter(Mandatory = $true)][string] $HostProject)
    $sdk = [regex]::Match([IO.File]::ReadAllText($HostProject), 'Godot\.NET\.Sdk/(\d+)\.(\d+)\.(\d+)')
    if (-not $sdk.Success) { return "$HostProject names no Godot.NET.Sdk version." }
    $expected = "$($sdk.Groups[1].Value).$($sdk.Groups[2].Value)"
    if ($sdk.Groups[3].Value -ne '0') { $expected += ".$($sdk.Groups[3].Value)" }
    try { $version = (Invoke-BoundedProcess -FilePath $Godot -Arguments @('--version', '--headless') -TimeoutSeconds 60).Output }
    catch { return "Godot.NET.Sdk expects Godot $expected stable .NET, and $Godot could not be started: $($_.Exception.Message)" }
    if ($version -notmatch "(?m)^$([regex]::Escape($expected))\.stable\.mono\.") {
        return "Godot.NET.Sdk expects Godot $expected stable .NET; $Godot reports: $(([string]$version).Trim())"
    }
}

function Start-Validation {
    # Begins one run's ledger. The caller empties Evidence before its own bootstrap, so that a run which
    # stops before this module loads still leaves a current summary rather than an earlier run's. From here
    # on the summary is current at every step: it reads FAILED (exit 1) until Complete-Validation decides,
    # so a run that throws or is stopped part way never leaves a passing or stale summary behind.
    param(
        [Parameter(Mandatory = $true)][string] $Project,
        [Parameter(Mandatory = $true)][string] $Evidence,
        [Parameter(Mandatory = $true)][string] $Title,
        [Parameter(Mandatory = $true)][string] $Selection)
    $projectPath = [IO.Path]::GetFullPath($Project).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $evidencePath = [IO.Path]::GetFullPath($Evidence)
    if (-not $evidencePath.StartsWith($projectPath, [StringComparison]::Ordinal)) { throw 'Evidence must stay inside the project.' }
    if (-not (Test-Path -LiteralPath $evidencePath -PathType Container)) { throw "Create the evidence directory $evidencePath first." }
    $script:Validation = @{
        Project = $projectPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
        Evidence = $evidencePath
        Title = $Title
        Selection = $Selection
        Results = [Collections.Generic.List[object]]::new()
        BlockedBy = $null
    }
    Write-ValidationSummary 'FAILED' 1 $script:Unfinished
}

function Get-ValidationState {
    if ($null -eq $script:Validation) { throw 'Call Start-Validation first.' }
    return $script:Validation
}

function Add-ValidationResult {
    # passed and failed ran; blocked waited on a failed gate; unavailable is required by the selection but
    # cannot run on this platform; not requested is an opt-in step nobody asked for.
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][ValidateSet('passed', 'failed', 'blocked', 'unavailable', 'not requested')][string] $Status,
        [double] $Seconds = 0,
        [string] $Note = '')
    (Get-ValidationState).Results.Add([pscustomobject]@{ Step = $Name; Status = $Status; Seconds = [math]::Round($Seconds, 1); Note = $Note })
    Write-ValidationSummary 'FAILED' 1 $script:Unfinished
    $marker = switch ($Status) { 'passed' { 'PASS' } 'failed' { 'FAIL' } 'blocked' { 'BLOCKED' } 'unavailable' { 'NOT RUN' } default { 'SKIP' } }
    Write-Host ("[{0}] {1}{2}" -f $marker, $Name, $(if ($Note) { " - $Note" } else { '' }))
}

function Invoke-ValidationStep {
    # Runs one command into its own log. A Gate step blocks every later step after it fails; independent
    # steps keep running after a failure so one run reports them all. Check reads the output of a run
    # that succeeded or failed and returns a note, or throws to fail a run that exited cleanly.
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $FilePath,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Arguments,
        [int] $TimeoutSeconds = 900,
        [switch] $Gate,
        [ValidateSet('', 'Windows', 'Linux', 'MacOS')][string] $NeedsPlatform = '',
        # The command must fail; Check then confirms it failed for the expected reason.
        [switch] $ExpectFailure,
        [scriptblock] $Check)
    $state = Get-ValidationState
    if ($state.BlockedBy) { Add-ValidationResult $Name 'blocked' 0 "blocked by $($state.BlockedBy)"; return }
    $platformOk = switch ($NeedsPlatform) { 'Windows' { $IsWindows } 'Linux' { $IsLinux } 'MacOS' { $IsMacOS } default { $true } }
    if (-not $platformOk) { Add-ValidationResult $Name 'unavailable' 0 "required here; runs only on $NeedsPlatform"; return }
    $log = Join-Path $state.Evidence "$Name.log"
    $override = Join-Path $state.Project 'override.cfg'
    $overrideBefore = Test-Path -LiteralPath $override
    $watch = [Diagnostics.Stopwatch]::StartNew()
    try {
        $result = Invoke-BoundedProcess -FilePath $FilePath -Arguments $Arguments -TimeoutSeconds $TimeoutSeconds `
            -WorkingDirectory $state.Project -LogPath $log
    }
    catch {
        # A command that cannot start is a failed step, never an end to the run.
        $cause = $_.Exception
        while ($cause.InnerException) { $cause = $cause.InnerException }
        $message = "could not start ${FilePath}: $($cause.Message)"
        [IO.File]::WriteAllText($log, "> $FilePath $($Arguments -join ' ')" + [Environment]::NewLine + $message + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
        Add-ValidationResult $Name 'failed' $watch.Elapsed.TotalSeconds "$message; see $([IO.Path]::GetRelativePath($state.Project, $log))"
        if ($Gate) { $state.BlockedBy = $Name }
        return
    }
    $note = ''
    $failure = $null
    if ($result.TimedOut) { $failure = "stopped after $TimeoutSeconds s: $($result.TimeoutReason)" }
    else {
        if ($ExpectFailure -and $result.ExitCode -eq 0) { $failure = 'the command succeeded, so the guard did not engage' }
        elseif (-not $ExpectFailure -and $result.ExitCode -ne 0) { $failure = "exit code $($result.ExitCode)" }
        if ($Check) {
            try { $note = [string](& $Check $result.Output) } catch { if (-not $failure) { $failure = $_.Exception.Message } }
        }
    }
    # A command stopped from outside cannot run its own cleanup; never leave its override behind.
    if (-not $overrideBefore -and (Test-Path -LiteralPath $override)) {
        Remove-Item -LiteralPath $override
        $note = (@($note, 'removed the override.cfg it left behind') | Where-Object { $_ }) -join '; '
    }
    if ($failure) {
        $counted = if ($note) { " ($note)" } else { '' }
        Add-ValidationResult $Name 'failed' $watch.Elapsed.TotalSeconds "$failure$counted; see $([IO.Path]::GetRelativePath($state.Project, $log))"
        $tail = @(([string]$result.Output) -split '\r?\n' | Where-Object { $_.Trim() }) | Select-Object -Last 15
        Write-Host ($tail -join [Environment]::NewLine)
        if ($Gate) { $state.BlockedBy = $Name }
    }
    else { Add-ValidationResult $Name 'passed' $watch.Elapsed.TotalSeconds $note }
}

function Invoke-ValidationProof {
    # Runs one proof script in its own PowerShell 7 process, so a failure or stop cannot leak environment
    # changes into the run, passing -GodotPath and the script's own arguments.
    param(
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][string] $Script,
        [Parameter(Mandatory = $true)][string] $Godot,
        [string[]] $Arguments = @(),
        [int] $TimeoutSeconds = 600,
        [ValidateSet('', 'Windows', 'Linux', 'MacOS')][string] $NeedsPlatform = '')
    $pwsh = (Get-Command -Name 'pwsh' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (-not $pwsh) { $pwsh = [Environment]::ProcessPath }
    $all = @('-NoProfile', '-NonInteractive', '-File', $Script, '-GodotPath', $Godot) + $Arguments
    Invoke-ValidationStep -Name $Name -FilePath $pwsh -Arguments $all -TimeoutSeconds $TimeoutSeconds -NeedsPlatform $NeedsPlatform
}

function Write-ValidationSummary {
    param([string] $Verdict, [int] $ExitCode, [string] $Reason)
    $state = Get-ValidationState
    $platform = [Runtime.InteropServices.RuntimeInformation]::OSDescription
    $lines = @("# $($state.Title) validation: $Verdict (exit $ExitCode)", '', "Selection: $($state.Selection); platform: $platform", '')
    if ($Reason) { $lines += @($Reason, '') }
    $lines += @('| Step | Result | Seconds | Note |', '| --- | --- | ---: | --- |')
    $lines += @($state.Results | ForEach-Object { "| $($_.Step) | $($_.Status) | $($_.Seconds) | $($_.Note) |" })
    [IO.File]::WriteAllText((Join-Path $state.Evidence 'summary.md'), ($lines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
    $json = [ordered]@{ selection = $state.Selection; platform = $platform; verdict = $Verdict; exitCode = $ExitCode; reason = $Reason; steps = @($state.Results) }
    [IO.File]::WriteAllText((Join-Path $state.Evidence 'summary.json'), ($json | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
}

function Complete-Validation {
    # Writes summary.md and summary.json and returns the run's exit code; the caller exits with it.
    # -Unavailable ends a run that could not go on here (no matching Godot, no display, an override.cfg in
    # the way) with that reason. A step that already failed still makes the run FAILED: a failure outranks
    # everything, and the reason keeps why the run then stopped.
    param([string] $Unavailable)
    $state = Get-ValidationState
    $failed = @($state.Results | Where-Object Status -EQ 'failed')
    $missing = @($state.Results | Where-Object Status -EQ 'unavailable')
    $reason = $null
    if ($failed.Count -gt 0) {
        $verdict = 'FAILED'; $exitCode = 1
        if ($Unavailable) { $reason = "$(@($failed | ForEach-Object Step) -join ', ') failed, and then the run could not go on: $Unavailable" }
    }
    elseif ($Unavailable) { $verdict = 'UNAVAILABLE'; $exitCode = 2; $reason = $Unavailable }
    elseif ($missing.Count -gt 0) {
        $verdict = 'INCOMPLETE'; $exitCode = 3
        $reason = "The $($state.Selection) is unproven: $(@($missing | ForEach-Object Step) -join ', ') cannot run on this platform. Run it where they can."
    }
    else { $verdict = 'PASSED'; $exitCode = 0 }
    Write-ValidationSummary $verdict $exitCode $reason
    $counts = @($state.Results | Group-Object Status | ForEach-Object { "$($_.Count) $($_.Name)" }) -join ', '
    Write-Host ''
    Write-Host "Validation $verdict, exit $exitCode ($($state.Results.Count) steps: $counts). Evidence: $([IO.Path]::GetRelativePath($state.Project, $state.Evidence))"
    if ($reason) { Write-Host $reason }
    return $exitCode
}

Export-ModuleMember -Function @(
    'Get-GodotUserRoot', 'Write-ProofOverride', 'Invoke-BoundedProcess', 'Invoke-GodotImport', 'Get-EngineDiagnostics',
    'Assert-GodotProof', 'Remove-ProofDirectory', 'Test-GodotEngineVersion', 'Start-Validation', 'Add-ValidationResult',
    'Invoke-ValidationStep', 'Invoke-ValidationProof', 'Complete-Validation')
