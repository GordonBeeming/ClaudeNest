import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, Link } from "react-router-dom";
import {
  ChevronLeft,
  RefreshCw,
  FolderTree as FolderTreeIcon,
  Activity,
} from "lucide-react";
import { getAgent } from "../api";
import type { Agent, SessionStatus } from "../types";
import { OnlineBadge } from "../components/StatusBadge";
import { FolderTree } from "../components/FolderTree";
import { SessionPanel } from "../components/SessionPanel";
import { useSignalRContext } from "../contexts/SignalRContext";
import { useUserContext } from "../contexts/UserContext";

export function AgentDetail() {
  const { agentId } = useParams<{ agentId: string }>();
  const { user } = useUserContext();
  const accountMaxSessions = user?.account?.maxSessions ?? 0;
  const [agent, setAgent] = useState<Agent | null>(null);
  const [sessions, setSessions] = useState<SessionStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const {
    subscribeToAgent,
    requestGetSessions,
    requestStartSession,
    requestStopSession,
    onSessionStatusChanged,
    onAllSessionsUpdated,
    onAgentStatusChanged,
    connected,
  } = useSignalRContext();

  const fetchAgent = useCallback(async () => {
    if (!agentId) return;
    try {
      const data = await getAgent(agentId);
      setAgent(data);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load agent");
    } finally {
      setLoading(false);
    }
  }, [agentId]);

  useEffect(() => {
    fetchAgent();
  }, [fetchAgent]);

  // Subscribe to agent updates via SignalR
  useEffect(() => {
    if (!agentId || !connected) return;
    subscribeToAgent(agentId);
    requestGetSessions(agentId);
  }, [agentId, connected, subscribeToAgent, requestGetSessions]);

  // Listen for real-time session updates
  useEffect(() => {
    const unsub1 = onSessionStatusChanged((update: SessionStatus) => {
      if (update.agentId !== agentId) return;
      setSessions((prev) => {
        const idx = prev.findIndex((s) => s.sessionId === update.sessionId);
        if (idx >= 0) {
          const next = [...prev];
          next[idx] = update;
          return next;
        }
        return [update, ...prev];
      });
    });

    const unsub2 = onAllSessionsUpdated(
      (id: string, allSessions: SessionStatus[]) => {
        if (id !== agentId) return;
        setSessions(allSessions);
      },
    );

    const unsub3 = onAgentStatusChanged((id: string, isOnline: boolean) => {
      if (id !== agentId) return;
      setAgent((prev) => (prev ? { ...prev, isOnline } : prev));
    });

    return () => {
      unsub1();
      unsub2();
      unsub3();
    };
  }, [agentId, onSessionStatusChanged, onAllSessionsUpdated, onAgentStatusChanged]);

  // Compute paths that have an active session (for blocking duplicate launches)
  const activeSessionPaths = useMemo(
    () =>
      sessions
        .filter(
          (s) =>
            s.state === "Running" ||
            s.state === "Starting" ||
            s.state === "Requested" ||
            s.state === "Stopping",
        )
        .map((s) => s.path),
    [sessions],
  );

  const activeSessions = useMemo(
    () =>
      sessions.filter(
        (s) =>
          s.state === "Running" ||
          s.state === "Starting" ||
          s.state === "Requested",
      ),
    [sessions],
  );
  const atSessionLimit = accountMaxSessions > 0 && activeSessions.length >= accountMaxSessions;

  const handleLaunchSession = useCallback(
    (path: string) => {
      if (!agentId) return;
      const sessionId = crypto.randomUUID();
      requestStartSession(agentId, sessionId, path);

      // Optimistically add the session
      setSessions((prev) => [
        {
          sessionId,
          agentId,
          path,
          state: "Requested",
          pid: null,
          startedAt: new Date().toISOString(),
          endedAt: null,
          exitCode: null,
        },
        ...prev,
      ]);
    },
    [agentId, requestStartSession],
  );

  const handleStopSession = useCallback(
    (sessionId: string) => {
      if (!agentId) return;
      requestStopSession(agentId, sessionId);
    },
    [agentId, requestStopSession],
  );

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  if (error || !agent) {
    return (
      <div className="py-8 text-center">
        <p className="text-red-500">{error || "Agent not found"}</p>
        <Link
          to="/"
          className="mt-4 inline-flex items-center gap-1 text-sm text-nest-500 hover:text-nest-600"
        >
          <ChevronLeft className="h-4 w-4" />
          Back to dashboard
        </Link>
      </div>
    );
  }

  return (
    <div>
      <Link
        to="/"
        className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
      >
        <ChevronLeft className="h-4 w-4" />
        All Agents
      </Link>

      <div className="mt-4 flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">
              {agent.name || agent.hostname || "Unnamed Agent"}
            </h1>
            <OnlineBadge isOnline={agent.isOnline} />
          </div>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            {agent.hostname}
            {agent.os ? ` \u00b7 ${agent.os}` : ""}
          </p>
        </div>
      </div>

      {!agent.isOnline && (
        <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
          This agent is currently offline. Session controls are unavailable
          until it reconnects.
        </div>
      )}

      <div className="mt-8 grid gap-8 lg:grid-cols-2">
        {/* Sessions */}
        <div>
          <div className="mb-4 flex items-center gap-2">
            <Activity className="h-4 w-4 text-nest-500" />
            <h2 className="font-medium text-gray-900 dark:text-white">
              Sessions
            </h2>
          </div>
          <SessionPanel
            sessions={sessions}
            maxSessions={accountMaxSessions}
            accountWide
            onStop={handleStopSession}
          />
        </div>

        {/* Folder Browser */}
        <div>
          <div className="mb-4 flex items-center gap-2">
            <FolderTreeIcon className="h-4 w-4 text-nest-500" />
            <h2 className="font-medium text-gray-900 dark:text-white">
              Browse Folders
            </h2>
          </div>
          <div className="rounded-xl border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900">
            {agent.isOnline ? (
              <FolderTree
                agentId={agent.id}
                allowedPaths={agent.allowedPaths}
                activeSessionPaths={activeSessionPaths}
                atSessionLimit={atSessionLimit}
                onLaunch={handleLaunchSession}
              />
            ) : (
              <p className="py-8 text-center text-sm text-gray-400">
                Agent must be online to browse folders.
              </p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
