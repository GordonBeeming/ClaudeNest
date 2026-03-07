import { useState, useEffect, useCallback } from "react";
import { Plus, Bird, RefreshCw } from "lucide-react";
import { getAgents } from "../api";
import type { Agent } from "../types";
import { AgentCard } from "../components/AgentCard";
import { InstallAgentModal } from "../components/InstallAgentModal";
import { useSignalRContext } from "../contexts/SignalRContext";
import { useUserContext } from "../contexts/UserContext";
import { useSEO } from "../hooks/useSEO";

export function Dashboard() {
  useSEO({ title: "Dashboard", noindex: true });
  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showPairing, setShowPairing] = useState(false);
  const { onAgentStatusChanged, onAgentRemoved } = useSignalRContext();
  const { user } = useUserContext();
  const maxAgents = user?.account?.maxAgents ?? 0;
  const planName = user?.account?.planName ?? "";
  const atAgentLimit = agents.length >= maxAgents && maxAgents > 0;

  const fetchAgents = useCallback(async () => {
    try {
      const data = await getAgents();
      setAgents(data);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load agents");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAgents();
  }, [fetchAgents]);

  // Listen for real-time agent status changes
  useEffect(() => {
    return onAgentStatusChanged((agentId, isOnline) => {
      setAgents((prev) => {
        const existed = prev.some((a) => a.id === agentId);
        if (isOnline && !existed) {
          // New agent just came online — close the install modal and refresh
          setShowPairing(false);
          fetchAgents();
        }
        return prev.map((a) => (a.id === agentId ? { ...a, isOnline } : a));
      });
    });
  }, [onAgentStatusChanged, fetchAgents]);

  // Listen for agent removal (handles multi-tab scenario)
  useEffect(() => {
    return onAgentRemoved((agentId) => {
      setAgents((prev) => prev.filter((a) => a.id !== agentId));
    });
  }, [onAgentRemoved]);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">
            Your Agents
          </h1>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            {agents.length} of {maxAgents} agents ({planName})
          </p>
        </div>
        <button
          onClick={() => setShowPairing(true)}
          disabled={atAgentLimit}
          className="flex items-center gap-2 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600 disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Plus className="h-4 w-4" />
          Add Agent
        </button>
      </div>

      {error && (
        <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-950/50 dark:text-red-400">
          {error}
        </div>
      )}

      {agents.length === 0 && !error ? (
        <div className="mt-16 flex flex-col items-center gap-4 text-center">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-nest-50 dark:bg-nest-950/50">
            <Bird className="h-8 w-8 text-nest-500" />
          </div>
          <div>
            <h2 className="text-lg font-medium text-gray-900 dark:text-white">
              No agents connected
            </h2>
            <p className="mt-1 max-w-sm text-sm text-gray-500 dark:text-gray-400">
              Install the ClaudeNest agent on a machine to start launching
              Claude Code remote sessions.
            </p>
          </div>
          <button
            onClick={() => setShowPairing(true)}
            className="flex items-center gap-2 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600"
          >
            <Plus className="h-4 w-4" />
            Add your first agent
          </button>
        </div>
      ) : (
        <div className="mt-6 space-y-3">
          {agents.map((agent) => (
            <AgentCard key={agent.id} agent={agent} />
          ))}
        </div>
      )}

      <InstallAgentModal
        open={showPairing}
        onClose={() => {
          setShowPairing(false);
          fetchAgents();
        }}
      />
    </div>
  );
}
