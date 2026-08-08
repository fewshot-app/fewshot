using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Fewshot.Api.Services;

public enum PresidioStatus { Stopped, Starting, Running, Restarting, Disabled }

public class PresidioProcessManager : IHostedService, IAsyncDisposable
{
    private readonly ILogger<PresidioProcessManager> _logger;
    private readonly IConfiguration _configuration;
    private string _scriptPath = "";
    private string _pythonExe = "";

    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _watcherTask;
    private int _restartCount;
    private DateTime? _startedAt;
    private bool _intentionallyStopped;

    private static readonly int[] BackoffSeconds = [3, 10, 30, 60];

    public PresidioStatus Status { get; private set; } = PresidioStatus.Stopped;
    public int RestartCount => _restartCount;
    public DateTime? StartedAt => _startedAt;
    public int? Pid => _process is { HasExited: false } p ? p.Id : null;

    public event EventHandler? StatusChanged;

    public PresidioProcessManager(ILogger<PresidioProcessManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        ResolvePaths();
    }

    private void ResolvePaths()
    {
        // Configurable paths — fall back to probing
        var configScript = _configuration["Fewshot:Presidio:ScriptPath"];
        var configPython = _configuration["Fewshot:Presidio:PythonPath"];

        _scriptPath = !string.IsNullOrEmpty(configScript) && File.Exists(configScript)
            ? configScript
            : ResolveScriptPath();

        _pythonExe = !string.IsNullOrEmpty(configPython) && TryPython(configPython)
            ? configPython
            : ResolvePython();

        _logger.LogInformation("Presidio script: {Script} (exists: {Exists})", _scriptPath, File.Exists(_scriptPath));
        _logger.LogInformation("Presidio python: {Python} (resolved: {Ok})", _pythonExe, !string.IsNullOrEmpty(_pythonExe));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Re-resolve on every start attempt so a restart can pick up
        // newly installed python or newly written config/script.
        if (!File.Exists(_scriptPath) || string.IsNullOrEmpty(_pythonExe))
            ResolvePaths();

        if (!File.Exists(_scriptPath))
        {
            _logger.LogWarning("Presidio script not found at {Path} — Presidio disabled", _scriptPath);
            Status = PresidioStatus.Disabled;
            return;
        }

        if (string.IsNullOrEmpty(_pythonExe))
        {
            _logger.LogWarning("Python not found — Presidio disabled");
            Status = PresidioStatus.Disabled;
            return;
        }

        _intentionallyStopped = false;
        _cts = new CancellationTokenSource();
        _watcherTask = WatchAsync(_cts.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _intentionallyStopped = true;
        await KillProcessAsync();
        if (_cts != null) await _cts.CancelAsync();
        if (_watcherTask != null)
        {
            try { await _watcherTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken); }
            catch { }
        }
        Status = PresidioStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RestartAsync()
    {
        _intentionallyStopped = false;
        _restartCount = 0;
        await KillProcessAsync();

        // If the watcher never started (Disabled at boot) or has exited, re-enter StartAsync
        // so a restart can recover once python/script are available or configured.
        if (_watcherTask is null || _watcherTask.IsCompleted)
            await StartAsync();
    }

    private async Task WatchAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_intentionallyStopped)
        {
            try
            {
                await StartProcessAsync(ct);

                if (_process != null)
                {
                    await _process.WaitForExitAsync(ct);

                    if (_intentionallyStopped || ct.IsCancellationRequested) break;

                    var exitCode = _process.ExitCode;
                    _logger.LogWarning("Presidio exited ({Code}) — restarting (attempt {N})", exitCode, _restartCount + 1);
                    Status = PresidioStatus.Restarting;
                    StatusChanged?.Invoke(this, EventArgs.Empty);

                    var delay = BackoffSeconds[Math.Min(_restartCount, BackoffSeconds.Length - 1)];
                    _restartCount++;
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Presidio watcher error");
                Status = PresidioStatus.Restarting;
                StatusChanged?.Invoke(this, EventArgs.Empty);
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }

        Status = PresidioStatus.Stopped;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task StartProcessAsync(CancellationToken ct)
    {
        Status = PresidioStatus.Starting;
        StatusChanged?.Invoke(this, EventArgs.Empty);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonExe,
            Arguments = $"\"{_scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        _process.OutputDataReceived += (_, e) => { if (e.Data != null) _logger.LogDebug("[Presidio] {Line}", e.Data); };
        _process.ErrorDataReceived += (_, e) => { if (e.Data != null) _logger.LogDebug("[Presidio:err] {Line}", e.Data); };

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        _startedAt = DateTime.UtcNow;
        _logger.LogInformation("Presidio started (PID {Pid})", _process.Id);

        await Task.Delay(1500, ct);
        Status = PresidioStatus.Running;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task KillProcessAsync()
    {
        if (_process is { HasExited: false } p)
        {
            try
            {
                p.Kill(entireProcessTree: true);
                await p.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch { }
        }
        _process = null;
        _startedAt = null;
    }



    private static string ResolveScriptPath()
    {
        // Probe user profiles for Fewshot\presidio\serve.py
        var candidates = new List<string>();

        // Current account's LocalAppData (works for user-mode runs)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
            candidates.Add(Path.Combine(localAppData, "Fewshot", "presidio", "serve.py"));

        // Scan C:\Users\*\AppData\Local\Fewshot (covers service account not matching installer user)
        var usersDir = @"C:\Users";
        if (Directory.Exists(usersDir))
        {
            foreach (var userDir in Directory.GetDirectories(usersDir))
            {
                var candidate = Path.Combine(userDir, "AppData", "Local", "Fewshot", "presidio", "serve.py");
                if (!candidates.Contains(candidate))
                    candidates.Add(candidate);
            }
        }

        // ProgramData fallback
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Fewshot", "presidio", "serve.py"));

        return candidates.FirstOrDefault(File.Exists) ?? candidates.First();
    }

    private static bool TryPython(string path)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            p?.WaitForExit(2000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string ResolvePython()
    {
        // Try PATH-based candidates first
        foreach (var candidate in new[] { "python", "python3" })
        {
            if (TryPython(candidate)) return candidate;
        }

        // Probe common Windows install locations (service accounts lack user PATH)
        var probeRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"C:\Python3",
        };

        foreach (var root in probeRoots)
        {
            if (string.IsNullOrEmpty(root)) continue;

            // LocalAppData\Programs\Python\Python3XX\python.exe
            var programsDir = Path.Combine(root, "Programs", "Python");
            if (Directory.Exists(programsDir))
            {
                foreach (var dir in Directory.GetDirectories(programsDir, "Python3*"))
                {
                    var exe = Path.Combine(dir, "python.exe");
                    if (TryPython(exe)) return exe;
                }
            }

            // ProgramFiles\Python3XX\python.exe
            if (Directory.Exists(root))
            {
                foreach (var dir in Directory.GetDirectories(root, "Python3*"))
                {
                    var exe = Path.Combine(dir, "python.exe");
                    if (TryPython(exe)) return exe;
                }
            }
        }

        return "";
    }

    public async ValueTask DisposeAsync()
    {
        await KillProcessAsync();
        _cts?.Dispose();
    }
}
