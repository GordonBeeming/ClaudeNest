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

    public bool HasActiveSessions()
    {
        return _sessions.Values.Any(s => s.State is SessionState.Starting or SessionState.Running or SessionState.Stopping);
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
                using var proc = Process.GetProcessById(session.Pid.Value);
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
            using var whichProcess = new Process
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

    private async Task<(bool IsAuthenticated, string? Error)> CheckClaudeAuthAsync(string claudeBinary, string workingDirectory)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = claudeBinary,
                    Arguments = "auth status",
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            // Wait with a timeout — auth status should be quick
            var exited = process.WaitForExit(10_000);
            if (!exited)
            {
                try { process.Kill(); } catch { /* best effort */ }
                return (false, "Claude CLI auth check timed out. The CLI may be unresponsive.");
            }

            if (process.ExitCode != 0)
            {
                var errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim();
                return (false, $"Claude CLI is not authenticated (exit code {process.ExitCode}). {errorDetail}".Trim());
            }

            // Check if the output confirms logged in — reject anything that isn't explicitly true
            var isLoggedIn = stdout.Contains("\"loggedIn\": true", StringComparison.OrdinalIgnoreCase) ||
                             stdout.Contains("\"loggedIn\":true", StringComparison.OrdinalIgnoreCase);
            if (!isLoggedIn)
            {
                return (false, "Claude CLI is not logged in. Run 'claude login' interactively on this machine to authenticate.");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to check Claude CLI auth: {ex.Message}");
        }
    }

    private async Task SpawnProcessAsync(ManagedSession session)
    {
        try
        {
            var claudeBinary = ResolveBinaryPath(config.ClaudeBinary);

            // Pre-flight: verify Claude CLI is authenticated
            var (isAuthenticated, authError) = await CheckClaudeAuthAsync(claudeBinary, session.Path);
            if (!isAuthenticated)
            {
                logger.LogError("Claude CLI auth check failed for session {SessionId}: {Error}", session.SessionId, authError);
                session.State = SessionState.Crashed;
                session.EndedAt = DateTime.UtcNow;
                session.ErrorMessage = authError;
                NotifyStatusChanged(session);
                return;
            }

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

            // Read stderr line-by-line so errors are reported in real-time.
            // Keep only the last N lines to avoid unbounded memory growth.
            const int maxStderrLines = 50;
            var stderrLines = new List<string>();
            var stderrTask = Task.Run(async () =>
            {
                try
                {
                    while (await process.StandardError.ReadLineAsync() is { } line)
                    {
                        stderrLines.Add(line);
                        if (stderrLines.Count > maxStderrLines)
                            stderrLines.RemoveAt(0);

                        logger.LogWarning("Session {SessionId} stderr: {Line}", session.SessionId, line);

                        // Report first stderr as error immediately (while process is still running)
                        if (session.State is SessionState.Running or SessionState.Starting)
                        {
                            if (line.Contains("Workspace not trusted"))
                                session.ErrorMessage = $"Workspace not trusted. Run 'claude' once in {session.Path} on the agent machine to accept the trust dialog, then try again.";
                            else
                                session.ErrorMessage = string.Join('\n', stderrLines).Trim();
                            NotifyStatusChanged(session);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Stderr reader ended for session {SessionId}", session.SessionId);
                }
            });

            // Drain stdout to prevent buffer deadlock
            var stdoutTask = Task.Run(async () =>
            {
                try
                {
                    var buffer = new char[4096];
                    while (await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length) > 0)
                    {
                        // Discard stdout — just drain the buffer to prevent deadlock
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Stdout drain ended for session {SessionId}", session.SessionId);
                }
            });

            // Only transition to Running if the process hasn't already exited
            if (!process.HasExited)
            {
                session.State = SessionState.Running;
                NotifyStatusChanged(session);
            }

            // Check for early exit — if the process dies within a few seconds, it likely failed to authenticate
            var earlyExitCheck = Task.Delay(TimeSpan.FromSeconds(5));
            var processExit = process.WaitForExitAsync();
            var completed = await Task.WhenAny(earlyExitCheck, processExit);

            if (completed == processExit && process.ExitCode != 0 && string.IsNullOrEmpty(session.ErrorMessage))
            {
                session.ErrorMessage = stderrLines.Count > 0
                    ? string.Join('\n', stderrLines).Trim()
                    : $"claude remote-control exited immediately with code {process.ExitCode}. " +
                      "This usually means the Claude CLI is not authenticated. " +
                      "Run 'claude' interactively on this machine to complete the login flow.";
            }

            if (completed == earlyExitCheck)
            {
                // Process survived the first 5 seconds — wait for it to finish
                await processExit;
            }

            // Wait for reader tasks to finish so all stderr is captured
            await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);

            if (session.State is SessionState.Stopped or SessionState.Crashed)
            {
                process.Dispose();
                return; // Already handled
            }

            if (stderrLines.Count > 0 && string.IsNullOrEmpty(session.ErrorMessage))
                session.ErrorMessage = string.Join('\n', stderrLines).Trim();

            session.State = session.State == SessionState.Stopping || process.ExitCode == 0
                ? SessionState.Stopped
                : SessionState.Crashed;
            session.EndedAt = DateTime.UtcNow;
            session.ExitCode = process.ExitCode;
            NotifyStatusChanged(session);
            process.Dispose();
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
