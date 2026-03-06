import { Folder, Square, ExternalLink, Clock } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import type { SessionStatus } from "../types";
import { StatusBadge } from "./StatusBadge";

interface SessionCardProps {
  session: SessionStatus;
  onStop: (sessionId: string) => void;
}

function SessionCard({ session, onStop }: SessionCardProps) {
  const isActive =
    session.state === "Running" ||
    session.state === "Starting" ||
    session.state === "Requested";
  const isStopping = session.state === "Stopping";

  // Extract just the folder name for display (handle both / and \ separators)
  const folderName = session.path.split(/[/\\]/).filter(Boolean).pop() || session.path;

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <Folder className="h-4 w-4 shrink-0 text-nest-500" />
            <span className="truncate font-medium text-sm" title={session.path}>
              {folderName}
            </span>
            <StatusBadge state={session.state} />
          </div>
          <div className="mt-2 flex items-center gap-3 text-xs text-gray-400">
            {session.pid && <span>PID: {session.pid}</span>}
            <span className="flex items-center gap-1">
              <Clock className="h-3 w-3" />
              {formatDistanceToNow(new Date(session.startedAt), {
                addSuffix: true,
              })}
            </span>
            {session.endedAt && (
              <span>
                Ran for{" "}
                {formatDistanceToNow(new Date(session.startedAt), {
                  includeSeconds: true,
                })}
              </span>
            )}
            {session.exitCode !== null && (
              <span>Exit: {session.exitCode}</span>
            )}
          </div>
          {session.state === "Crashed" && session.errorMessage && (
            <p className="mt-2 rounded-md bg-red-50 px-2 py-1.5 text-xs text-red-600 dark:bg-red-950/50 dark:text-red-400">
              {session.errorMessage}
            </p>
          )}
        </div>

        <div className="flex shrink-0 flex-col items-stretch gap-2 sm:flex-row sm:items-center">
          {isActive && (
            <>
              <a
                href="https://claude.ai/code"
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center justify-center gap-1 rounded-lg border border-gray-200 px-3 py-2.5 sm:py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <ExternalLink className="h-3 w-3" />
                Open in Claude
              </a>
              <button
                onClick={() => onStop(session.sessionId)}
                disabled={isStopping}
                className="flex items-center justify-center gap-1 rounded-lg bg-red-50 px-3 py-2.5 sm:py-1.5 text-xs font-medium text-red-600 hover:bg-red-100 disabled:opacity-50 dark:bg-red-950/50 dark:text-red-400 dark:hover:bg-red-950"
              >
                <Square className="h-3 w-3" />
                Stop
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

interface SessionPanelProps {
  sessions: SessionStatus[];
  maxSessions?: number;
  accountWide?: boolean;
  onStop: (sessionId: string) => void;
}

export function SessionPanel({ sessions, maxSessions, accountWide, onStop }: SessionPanelProps) {
  const activeSessions = sessions.filter(
    (s) =>
      s.state === "Running" ||
      s.state === "Starting" ||
      s.state === "Requested",
  );
  const pastSessions = sessions.filter(
    (s) =>
      s.state === "Stopped" || s.state === "Crashed" || s.state === "Stopping",
  );

  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-medium text-gray-900 dark:text-white">
            Active Sessions
          </h3>
          {maxSessions && (
            <span className="text-xs text-gray-400">
              {activeSessions.length} of {maxSessions} sessions{accountWide ? " (account-wide)" : ""}
            </span>
          )}
        </div>

        {activeSessions.length === 0 ? (
          <p className="mt-3 text-center text-sm text-gray-400">
            No active sessions. Browse folders below to launch one.
          </p>
        ) : (
          <div className="mt-3 space-y-2">
            {activeSessions.map((s) => (
              <SessionCard key={s.sessionId} session={s} onStop={onStop} />
            ))}
          </div>
        )}
      </div>

      {pastSessions.length > 0 && (
        <div>
          <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400">
            Recent Sessions
          </h3>
          <div className="mt-3 space-y-2">
            {pastSessions.slice(0, 5).map((s) => (
              <SessionCard key={s.sessionId} session={s} onStop={onStop} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
