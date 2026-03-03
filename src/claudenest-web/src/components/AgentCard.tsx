import { Link } from "react-router-dom";
import { Monitor, Apple, Server, ChevronRight } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import type { Agent } from "../types";
import { OnlineBadge } from "./StatusBadge";

function OsIcon({ os }: { os: string | null }) {
  const lower = os?.toLowerCase() ?? "";
  if (lower.includes("mac") || lower.includes("osx") || lower.includes("darwin"))
    return <Apple className="h-5 w-5" />;
  if (lower.includes("win"))
    return <Monitor className="h-5 w-5" />;
  return <Server className="h-5 w-5" />;
}

export function AgentCard({ agent }: { agent: Agent }) {
  return (
    <Link
      to={`/agents/${agent.id}`}
      className="group flex items-center gap-4 rounded-xl border border-gray-200 bg-white p-4 hover:border-nest-300 hover:shadow-sm dark:border-gray-800 dark:bg-gray-900 dark:hover:border-nest-700"
    >
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400">
        <OsIcon os={agent.os} />
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <h3 className="truncate font-medium text-gray-900 dark:text-white">
            {agent.name || agent.hostname || "Unnamed Agent"}
          </h3>
          <OnlineBadge isOnline={agent.isOnline} />
          {agent.version && (
            <span className="rounded bg-gray-100 px-1.5 py-0.5 text-xs font-mono text-gray-500 dark:bg-gray-800 dark:text-gray-400">
              v{agent.version}
            </span>
          )}
        </div>
        <p className="mt-0.5 truncate text-sm text-gray-500 dark:text-gray-400">
          {agent.hostname ?? "Unknown host"}
          {agent.os ? ` \u00b7 ${agent.os}` : ""}
          {agent.lastSeenAt
            ? ` \u00b7 Last seen ${formatDistanceToNow(new Date(agent.lastSeenAt), { addSuffix: true })}`
            : ""}
        </p>
      </div>

      <ChevronRight className="h-5 w-5 shrink-0 text-gray-300 group-hover:text-nest-500 dark:text-gray-600" />
    </Link>
  );
}
