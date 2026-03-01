import { clsx } from "clsx";
import type { SessionState } from "../types";

interface Props {
  state: SessionState;
  className?: string;
}

const config: Record<SessionState, { label: string; dot: string; bg: string }> =
  {
    Requested: {
      label: "Requested",
      dot: "bg-amber-400",
      bg: "bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300",
    },
    Starting: {
      label: "Starting",
      dot: "bg-amber-400 animate-pulse",
      bg: "bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-300",
    },
    Running: {
      label: "Running",
      dot: "bg-green-500",
      bg: "bg-green-50 text-green-700 dark:bg-green-950/50 dark:text-green-300",
    },
    Stopping: {
      label: "Stopping",
      dot: "bg-orange-400 animate-pulse",
      bg: "bg-orange-50 text-orange-700 dark:bg-orange-950/50 dark:text-orange-300",
    },
    Stopped: {
      label: "Stopped",
      dot: "bg-gray-400",
      bg: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400",
    },
    Crashed: {
      label: "Crashed",
      dot: "bg-red-500",
      bg: "bg-red-50 text-red-700 dark:bg-red-950/50 dark:text-red-300",
    },
  };

export function StatusBadge({ state, className }: Props) {
  const c = config[state];
  return (
    <span
      className={clsx(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium",
        c.bg,
        className,
      )}
    >
      <span className={clsx("h-1.5 w-1.5 rounded-full", c.dot)} />
      {c.label}
    </span>
  );
}

export function OnlineBadge({ isOnline }: { isOnline: boolean }) {
  return (
    <span
      className={clsx(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-medium",
        isOnline
          ? "bg-green-50 text-green-700 dark:bg-green-950/50 dark:text-green-300"
          : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
      )}
    >
      <span
        className={clsx(
          "h-1.5 w-1.5 rounded-full",
          isOnline ? "bg-green-500" : "bg-gray-400",
        )}
      />
      {isOnline ? "Online" : "Offline"}
    </span>
  );
}
