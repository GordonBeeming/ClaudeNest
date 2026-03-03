import { useState, useEffect, useCallback, useMemo, useRef } from "react";
import {
  Folder,
  FolderOpen,
  ChevronRight,
  ChevronDown,
  Play,
  Loader2,
  Ban,
  Star,
  StarOff,
  Palette,
  X,
} from "lucide-react";
import { clsx } from "clsx";
import { useSignalRContext } from "../contexts/SignalRContext";
import type { DirectoryListingResult, FolderPreference } from "../types";

const PRESET_COLORS = [
  "#ef4444", // red
  "#f97316", // orange
  "#eab308", // yellow
  "#22c55e", // green
  "#06b6d4", // cyan
  "#3b82f6", // blue
  "#8b5cf6", // violet
  "#ec4899", // pink
  "#6b7280", // gray
  "#84cc16", // lime
];

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

function ColorPicker({
  currentColor,
  onSelect,
  onClose,
}: {
  currentColor: string | null;
  onSelect: (color: string | null) => void;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        onClose();
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [onClose]);

  return (
    <div
      ref={ref}
      className="absolute right-0 top-full z-50 mt-1 rounded-lg border border-gray-200 bg-white p-2 shadow-lg dark:border-gray-700 dark:bg-gray-800"
      onClick={(e) => e.stopPropagation()}
    >
      <div className="grid grid-cols-5 gap-1.5">
        {PRESET_COLORS.map((color) => (
          <button
            key={color}
            onClick={() => {
              onSelect(color);
              onClose();
            }}
            className={clsx(
              "h-6 w-6 rounded-full border-2 transition-transform hover:scale-110",
              currentColor === color
                ? "border-gray-900 dark:border-white"
                : "border-transparent",
            )}
            style={{ backgroundColor: color }}
          />
        ))}
      </div>
      {currentColor && (
        <button
          onClick={() => {
            onSelect(null);
            onClose();
          }}
          className="mt-1.5 flex w-full items-center justify-center gap-1 rounded-md px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-700"
        >
          <X className="h-3 w-3" />
          Reset
        </button>
      )}
    </div>
  );
}

interface FolderNodeProps {
  agentId: string;
  path: string;
  name: string;
  depth: number;
  onLaunch: (path: string) => void;
  activeSessionPaths: string[];
  atSessionLimit?: boolean;
  preferences?: Map<string, FolderPreference>;
  onToggleFavorite?: (path: string, currentPref: FolderPreference | undefined) => void;
  onSetColor?: (path: string, color: string | null, currentPref: FolderPreference | undefined) => void;
  autoExpand?: boolean;
}

function FolderNode({
  agentId,
  path,
  name,
  depth,
  onLaunch,
  activeSessionPaths,
  atSessionLimit,
  preferences,
  onToggleFavorite,
  onSetColor,
  autoExpand,
}: FolderNodeProps) {
  const { requestDirectoryListing, onDirectoryListingResult } =
    useSignalRContext();
  const [expanded, setExpanded] = useState(!!autoExpand);
  const [loading, setLoading] = useState(!!autoExpand);
  const [children, setChildren] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [hovering, setHovering] = useState(false);
  const [showColorPicker, setShowColorPicker] = useState(false);
  const autoExpandTriggered = useRef(false);

  const pref = preferences?.get(path);
  const folderColor = pref?.color ?? null;

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

  // Auto-expand: trigger directory listing on mount
  useEffect(() => {
    if (autoExpand && !autoExpandTriggered.current) {
      autoExpandTriggered.current = true;
      requestDirectoryListing(agentId, path);
    }
  }, [autoExpand, agentId, path, requestDirectoryListing]);

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
        onMouseLeave={() => {
          setHovering(false);
          setShowColorPicker(false);
        }}
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
          <FolderOpen
            className="h-4 w-4 shrink-0"
            style={folderColor ? { color: folderColor } : undefined}
            {...(!folderColor ? { className: "h-4 w-4 shrink-0 text-nest-500" } : {})}
          />
        ) : (
          <Folder
            className="h-4 w-4 shrink-0"
            style={folderColor ? { color: folderColor } : undefined}
            {...(!folderColor ? { className: "h-4 w-4 shrink-0 text-gray-400 dark:text-gray-500" } : {})}
          />
        )}

        <span className="min-w-0 truncate text-sm">{name}</span>

        {/* Hover action buttons */}
        {hovering && (
          <div className="ml-auto flex shrink-0 items-center gap-1">
            {onToggleFavorite && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onToggleFavorite(path, pref);
                }}
                className="rounded-md p-0.5 text-gray-400 hover:text-amber-500 dark:text-gray-500 dark:hover:text-amber-400"
                title={pref?.isFavorite ? "Remove from favorites" : "Add to favorites"}
              >
                {pref?.isFavorite ? (
                  <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" />
                ) : (
                  <StarOff className="h-3.5 w-3.5" />
                )}
              </button>
            )}

            {onSetColor && (
              <div className="relative">
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    setShowColorPicker((s) => !s);
                  }}
                  className="rounded-md p-0.5 text-gray-400 hover:text-nest-500 dark:text-gray-500 dark:hover:text-nest-400"
                  title="Set folder color"
                >
                  <Palette className="h-3.5 w-3.5" />
                </button>
                {showColorPicker && (
                  <ColorPicker
                    currentColor={folderColor}
                    onSelect={(color) => onSetColor(path, color, pref)}
                    onClose={() => setShowColorPicker(false)}
                  />
                )}
              </div>
            )}

            {depth > 0 && !blocked && !atSessionLimit && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onLaunch(path);
                }}
                className="flex items-center gap-1 rounded-md bg-nest-500 px-2 py-0.5 text-xs font-medium text-white hover:bg-nest-600"
              >
                <Play className="h-3 w-3" />
                Launch
              </button>
            )}

            {depth > 0 && !blocked && atSessionLimit && (
              <span className="flex items-center gap-1 rounded-md bg-amber-50 px-2 py-0.5 text-xs text-amber-600 dark:bg-amber-950/50 dark:text-amber-400">
                <Ban className="h-3 w-3" />
                Session limit reached
              </span>
            )}

            {depth > 0 && blocked && (
              <span className="flex items-center gap-1 rounded-md bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
                <Ban className="h-3 w-3" />
                Session active
              </span>
            )}
          </div>
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
                preferences={preferences}
                onToggleFavorite={onToggleFavorite}
                onSetColor={onSetColor}
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
  preferences?: Map<string, FolderPreference>;
  onToggleFavorite?: (path: string, currentPref: FolderPreference | undefined) => void;
  onSetColor?: (path: string, color: string | null, currentPref: FolderPreference | undefined) => void;
}

export function FolderTree({
  agentId,
  allowedPaths,
  activeSessionPaths,
  atSessionLimit,
  onLaunch,
  preferences,
  onToggleFavorite,
  onSetColor,
}: FolderTreeProps) {
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
          preferences={preferences}
          onToggleFavorite={onToggleFavorite}
          onSetColor={onSetColor}
        />
      ))}
    </div>
  );
}

interface FavoriteFolderTreeProps {
  agentId: string;
  favoritePaths: string[];
  activeSessionPaths: string[];
  atSessionLimit?: boolean;
  onLaunch: (path: string) => void;
  preferences: Map<string, FolderPreference>;
  onToggleFavorite: (path: string, currentPref: FolderPreference | undefined) => void;
  onSetColor: (path: string, color: string | null, currentPref: FolderPreference | undefined) => void;
}

export function FavoriteFolderTree({
  agentId,
  favoritePaths,
  activeSessionPaths,
  atSessionLimit,
  onLaunch,
  preferences,
  onToggleFavorite,
  onSetColor,
}: FavoriteFolderTreeProps) {
  if (favoritePaths.length === 0) {
    return null;
  }

  return (
    <div className="space-y-1">
      {favoritePaths.map((favPath) => {
        // Show last two path segments for context (e.g., "github/ClaudeNest")
        const parts = favPath.replace(/\/+$/, "").split("/");
        const displayName = parts.length >= 2
          ? parts.slice(-2).join("/")
          : parts.pop() || favPath;

        return (
          <FolderNode
            key={favPath}
            agentId={agentId}
            path={favPath}
            name={displayName}
            depth={0}
            onLaunch={onLaunch}
            activeSessionPaths={activeSessionPaths}
            atSessionLimit={atSessionLimit}
            preferences={preferences}
            onToggleFavorite={onToggleFavorite}
            onSetColor={onSetColor}
            autoExpand
          />
        );
      })}
    </div>
  );
}
