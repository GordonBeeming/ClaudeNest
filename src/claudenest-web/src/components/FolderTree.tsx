import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Folder,
  FolderOpen,
  ChevronRight,
  ChevronDown,
  Play,
  Loader2,
  Ban,
} from "lucide-react";
import { clsx } from "clsx";
import { useSignalRContext } from "../contexts/SignalRContext";
import type { DirectoryListingResult } from "../types";

/** Check if a path overlaps with any active session path (same, parent, or child). */
function isPathBlocked(path: string, activeSessionPaths: string[]): boolean {
  const normalized = path.replace(/\/+$/, "");
  return activeSessionPaths.some((sp) => {
    const normalizedSp = sp.replace(/\/+$/, "");
    return (
      normalized === normalizedSp ||
      normalized.startsWith(normalizedSp + "/") ||
      normalizedSp.startsWith(normalized + "/")
    );
  });
}

interface FolderNodeProps {
  agentId: string;
  path: string;
  name: string;
  depth: number;
  onLaunch: (path: string) => void;
  activeSessionPaths: string[];
  atSessionLimit?: boolean;
}

function FolderNode({ agentId, path, name, depth, onLaunch, activeSessionPaths, atSessionLimit }: FolderNodeProps) {
  const { requestDirectoryListing, onDirectoryListingResult } =
    useSignalRContext();
  const [expanded, setExpanded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [children, setChildren] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [hovering, setHovering] = useState(false);

  const blocked = useMemo(
    () => isPathBlocked(path, activeSessionPaths),
    [path, activeSessionPaths],
  );

  const handleToggle = useCallback(() => {
    if (!expanded && children === null) {
      setLoading(true);
      setError(null);
      requestDirectoryListing(agentId, path);
    }
    setExpanded((e) => !e);
  }, [expanded, children, agentId, path, requestDirectoryListing]);

  useEffect(() => {
    return onDirectoryListingResult((result: DirectoryListingResult) => {
      if (result.path === path) {
        setLoading(false);
        if (result.error) {
          setError(result.error);
        } else {
          setChildren(result.directories);
        }
      }
    });
  }, [path, onDirectoryListingResult]);

  return (
    <div>
      <div
        className={clsx(
          "group flex cursor-pointer items-center gap-1 rounded-lg px-2 py-1.5 hover:bg-gray-100 dark:hover:bg-gray-800",
          expanded && "bg-gray-50 dark:bg-gray-800/50",
        )}
        style={{ paddingLeft: `${depth * 20 + 8}px` }}
        onClick={handleToggle}
        onMouseEnter={() => setHovering(true)}
        onMouseLeave={() => setHovering(false)}
      >
        <span className="shrink-0 text-gray-400">
          {loading ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : expanded ? (
            <ChevronDown className="h-4 w-4" />
          ) : (
            <ChevronRight className="h-4 w-4" />
          )}
        </span>

        {expanded ? (
          <FolderOpen className="h-4 w-4 shrink-0 text-nest-500" />
        ) : (
          <Folder className="h-4 w-4 shrink-0 text-gray-400 dark:text-gray-500" />
        )}

        <span className="min-w-0 truncate text-sm">{name}</span>

        {depth > 0 && hovering && !blocked && !atSessionLimit && (
          <button
            onClick={(e) => {
              e.stopPropagation();
              onLaunch(path);
            }}
            className="ml-auto flex shrink-0 items-center gap-1 rounded-md bg-nest-500 px-2 py-0.5 text-xs font-medium text-white hover:bg-nest-600"
          >
            <Play className="h-3 w-3" />
            Launch
          </button>
        )}

        {depth > 0 && hovering && !blocked && atSessionLimit && (
          <span className="ml-auto flex shrink-0 items-center gap-1 rounded-md bg-amber-50 px-2 py-0.5 text-xs text-amber-600 dark:bg-amber-950/50 dark:text-amber-400">
            <Ban className="h-3 w-3" />
            Session limit reached
          </span>
        )}

        {depth > 0 && hovering && blocked && (
          <span className="ml-auto flex shrink-0 items-center gap-1 rounded-md bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
            <Ban className="h-3 w-3" />
            Session active
          </span>
        )}
      </div>

      {error && (
        <p
          className="px-2 py-1 text-xs text-red-500"
          style={{ paddingLeft: `${(depth + 1) * 20 + 8}px` }}
        >
          {error}
        </p>
      )}

      {expanded && children && (
        <div>
          {children.length === 0 ? (
            <p
              className="px-2 py-1 text-xs text-gray-400 italic"
              style={{ paddingLeft: `${(depth + 1) * 20 + 8}px` }}
            >
              No subdirectories
            </p>
          ) : (
            children.map((child) => (
              <FolderNode
                key={child}
                agentId={agentId}
                path={`${path}/${child}`}
                name={child}
                depth={depth + 1}
                onLaunch={onLaunch}
                activeSessionPaths={activeSessionPaths}
                atSessionLimit={atSessionLimit}
              />
            ))
          )}
        </div>
      )}
    </div>
  );
}

interface FolderTreeProps {
  agentId: string;
  allowedPaths: string[];
  activeSessionPaths: string[];
  atSessionLimit?: boolean;
  onLaunch: (path: string) => void;
}

export function FolderTree({ agentId, allowedPaths, activeSessionPaths, atSessionLimit, onLaunch }: FolderTreeProps) {
  if (allowedPaths.length === 0) {
    return (
      <div className="py-8 text-center text-sm text-gray-400">
        No allowed paths configured on this agent.
      </div>
    );
  }

  return (
    <div className="space-y-1">
      {allowedPaths.map((rootPath) => (
        <FolderNode
          key={rootPath}
          agentId={agentId}
          path={rootPath}
          name={rootPath.replace(/\/+$/, "").split("/").pop() || rootPath}
          depth={0}
          onLaunch={onLaunch}
          activeSessionPaths={activeSessionPaths}
          atSessionLimit={atSessionLimit}
        />
      ))}
    </div>
  );
}
