using System.Collections.Concurrent;
using System.Diagnostics;
using ClaudeNest.Agent.Config;
using ClaudeNest.Shared.Enums;
using ClaudeNest.Shared.Messages;

namespace ClaudeNest.Agent.Services;

public sealed class SessionManager(
    NestConfig config,
    DirectoryBrowser directoryBrowser,
    Guid agentId,
    ILogger<SessionManager> logger)
{
    private readonly ConcurrentDictionary<Guid, ManagedSession> _sessions = new();

    public event Func<SessionStatusUpdate, Task>? OnSessionStatusChanged;

    public bool TryStartSession(Guid sessionId, string path, string? permissionMode, out string? error)
    {
        error = null;

        if (!directoryBrowser.IsPathAllowed(path))
        {
            error = "Path is not allowed";
            return false;
        }

        var activeSessions = _sessions.Values
            .Where(s => s.State is SessionState.Starting or SessionState.Running or SessionState.Stopping)
            .ToList();

        // Check for overlapping paths (same, parent, or child of an active session)
        var resolvedCheck = Path.GetFullPath(path);
        var overlapping = activeSessions.FirstOrDefault(s =>
            string.Equals(s.Path, resolvedCheck, StringComparison.OrdinalIgnoreCase) ||
            resolvedCheck.StartsWith(s.Path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            s.Path.StartsWith(resolvedCheck + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        if (overlapping is not null)
        {
            error = $"An active session already covers this path ({overlapping.Path})";
            return false;
        }

        var resolvedPath = Path.GetFullPath(path);
        var session = new ManagedSession
        {
            SessionId = sessionId,
            Path = resolvedPath,
            State = SessionState.Starting,
            PermissionMode = permissionMode
        };

        if (!_sessions.TryAdd(sessionId, session))
        {
            error = "Session already exists";
            return false;
        }

        NotifyStatusChanged(session);
        _ = SpawnProcessAsync(session);
        return true;
    }

    public bool TryStopSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return false;

        if (session.State is not (SessionState.Starting or SessionState.Running))
            return false;

        session.State = SessionState.Stopping;
        NotifyStatusChanged(session);

        try
        {
            session.Process?.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill process for session {SessionId}", sessionId);
        }

        return true;
    }

    public List<SessionStatusUpdate> ReconcileSessions(List<ActiveSessionInfo> serverSessions)
    {
        var updates = new List<SessionStatusUpdate>();

        foreach (var serverSession in serverSessions)
        {
            // Skip if we're already tracking this session
            if (_sessions.ContainsKey(serverSession.SessionId))
                continue;

            var isAlive = false;
            Process? process = null;
            if (serverSession.Pid is not null)
            {
                try
                {
                    process = Process.GetProcessById(serverSession.Pid.Value);
                    isAlive = !process.HasExited;
                }
                catch
                {
                    // Process doesn't exist
                }
            }

            if (isAlive && process is not null)
            {
                // Process is still running — adopt it
                var session = new ManagedSession
                {
                    SessionId = serverSession.SessionId,
                    Path = serverSession.Path,
                    State = SessionState.Running,
                    Pid = serverSession.Pid
                };
                session.Process = process;
                _sessions.TryAdd(serverSession.SessionId, session);

                logger.LogInformation(
                    "Adopted running session {SessionId} (PID {Pid})",
                    serverSession.SessionId, serverSession.Pid);

                // Monitor the process for exit
                _ = MonitorAdoptedProcessAsync(session);
            }
            else
            {
                // Process is gone — report as crashed
                logger.LogInformation(
                    "Session {SessionId} (PID {Pid}) is no longer running, reporting as crashed",
                    serverSession.SessionId, serverSession.Pid);
            }

            updates.Add(new SessionStatusUpdate
            {
                SessionId = serverSession.SessionId,
                AgentId = agentId,
                Path = serverSession.Path,
                State = isAlive ? SessionState.Running : SessionState.Crashed,
                Pid = serverSession.Pid,
                EndedAt = isAlive ? null : DateTime.UtcNow,
                ErrorMessage = isAlive ? null : "Process no longer running after agent restart"
            });
        }

        return updates;
    }

    public List<SessionStatusUpdate> GetAllSessions()
    {
        return _sessions.Values.Select(ToStatusUpdate).ToList();
    }

    public void StopAllSessions()
    {
        foreach (var (id, session) in _sessions)
        {
            if (session.State is SessionState.Starting or SessionState.Running)
            {
                TryStopSession(id);
            }
        }
    }

    public void HealthCheck()
    {
        foreach (var session in _sessions.Values)
        {
            if (session.State is not (SessionState.Running or SessionState.Starting))
                continue;

            if (session.Pid is null)
                continue;

            try
            {
                Process.GetProcessById(session.Pid.Value);
            }
            catch
            {
                // Process no longer exists
                session.State = SessionState.Crashed;
                session.EndedAt = DateTime.UtcNow;
                NotifyStatusChanged(session);
            }
        }

        // Clean up old stopped/crashed sessions (older than 1 hour)
        var cutoff = DateTime.UtcNow.AddHours(-1);
        foreach (var (id, session) in _sessions)
        {
            if (session.State is SessionState.Stopped or SessionState.Crashed &&
                session.EndedAt < cutoff)
            {
                _sessions.TryRemove(id, out _);
            }
        }
    }

    private string ResolveBinaryPath(string binary)
    {
        // If already an absolute path, use it directly
        if (Path.IsPathRooted(binary))
            return binary;

        // Try to find it via 'which' (Unix) or 'where' (Windows)
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var whichProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "where" : "which",
                    Arguments = binary,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            whichProcess.Start();
            var result = whichProcess.StandardOutput.ReadLine()?.Trim();
            whichProcess.WaitForExit(5000);
            if (whichProcess.ExitCode == 0 && !string.IsNullOrEmpty(result) && File.Exists(result))
            {
                logger.LogInformation("Resolved '{Binary}' to '{FullPath}'", binary, result);
                return result;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve binary via which/where");
        }

        // Check common installation paths as fallback
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] commonPaths =
        [
            Path.Combine(home, ".local", "bin", binary),
            Path.Combine(home, ".npm-global", "bin", binary),
            $"/usr/local/bin/{binary}",
            $"/opt/homebrew/bin/{binary}",
        ];

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                logger.LogInformation("Found '{Binary}' at '{FullPath}'", binary, path);
                return path;
            }
        }

        // Return as-is and let the process start fail with a clear error
        logger.LogWarning("Could not resolve full path for '{Binary}', using as-is", binary);
        return binary;
    }

    private async Task SpawnProcessAsync(ManagedSession session)
    {
        try
        {
            var claudeBinary = ResolveBinaryPath(config.ClaudeBinary);

            var arguments = !string.IsNullOrEmpty(session.PermissionMode) && session.PermissionMode != "default"
                ? $"remote-control --permission-mode {session.PermissionMode}"
                : "remote-control";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = claudeBinary,
                    Arguments = arguments,
                    WorkingDirectory = session.Path,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            session.Process = process;
            session.Pid = process.Id;

            // Start capturing stderr immediately (before checking exit status)
            var stderrTask = process.StandardError.ReadToEndAsync();
            _ = process.StandardOutput.ReadToEndAsync();

            // Only transition to Running if the process hasn't already exited
            if (!process.HasExited)
            {
                session.State = SessionState.Running;
                NotifyStatusChanged(session);
            }

            await process.WaitForExitAsync();

            // Capture stderr for error reporting
            var stderr = await stderrTask;
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogWarning("Session {SessionId} stderr: {StdErr}", session.SessionId, stderr);

                // Make trust errors actionable
                if (stderr.Contains("Workspace not trusted"))
                    session.ErrorMessage = $"Workspace not trusted. Run 'claude' once in {session.Path} on the agent machine to accept the trust dialog, then try again.";
                else
                    session.ErrorMessage = stderr.Trim();
            }

            if (session.State is SessionState.Stopped or SessionState.Crashed)
                return; // Already handled

            session.State = session.State == SessionState.Stopping || process.ExitCode == 0
                ? SessionState.Stopped
                : SessionState.Crashed;
            session.EndedAt = DateTime.UtcNow;
            session.ExitCode = process.ExitCode;
            NotifyStatusChanged(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start claude process for session {SessionId}", session.SessionId);
            session.State = SessionState.Crashed;
            session.EndedAt = DateTime.UtcNow;
            session.ErrorMessage = ex.Message.Contains("No such file or directory")
                ? $"{ex.Message} Set ClaudeBinary in ~/.claudenest/config.json to the full path (e.g. /Users/you/.local/bin/claude)."
                : ex.Message;
            NotifyStatusChanged(session);
        }
    }

    private async Task MonitorAdoptedProcessAsync(ManagedSession session)
    {
        try
        {
            if (session.Process is null) return;
            await session.Process.WaitForExitAsync();

            if (session.State is SessionState.Stopped or SessionState.Crashed)
                return;

            session.State = session.Process.ExitCode == 0
                ? SessionState.Stopped
                : SessionState.Crashed;
            session.EndedAt = DateTime.UtcNow;
            session.ExitCode = session.Process.ExitCode;
            NotifyStatusChanged(session);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error monitoring adopted session {SessionId}", session.SessionId);
            session.State = SessionState.Crashed;
            session.EndedAt = DateTime.UtcNow;
            NotifyStatusChanged(session);
        }
    }

    private void NotifyStatusChanged(ManagedSession session)
    {
        var update = ToStatusUpdate(session);
        OnSessionStatusChanged?.Invoke(update);
    }

    private SessionStatusUpdate ToStatusUpdate(ManagedSession session)
    {
        return new SessionStatusUpdate
        {
            SessionId = session.SessionId,
            AgentId = agentId,
            Path = session.Path,
            State = session.State,
            Pid = session.Pid,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            ExitCode = session.ExitCode,
            ErrorMessage = session.ErrorMessage
        };
    }
}
