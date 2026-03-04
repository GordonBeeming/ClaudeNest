import { useState, useEffect, useCallback, useMemo } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import {
  ChevronLeft,
  RefreshCw,
  FolderTree as FolderTreeIcon,
  Activity,
  Trash2,
  Download,
  Terminal,
  Star,
  Eye,
  EyeOff,
} from "lucide-react";
import { getAgent, deleteAgent, triggerAgentUpdate, getFolderPreferences, upsertFolderPreference, deleteFolderPreference } from "../api";
import type { Agent, SessionStatus, FolderPreference } from "../types";
import { OnlineBadge } from "../components/StatusBadge";
import { FolderTree, FavoriteFolderTree } from "../components/FolderTree";
import { SessionPanel } from "../components/SessionPanel";
import { useSignalRContext } from "../contexts/SignalRContext";
import { useUserContext } from "../contexts/UserContext";

export function AgentDetail() {
  const { agentId } = useParams<{ agentId: string }>();
  const navigate = useNavigate();
  const { user } = useUserContext();
  const accountMaxSessions = user?.account?.maxSessions ?? 0;
  const [agent, setAgent] = useState<Agent | null>(null);
  const [sessions, setSessions] = useState<SessionStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const [removing, setRemoving] = useState(false);
  const [updateStatus, setUpdateStatus] = useState<string | null>(null);
  const [updatingAgent, setUpdatingAgent] = useState(false);
  const [preferences, setPreferences] = useState<Map<string, FolderPreference>>(new Map());
  const [showAllFolders, setShowAllFolders] = useState(true);

  const {
    subscribeToAgent,
    requestGetSessions,
    requestStartSession,
    requestStopSession,
    onSessionStatusChanged,
    onAllSessionsUpdated,
    onAgentStatusChanged,
    onAgentUpdateStatus,
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

  const fetchPreferences = useCallback(async () => {
    if (!agentId) return;
    try {
      const prefs = await getFolderPreferences(agentId);
      setPreferences(new Map(prefs.map((p) => [p.path, p])));
    } catch {
      // Non-critical — preferences just won't load
    }
  }, [agentId]);

  useEffect(() => {
    fetchAgent();
    fetchPreferences();
  }, [fetchAgent, fetchPreferences]);

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

    const unsub4 = onAgentUpdateStatus(
      (report: {
        agentId: string;
        status: string;
        error?: string;
        newVersion?: string;
      }) => {
        if (report.agentId !== agentId) return;
        if (report.status === "completed") {
          setUpdateStatus(
            `Updated to v${report.newVersion ?? "unknown"}`,
          );
          setUpdatingAgent(false);
          // Refresh agent data to get new version
          fetchAgent();
        } else if (report.status === "failed") {
          setUpdateStatus(`Update failed: ${report.error ?? "unknown error"}`);
          setUpdatingAgent(false);
        } else {
          setUpdateStatus(
            `Update status: ${report.status}${report.newVersion ? ` (v${report.newVersion})` : ""}`,
          );
        }
      },
    );

    return () => {
      unsub1();
      unsub2();
      unsub3();
      unsub4();
    };
  }, [agentId, onSessionStatusChanged, onAllSessionsUpdated, onAgentStatusChanged, onAgentUpdateStatus, fetchAgent]);

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

  const favoritePaths = useMemo(
    () =>
      Array.from(preferences.values())
        .filter((p) => p.isFavorite)
        .map((p) => p.path),
    [preferences],
  );

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
          errorMessage: null,
        },
        ...prev,
      ]);
    },
    [agentId, requestStartSession],
  );

  const handleToggleFavorite = useCallback(
    async (path: string, currentPref: FolderPreference | undefined) => {
      if (!agentId) return;
      const isFavorite = !currentPref?.isFavorite;

      // If removing favorite and no color, delete the preference entirely
      if (!isFavorite && !currentPref?.color) {
        if (currentPref) {
          // Optimistic update
          setPreferences((prev) => {
            const next = new Map(prev);
            next.delete(path);
            return next;
          });
          try {
            await deleteFolderPreference(agentId, currentPref.id);
          } catch {
            // Revert on error
            fetchPreferences();
          }
        }
        return;
      }

      // Optimistic update
      const optimisticPref: FolderPreference = {
        id: currentPref?.id ?? "",
        path,
        isFavorite,
        color: currentPref?.color ?? null,
        updatedAt: new Date().toISOString(),
      };
      setPreferences((prev) => {
        const next = new Map(prev);
        next.set(path, optimisticPref);
        return next;
      });

      try {
        const result = await upsertFolderPreference(agentId, {
          path,
          isFavorite,
          color: currentPref?.color ?? null,
        });
        setPreferences((prev) => {
          const next = new Map(prev);
          next.set(path, result);
          return next;
        });
      } catch {
        fetchPreferences();
      }
    },
    [agentId, fetchPreferences],
  );

  const handleSetColor = useCallback(
    async (path: string, color: string | null, currentPref: FolderPreference | undefined) => {
      if (!agentId) return;

      // If removing color and not favorite, delete the preference
      if (!color && !currentPref?.isFavorite) {
        if (currentPref) {
          setPreferences((prev) => {
            const next = new Map(prev);
            next.delete(path);
            return next;
          });
          try {
            await deleteFolderPreference(agentId, currentPref.id);
          } catch {
            fetchPreferences();
          }
        }
        return;
      }

      const optimisticPref: FolderPreference = {
        id: currentPref?.id ?? "",
        path,
        isFavorite: currentPref?.isFavorite ?? false,
        color,
        updatedAt: new Date().toISOString(),
      };
      setPreferences((prev) => {
        const next = new Map(prev);
        next.set(path, optimisticPref);
        return next;
      });

      try {
        const result = await upsertFolderPreference(agentId, {
          path,
          isFavorite: currentPref?.isFavorite ?? false,
          color,
        });
        setPreferences((prev) => {
          const next = new Map(prev);
          next.set(path, result);
          return next;
        });
      } catch {
        fetchPreferences();
      }
    },
    [agentId, fetchPreferences],
  );

  const handleTriggerUpdate = useCallback(async () => {
    if (!agentId) return;
    setUpdatingAgent(true);
    setUpdateStatus("Triggering update...");
    try {
      await triggerAgentUpdate(agentId);
      setUpdateStatus("Update triggered. Downloading...");
    } catch (e) {
      setUpdateStatus(
        e instanceof Error ? `Update failed: ${e.message}` : "Update failed",
      );
      setUpdatingAgent(false);
    }
  }, [agentId]);

  const handleRemoveAgent = useCallback(async () => {
    if (!agentId) return;
    setRemoving(true);
    try {
      await deleteAgent(agentId);
      navigate("/dashboard");
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to remove agent");
      setRemoving(false);
      setConfirmRemove(false);
    }
  }, [agentId, navigate]);

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
          to="/dashboard"
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
        to="/dashboard"
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
          {agent.version && (
            <p className="mt-0.5 text-sm text-gray-500 dark:text-gray-400">
              Version {agent.version}
              {agent.architecture ? ` (${agent.architecture})` : ""}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {agent.isOnline && agent.version && (
            <button
              onClick={handleTriggerUpdate}
              disabled={updatingAgent}
              className="flex items-center gap-1.5 rounded-lg border border-nest-200 px-3 py-1.5 text-sm font-medium text-nest-600 hover:bg-nest-50 disabled:opacity-50 dark:border-nest-800 dark:text-nest-400 dark:hover:bg-nest-950/30"
            >
              <Download className="h-4 w-4" />
              {updatingAgent ? "Updating..." : "Update"}
            </button>
          )}
          {confirmRemove ? (
            <div className="flex items-center gap-2">
              <span className="text-sm text-red-600 dark:text-red-400">
                {agent.isOnline
                  ? "This will stop all sessions and deregister the agent."
                  : "This will permanently remove this agent."}
              </span>
              <button
                onClick={handleRemoveAgent}
                disabled={removing}
                className="rounded-lg bg-red-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
              >
                {removing ? "Removing..." : "Confirm"}
              </button>
              <button
                onClick={() => setConfirmRemove(false)}
                disabled={removing}
                className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                Cancel
              </button>
            </div>
          ) : (
            <button
              onClick={() => setConfirmRemove(true)}
              className="flex items-center gap-1.5 rounded-lg border border-red-200 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950/30"
            >
              <Trash2 className="h-4 w-4" />
              Remove
            </button>
          )}
        </div>
      </div>

      {!agent.isOnline && (
        <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
          This agent is currently offline. Session controls are unavailable
          until it reconnects.
        </div>
      )}

      {updateStatus && (
        <div
          className={`mt-4 rounded-lg border px-4 py-3 text-sm ${
            updateStatus.includes("failed")
              ? "border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-950/30 dark:text-red-300"
              : updateStatus.includes("Updated to")
                ? "border-green-200 bg-green-50 text-green-700 dark:border-green-800 dark:bg-green-950/30 dark:text-green-300"
                : "border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-800 dark:bg-blue-950/30 dark:text-blue-300"
          }`}
        >
          {updateStatus}
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
          {/* Favorites Section */}
          {favoritePaths.length > 0 && (
            <div className="mb-6">
              <div className="mb-4 flex items-center gap-2">
                <Star className="h-4 w-4 text-amber-400" />
                <h2 className="font-medium text-gray-900 dark:text-white">
                  Favorites
                </h2>
                <button
                  onClick={() => setShowAllFolders((s) => !s)}
                  className="ml-auto flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                  title={showAllFolders ? "Show favorites only" : "Show all folders"}
                >
                  {showAllFolders ? (
                    <>
                      <EyeOff className="h-3.5 w-3.5" />
                      Show favorites only
                    </>
                  ) : (
                    <>
                      <Eye className="h-3.5 w-3.5" />
                      Show all folders
                    </>
                  )}
                </button>
              </div>
              <div className="rounded-xl border border-amber-200 bg-white p-3 dark:border-amber-800/50 dark:bg-gray-900">
                {agent.isOnline ? (
                  <FavoriteFolderTree
                    agentId={agent.id}
                    favoritePaths={favoritePaths}
                    activeSessionPaths={activeSessionPaths}
                    atSessionLimit={atSessionLimit}
                    onLaunch={handleLaunchSession}
                    preferences={preferences}
                    onToggleFavorite={handleToggleFavorite}
                    onSetColor={handleSetColor}
                  />
                ) : (
                  <p className="py-8 text-center text-sm text-gray-400">
                    Agent must be online to browse folders.
                  </p>
                )}
              </div>
            </div>
          )}

          {/* All Folders Section */}
          {showAllFolders && (
            <>
              <div className="mb-4 flex items-center gap-2">
                <FolderTreeIcon className="h-4 w-4 text-nest-500" />
                <h2 className="font-medium text-gray-900 dark:text-white">
                  {favoritePaths.length > 0 ? "All Folders" : "Browse Folders"}
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
                    preferences={preferences}
                    onToggleFavorite={handleToggleFavorite}
                    onSetColor={handleSetColor}
                  />
                ) : (
                  <p className="py-8 text-center text-sm text-gray-400">
                    Agent must be online to browse folders.
                  </p>
                )}
              </div>
            </>
          )}

          <div className="mt-3 rounded-lg border border-gray-200 bg-gray-50 px-4 py-3 dark:border-gray-800 dark:bg-gray-800/50">
            <div className="flex items-center gap-2 text-xs font-medium text-gray-500 dark:text-gray-400">
              <Terminal className="h-3.5 w-3.5" />
              Add more folders to this agent
            </div>
            <code className="mt-1.5 block text-xs text-gray-600 dark:text-gray-300">
              claudenest-agent add-path /path/to/directory
            </code>
          </div>
        </div>
      </div>
    </div>
  );
}
