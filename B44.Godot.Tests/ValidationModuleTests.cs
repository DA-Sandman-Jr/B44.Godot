using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace B44.Godot.Tests;

/// <summary>
/// The validation module games import from tools/B44.Godot.Validation.psm1. It is PowerShell, so each test
/// runs a short script in PowerShell 7 (pwsh, required on the machine), with throwaway pwsh processes
/// standing in for the engine. No Godot binary is involved.
/// </summary>
public sealed class ValidationModuleTests : IDisposable
{
    private static readonly string Module = Path.Combine(AppContext.BaseDirectory, "B44.Godot.Validation.psm1");
    private readonly string _work = Directory.CreateTempSubdirectory("b44-validation-tests-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    [Fact]
    public void Module_ExportsTheContract()
    {
        string output = Pass("(Get-Command -Module B44.Godot.Validation).Name | Sort-Object");

        string[] expected =
        [
            "Add-ValidationResult", "Assert-GodotProof", "Complete-Validation", "Get-EngineDiagnostics", "Get-GodotUserRoot",
            "Invoke-BoundedProcess", "Invoke-GodotImport", "Invoke-ValidationProof", "Invoke-ValidationStep",
            "Remove-ProofDirectory", "Start-Validation", "Test-GodotEngineVersion", "Write-ProofOverride",
        ];
        Assert.Equal(expected, Lines(output));
    }

    [Fact]
    public void BoundedProcess_ExitsInTime_ReturnsItsExitCodeAndOutput()
    {
        string output = Pass("""
            $r = Invoke-BoundedProcess -FilePath $pwsh -Arguments @('-NoProfile', '-Command', 'Write-Output fine; exit 3') -TimeoutSeconds 60
            "exit=$($r.ExitCode) timedout=$($r.TimedOut) output=$($r.Output.Trim())"
            """);

        Assert.Contains("exit=3 timedout=False output=fine", output, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedProcess_OutlivesItsLimit_IsStoppedAndReportsTheTimeout()
    {
        string output = Pass("""
            $watch = [Diagnostics.Stopwatch]::StartNew()
            $r = Invoke-BoundedProcess -FilePath $pwsh -Arguments @('-NoProfile', '-Command', 'Start-Sleep -Seconds 120') -TimeoutSeconds 2
            "exit=[$($r.ExitCode)] timedout=$($r.TimedOut) stopped=$($watch.Elapsed.TotalSeconds -lt 60)"
            """);

        Assert.Contains("exit=[] timedout=True stopped=True", output, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedProcess_PassesEachArgumentWithoutShellQuoting()
    {
        // -File hands its arguments to the script verbatim, so the echo shows exactly what arrived.
        File.WriteAllText(Path.Combine(_work, "echo.ps1"), "param([string] $Value) \"[$Value]\"");
        string output = Pass("""
            $r = Invoke-BoundedProcess -FilePath $pwsh -Arguments @('-NoProfile', '-File', (Join-Path $PWD.Path 'echo.ps1'), 'two "quoted" words; $(exit 9)') -TimeoutSeconds 60
            "argument=$($r.Output.Trim()) exit=$($r.ExitCode)"
            """);

        Assert.Contains("argument=[two \"quoted\" words; $(exit 9)] exit=0", output, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedProcess_ParentExitsWhileItsChildHoldsTheOutput_StopsWaitingAtTheLimitAndKeepsTheOutput()
    {
        // The parent starts a child that inherits its redirected output, prints, and exits. The child keeps the
        // pipes open for minutes, so waiting for the end of the output would outlive the limit by far.
        File.WriteAllText(Path.Combine(_work, "parent.ps1"), """
            param([string] $Child, [string] $PidFile)
            $start = [Diagnostics.ProcessStartInfo]::new($Child)
            foreach ($argument in @('-NoProfile', '-Command', 'Start-Sleep -Seconds 300')) { $start.ArgumentList.Add($argument) }
            $start.UseShellExecute = $false
            $child = [Diagnostics.Process]::Start($start)
            Set-Content -LiteralPath $PidFile -Value $child.Id
            'parent done'
            """);
        string output = Pass("""
            $pidFile = Join-Path $PWD.Path 'child.pid'
            $watch = [Diagnostics.Stopwatch]::StartNew()
            try {
                $r = Invoke-BoundedProcess -FilePath $pwsh -Arguments @('-NoProfile', '-File', (Join-Path $PWD.Path 'parent.ps1'), '-Child', $pwsh, '-PidFile', $pidFile) -TimeoutSeconds 20
            }
            finally {
                $seconds = $watch.Elapsed.TotalSeconds
                # The whole tree: a pwsh installed as a .NET tool runs PowerShell in a child process.
                if (Test-Path -LiteralPath $pidFile) { try { [Diagnostics.Process]::GetProcessById([int](Get-Content -LiteralPath $pidFile)).Kill($true) } catch { } }
            }
            "exit=$($r.ExitCode) timedout=$($r.TimedOut) bounded=$($seconds -lt 90) output=$($r.Output.Trim())"
            "reason=$($r.TimeoutReason)"
            """);

        Assert.Contains("exit=0 timedout=True bounded=True output=parent done", output, StringComparison.Ordinal);
        Assert.Contains("reason=it exited, but a process it started still held its output open at its time limit", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Proof_CleanExitWithItsMarker_Passes()
    {
        string output = Pass(Proof("PROOF_PASS details", exitCode: 0) + "\n'verdict=passed'");

        Assert.Contains("verdict=passed", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PROOF_PASS", 3, "the engine exited with code 3")]
    [InlineData("nothing", 0, "the completion marker 'PROOF_PASS' is missing")]
    [InlineData("PROOF_PASS\nWARNING: a leak at exit", 0, "unexpected engine diagnostic: WARNING: a leak at exit")]
    [InlineData("PROOF_PASS\nERROR: broken", 0, "unexpected engine diagnostic: ERROR: broken")]
    [InlineData("PROOF_PASS\nSCRIPT ERROR: thrown", 0, "unexpected engine diagnostic: SCRIPT ERROR: thrown")]
    [InlineData("PROOF_PASS\nUSER WARNING: pushed", 0, "unexpected engine diagnostic: USER WARNING: pushed")]
    public void Proof_WithAProblem_FailsAndNamesIt(string output, int exitCode, string problem)
    {
        string message = Fail(Proof(output, exitCode));

        Assert.Contains("Sample proof failed: ", message, StringComparison.Ordinal);
        Assert.Contains(problem, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Proof_StoppedAtItsLimit_FailsAsATimeout()
    {
        string message = Fail("""
            $r = [pscustomobject]@{ ExitCode = $null; TimedOut = $true; Output = 'PROOF_PASS' }
            Assert-GodotProof -Result $r -Name 'Sample proof' -Marker 'PROOF_PASS'
            """);

        Assert.Contains("the engine did not exit within its time limit and was stopped", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, true, "")]
    [InlineData(1, false, "expected 2 diagnostic(s) matching '^ERROR: decoder$', saw 1")]
    [InlineData(3, false, "expected 2 diagnostic(s) matching '^ERROR: decoder$', saw 3")]
    public void Proof_DeclaredDiagnostic_MustAppearExactlyItsCount(int seen, bool passes, string problem)
    {
        string lines = string.Join("\n", Enumerable.Repeat("ERROR: decoder", seen));
        string script = Proof("PROOF_PASS\n" + lines, 0, "@(@{ Pattern = '^ERROR: decoder$'; Count = 2 })");

        if (passes)
        {
            Pass(script);
        }
        else
        {
            Assert.Contains(problem, Fail(script), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Proof_DeclaredDiagnostic_DoesNotExcuseAnyOther()
    {
        string message = Fail(Proof("PROOF_PASS\nERROR: decoder\nWARNING: something else", 0, "@(@{ Pattern = '^ERROR: decoder$'; Count = 1 })"));

        Assert.Contains("unexpected engine diagnostic: WARNING: something else", message, StringComparison.Ordinal);
        Assert.DoesNotContain("unexpected engine diagnostic: ERROR: decoder", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Override_IsolatesTheUserDirectory()
    {
        Pass("Write-ProofOverride -Project $PWD.Path -UserDirectory 'game-proof/run-1' -Lines @('[autoload]', 'Probe=\"*res://p.cs\"') | Out-Null");

        Assert.Equal(
            ["[application]", "config/use_custom_user_dir=true", "config/custom_user_dir_name=\"game-proof/run-1\"", "[autoload]", "Probe=\"*res://p.cs\""],
            File.ReadAllLines(Path.Combine(_work, "override.cfg")));
    }

    [Fact]
    public void Override_ExistingFile_IsPreserved()
    {
        string path = Path.Combine(_work, "override.cfg");
        File.WriteAllText(path, "developer settings");

        string message = Fail("Write-ProofOverride -Project $PWD.Path -UserDirectory 'game-proof/run-1' | Out-Null");

        Assert.Contains("Preserve the existing override.cfg", message, StringComparison.Ordinal);
        Assert.Equal("developer settings", File.ReadAllText(path));
    }

    [Fact]
    public void ProofDirectory_ItsOwnLeafBeneathTheParent_IsRemoved()
    {
        string run = Directory.CreateDirectory(Path.Combine(_work, "proofs", "run-1", "nested")).Parent!.FullName;

        Pass("Remove-ProofDirectory -Path (Join-Path $PWD.Path 'proofs/run-1') -Parent (Join-Path $PWD.Path 'proofs') -Leaf 'run-1'");

        Assert.False(Directory.Exists(run));
    }

    [Theory]
    [InlineData("other/run-1", "proofs", "run-1")]
    [InlineData("proofs/run-2", "proofs", "run-1")]
    [InlineData("proofs/deeper/run-1", "proofs", "run-1")]
    [InlineData("proofs/../outside/run-1", "proofs", "run-1")]
    public void ProofDirectory_AnythingElse_IsRefusedAndKept(string path, string parent, string leaf)
    {
        string target = Directory.CreateDirectory(Path.GetFullPath(Path.Combine(_work, path))).FullName;

        string message = Fail($"Remove-ProofDirectory -Path (Join-Path $PWD.Path '{path}') -Parent (Join-Path $PWD.Path '{parent}') -Leaf '{leaf}'");

        Assert.Contains("Refusing to delete", message, StringComparison.Ordinal);
        Assert.True(Directory.Exists(target));
    }

    [Theory]
    [InlineData("4.7.stable.mono.official.5b4e0cb0f", "Godot.NET.Sdk/4.7.0", true)]
    [InlineData("4.7.1.stable.mono.official.abc", "Godot.NET.Sdk/4.7.1", true)]
    [InlineData("4.6.stable.mono.official.abc", "Godot.NET.Sdk/4.7.0", false)]
    [InlineData("4.7.stable.official.abc", "Godot.NET.Sdk/4.7.0", false)]
    public void EngineVersion_MustMatchTheHostSdk(string reported, string sdk, bool matches)
    {
        File.WriteAllText(Path.Combine(_work, "Host.csproj"), $"<Project Sdk=\"{sdk}\"></Project>");
        string godot = FakeExecutable("godot", reported);

        string output = Pass($"$reason = Test-GodotEngineVersion -Godot '{godot}' -HostProject (Join-Path $PWD.Path 'Host.csproj')\n\"reason=[$reason]\"");

        Assert.Equal(matches, output.Contains("reason=[]", StringComparison.Ordinal));
    }

    [Fact]
    public void EngineVersion_MissingExecutable_IsAReasonNotACrash()
    {
        File.WriteAllText(Path.Combine(_work, "Host.csproj"), "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"></Project>");

        string output = Pass("$reason = Test-GodotEngineVersion -Godot (Join-Path $PWD.Path 'no-godot') -HostProject (Join-Path $PWD.Path 'Host.csproj')\n\"reason=[$reason]\"");

        Assert.Contains("expects Godot 4.7 stable .NET", output, StringComparison.Ordinal);
        Assert.Contains("could not be started", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_EveryStepPassed_IsPassedWithExitZero()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'build' $pwsh @('-NoProfile', '-Command', 'exit 0') -Gate
            Invoke-ValidationStep 'test' $pwsh @('-NoProfile', '-Command', 'Write-Output "total: 4"') -Check { param($o) 'counted ' + ([regex]::Match($o, '\d+').Value) }
            """, out int exitCode);

        Assert.Equal(0, exitCode);
        Assert.Equal("PASSED", summary.GetProperty("verdict").GetString());
        Assert.Equal("counted 4", Step(summary, "test").GetProperty("Note").GetString());
        Assert.True(File.Exists(Path.Combine(_work, "game", "artifacts", "validation", "test.log")));
    }

    [Fact]
    public void Ledger_AFailedGate_BlocksLaterStepsAndFails()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'restore' $pwsh @('-NoProfile', '-Command', 'exit 2') -Gate
            Invoke-ValidationStep 'build' $pwsh @('-NoProfile', '-Command', 'exit 0')
            """, out int exitCode);

        Assert.Equal(1, exitCode);
        Assert.Equal("FAILED", summary.GetProperty("verdict").GetString());
        Assert.Equal("failed", Step(summary, "restore").GetProperty("Status").GetString());
        Assert.Equal("blocked", Step(summary, "build").GetProperty("Status").GetString());
    }

    [Fact]
    public void Ledger_ARequiredStepThisPlatformCannotRun_IsIncomplete()
    {
        JsonElement summary = Validate($"""
            Invoke-ValidationStep 'proof' $pwsh @('-NoProfile', '-Command', 'exit 0')
            Invoke-ValidationStep 'package' $pwsh @('-NoProfile', '-Command', 'exit 0') -NeedsPlatform '{OtherPlatform()}'
            """, out int exitCode);

        Assert.Equal(3, exitCode);
        Assert.Equal("INCOMPLETE", summary.GetProperty("verdict").GetString());
        Assert.Equal("unavailable", Step(summary, "package").GetProperty("Status").GetString());
        Assert.Contains("package", summary.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_AFailure_OutranksAnUnavailableStep()
    {
        JsonElement summary = Validate($"""
            Invoke-ValidationStep 'proof' $pwsh @('-NoProfile', '-Command', 'exit 1')
            Invoke-ValidationStep 'package' $pwsh @('-NoProfile', '-Command', 'exit 0') -NeedsPlatform '{OtherPlatform()}'
            """, out int exitCode);

        Assert.Equal(1, exitCode);
        Assert.Equal("FAILED", summary.GetProperty("verdict").GetString());
    }

    [Fact]
    public void Ledger_AFailedStepThenUnavailable_StaysFailedAndKeepsWhyItStopped()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'restore' $pwsh @('-NoProfile', '-Command', 'exit 1')
            """, out int exitCode, unavailable: "no matching Godot");

        Assert.Equal(1, exitCode);
        Assert.Equal("FAILED", summary.GetProperty("verdict").GetString());
        Assert.Equal("restore failed, and then the run could not go on: no matching Godot", summary.GetProperty("reason").GetString());
    }

    [Fact]
    public void Ledger_AStepWhoseExecutableIsMissing_FailsBlocksAndTheRunStillCompletes()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'restore' (Join-Path $game 'no-such-tool') @('restore') -Gate
            Invoke-ValidationStep 'build' $pwsh @('-NoProfile', '-Command', 'exit 0')
            """, out int exitCode);

        Assert.Equal(1, exitCode);
        Assert.Equal("FAILED", summary.GetProperty("verdict").GetString());
        Assert.Equal("failed", Step(summary, "restore").GetProperty("Status").GetString());
        Assert.StartsWith("could not start ", Step(summary, "restore").GetProperty("Note").GetString(), StringComparison.Ordinal);
        Assert.Equal("blocked", Step(summary, "build").GetProperty("Status").GetString());
        Assert.Contains("could not start", File.ReadAllText(Path.Combine(_work, "game", "artifacts", "validation", "restore.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_ARunThatStopsBeforeCompleting_LeavesACurrentFailingSummary()
    {
        string evidence = Path.Combine(_work, "game", "artifacts", "validation");
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence, "summary.json"), "{ \"verdict\": \"PASSED\", \"exitCode\": 0 }");

        string message = Fail("""
            $game = Join-Path $PWD.Path 'game'
            Start-Validation -Project $game -Evidence (Join-Path $game 'artifacts/validation') -Title 'Game' -Selection 'scope headless'
            Invoke-ValidationStep 'build' $pwsh @('-NoProfile', '-Command', 'exit 0')
            throw 'the validator broke between steps'
            """);

        Assert.Contains("the validator broke between steps", message, StringComparison.Ordinal);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "summary.json")));
        JsonElement summary = document.RootElement;
        Assert.Equal("FAILED", summary.GetProperty("verdict").GetString());
        Assert.Equal(1, summary.GetProperty("exitCode").GetInt32());
        Assert.Contains("has not finished", summary.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("passed", Step(summary, "build").GetProperty("Status").GetString());
    }

    [Fact]
    public void Ledger_ASelectionThatCannotStart_IsUnavailable()
    {
        JsonElement summary = Validate(string.Empty, out int exitCode, unavailable: "no display");

        Assert.Equal(2, exitCode);
        Assert.Equal("UNAVAILABLE", summary.GetProperty("verdict").GetString());
        Assert.Equal("no display", summary.GetProperty("reason").GetString());
    }

    [Fact]
    public void Ledger_AnExpectedFailureThatSucceeds_Fails()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'probe' $pwsh @('-NoProfile', '-Command', 'exit 0') -ExpectFailure -Check { param($o) 'rejected' }
            """, out int exitCode);

        Assert.Equal(1, exitCode);
        Assert.Contains("the guard did not engage", Step(summary, "probe").GetProperty("Note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_ACheckThatRejectsACleanRun_FailsAndAFailedRunStillReportsItsCount()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'format' $pwsh @('-NoProfile', '-Command', 'exit 0') -Check { param($o) throw 'could not load the workspace' }
            Invoke-ValidationStep 'test' $pwsh @('-NoProfile', '-Command', 'Write-Output "total: 7"; exit 2') -Check { param($o) '7 tests' }
            """, out int exitCode);

        Assert.Equal(1, exitCode);
        Assert.Contains("could not load the workspace", Step(summary, "format").GetProperty("Note").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("exit code 2 (7 tests)", Step(summary, "test").GetProperty("Note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_AStepThatLeavesItsOverrideBehind_HasItRemoved()
    {
        JsonElement summary = Validate("""
            Invoke-ValidationStep 'proof' $pwsh @('-NoProfile', '-Command', "Set-Content -LiteralPath '$(Join-Path $game 'override.cfg')' -Value x")
            """, out int exitCode);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(_work, "game", "override.cfg")));
        Assert.Contains("removed the override.cfg it left behind", Step(summary, "proof").GetProperty("Note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Ledger_EvidenceOutsideTheProject_IsRefused()
    {
        string message = Fail("""
            New-Item -ItemType Directory -Path 'game', 'elsewhere' | Out-Null
            Start-Validation -Project (Join-Path $PWD.Path 'game') -Evidence (Join-Path $PWD.Path 'elsewhere') -Title 'Game' -Selection 'scope headless'
            """);

        Assert.Contains("Evidence must stay inside the project", message, StringComparison.Ordinal);
    }

    private static string Proof(string output, int exitCode, string expected = "@()") =>
        $"$r = [pscustomobject]@{{ ExitCode = {exitCode}; TimedOut = $false; Output = \"{output.Replace("\n", "`n", StringComparison.Ordinal)}\" }}\n"
        + $"Assert-GodotProof -Result $r -Name 'Sample proof' -Marker 'PROOF_PASS' -ExpectedDiagnostics {expected}";

    private JsonElement Validate(string steps, out int exitCode, string unavailable = "")
    {
        string evidence = Path.Combine(_work, "game", "artifacts", "validation");
        Directory.CreateDirectory(evidence);
        // A stale summary from an earlier run must be replaced, never left to stand for this one.
        File.WriteAllText(Path.Combine(evidence, "summary.json"), "{ \"verdict\": \"STALE\" }");
        (int code, string output) = Run($"""
            $game = Join-Path $PWD.Path 'game'
            Start-Validation -Project $game -Evidence (Join-Path $game 'artifacts/validation') -Title 'Game' -Selection 'scope headless'
            {steps}
            exit (Complete-Validation -Unavailable '{unavailable}')
            """);
        exitCode = code;
        Assert.True(File.Exists(Path.Combine(evidence, "summary.md")), output);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence, "summary.json")));
        Assert.Equal(code, document.RootElement.GetProperty("exitCode").GetInt32());
        return document.RootElement.Clone();
    }

    private static JsonElement Step(JsonElement summary, string name) =>
        summary.GetProperty("steps").EnumerateArray().Single(step => step.GetProperty("Step").GetString() == name);

    private static string OtherPlatform() => OperatingSystem.IsWindows() ? "Linux" : "Windows";

    private string FakeExecutable(string name, string output)
    {
        if (OperatingSystem.IsWindows())
        {
            string command = Path.Combine(_work, name + ".cmd");
            File.WriteAllText(command, $"@echo {output}\r\n");
            return command;
        }

        string script = Path.Combine(_work, name);
        File.WriteAllText(script, $"#!/bin/sh\necho '{output}'\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return script;
    }

    private static string[] Lines(string output) =>
        [.. output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private string Pass(string body)
    {
        (int exitCode, string output) = Run(body);
        Assert.True(exitCode == 0, output);
        return output;
    }

    private string Fail(string body)
    {
        (int exitCode, string output) = Run(body);
        Assert.True(exitCode != 0, "The script was expected to fail but succeeded:\n" + output);
        return output;
    }

    private (int ExitCode, string Output) Run(string body)
    {
        string script = Path.Combine(_work, "test.ps1");
        File.WriteAllText(script, string.Join('\n',
            "$ErrorActionPreference = 'Stop'",
            $"Import-Module '{Module}' -Force",
            "$pwsh = (Get-Command -Name 'pwsh' -CommandType Application | Select-Object -First 1).Source",
            body));
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = _work,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-File", script })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell 7 (pwsh) is required to test the validation module.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        // One limit covers the script's exit and the end of its output, which a process it left behind could hold.
        var limit = Stopwatch.StartNew();
        if (!process.WaitForExit(180_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The PowerShell test script did not finish.");
        }

        TimeSpan remaining = TimeSpan.FromSeconds(180) - limit.Elapsed;
        if (!Task.WaitAll([output, error], (int)Math.Max(0, remaining.TotalMilliseconds), TestContext.Current.CancellationToken))
        {
            throw new TimeoutException("The PowerShell test script exited, but something it started still held its output open.");
        }

        return (process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }
}
