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

    private async Task SpawnProcessAsync(ManagedSession session)
    {
        try
        {
            var arguments = !string.IsNullOrEmpty(session.PermissionMode) && session.PermissionMode != "default"
                ? $"remote-control --permission-mode {session.PermissionMode}"
                : "remote-control";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = config.ClaudeBinary,
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
            session.ErrorMessage = ex.Message;
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
