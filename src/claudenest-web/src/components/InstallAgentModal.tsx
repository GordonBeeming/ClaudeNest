import { useState, useEffect, useMemo } from "react";
import { X, Copy, Check, ChevronDown, ChevronRight, Terminal, Clock } from "lucide-react";
import { clsx } from "clsx";
import { generatePairingToken } from "../api";

interface InstallAgentModalProps {
  open: boolean;
  onClose: () => void;
}

type Platform = "windows" | "macos" | "linux";

interface PlatformConfig {
  label: string;
  icon: string;
  installCommand: (backendUrl: string, token: string) => string;
  installNotes: string[];
}

const PLATFORMS: Record<Platform, PlatformConfig> = {
  windows: {
    label: "Windows",
    icon: "🪟",
    installCommand: (backendUrl, token) =>
      `$env:CLAUDENEST_TOKEN='${token}'; irm '${backendUrl}/install.ps1' | iex`,
    installNotes: [
      "Run this command in an elevated (Administrator) PowerShell",
      "You will be prompted for your Windows password to register the agent as a Windows Service",
    ],
  },
  macos: {
    label: "macOS",
    icon: "🍎",
    installCommand: (backendUrl, token) =>
      `curl -sSL '${backendUrl}/install.sh' | CLAUDENEST_TOKEN='${token}' bash`,
    installNotes: [
      "The agent will be installed as a macOS LaunchAgent for auto-start",
      "The binary will be placed in ~/.claudenest/bin/",
    ],
  },
  linux: {
    label: "Linux",
    icon: "🐧",
    installCommand: (backendUrl, token) =>
      `curl -sSL '${backendUrl}/install.sh' | CLAUDENEST_TOKEN='${token}' bash`,
    installNotes: [
      "The agent will be installed as a systemd user service for auto-start",
      "The binary will be placed in ~/.claudenest/bin/",
    ],
  },
};

const PLATFORM_ORDER: Platform[] = ["windows", "macos", "linux"];

function detectPlatform(): Platform {
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes("win")) return "windows";
  if (ua.includes("mac")) return "macos";
  return "linux";
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button
      onClick={handleCopy}
      className="shrink-0 rounded-md p-1.5 text-gray-400 hover:bg-gray-700 hover:text-white"
      title="Copy to clipboard"
    >
      {copied ? <Check className="h-4 w-4 text-green-400" /> : <Copy className="h-4 w-4" />}
    </button>
  );
}

function PlatformSection({
  config,
  token,
  backendUrl,
  expanded,
  onToggle,
  isDetected,
}: {
  config: PlatformConfig;
  token: string;
  backendUrl: string;
  expanded: boolean;
  onToggle: () => void;
  isDetected: boolean;
}) {
  const command = config.installCommand(backendUrl, token);

  return (
    <div
      className={clsx(
        "rounded-lg border",
        isDetected
          ? "border-nest-300 bg-nest-50/50 dark:border-nest-700 dark:bg-nest-950/30"
          : "border-gray-200 dark:border-gray-700",
      )}
    >
      <button
        onClick={onToggle}
        className="flex w-full items-center gap-3 px-4 py-3 text-left"
      >
        {expanded ? (
          <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
        ) : (
          <ChevronRight className="h-4 w-4 shrink-0 text-gray-400" />
        )}
        <span className="text-lg">{config.icon}</span>
        <span className="font-medium text-gray-900 dark:text-white">{config.label}</span>
        {isDetected && (
          <span className="rounded-full bg-nest-100 px-2 py-0.5 text-xs font-medium text-nest-700 dark:bg-nest-900/50 dark:text-nest-300">
            Detected
          </span>
        )}
      </button>

      {expanded && (
        <div className="space-y-4 border-t border-gray-200 px-4 py-4 dark:border-gray-700">
          {/* Install command */}
          <div>
            <div className="relative rounded-lg bg-gray-900 p-3 dark:bg-gray-800">
              <div className="flex items-start gap-2">
                <Terminal className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" />
                <code className="mr-8 break-all text-sm text-green-400">
                  {command}
                </code>
                <div className="absolute right-2 top-2">
                  <CopyButton text={command} />
                </div>
              </div>
            </div>
          </div>

          {/* Notes */}
          {config.installNotes.length > 0 && (
            <div>
              <h4 className="mb-1.5 text-sm font-medium text-gray-700 dark:text-gray-300">
                Notes
              </h4>
              <ul className="space-y-1 text-xs text-gray-500 dark:text-gray-400">
                {config.installNotes.map((note, i) => (
                  <li key={i} className="flex gap-1.5">
                    <span className="shrink-0">•</span>
                    <span>{note}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export function InstallAgentModal({ open, onClose }: InstallAgentModalProps) {
  const [token, setToken] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const detectedPlatform = useMemo(() => detectPlatform(), []);
  const [expandedPlatforms, setExpandedPlatforms] = useState<Set<Platform>>(
    new Set([detectedPlatform]),
  );

  const backendUrl = window.location.origin;

  // Reset state and fetch token when modal opens
  useEffect(() => {
    if (!open) return;

    let cancelled = false;

    Promise.resolve()
      .then(() => {
        if (cancelled) return;
        setLoading(true);
        setError(null);
        setToken(null);
        setExpandedPlatforms(new Set([detectedPlatform]));
        return generatePairingToken();
      })
      .then((result) => {
        if (cancelled || !result) return;
        setToken(result.token);
      })
      .catch((e) => {
        if (!cancelled) setError(e instanceof Error ? e.message : "Failed to generate token");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, detectedPlatform]);

  const togglePlatform = (p: Platform) => {
    setExpandedPlatforms((prev) => {
      const next = new Set(prev);
      if (next.has(p)) {
        next.delete(p);
      } else {
        next.add(p);
      }
      return next;
    });
  };

  const handleClose = () => {
    setToken(null);
    setError(null);
    onClose();
  };

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 backdrop-blur-sm sm:items-center"
      onClick={handleClose}
    >
      <div
        className="w-full max-h-[85vh] max-w-2xl overflow-y-auto rounded-t-2xl bg-white p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-xl dark:bg-gray-900 sm:mx-4 sm:rounded-2xl sm:p-6"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Install Agent</h2>
          <button
            onClick={handleClose}
            className="rounded-lg p-1 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Install the ClaudeNest agent on the machine you want to connect.
        </p>

        <div className="mt-4">
          {loading && (
            <div className="flex items-center justify-center py-8">
              <div className="h-5 w-5 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" />
              <span className="ml-2 text-sm text-gray-500">Generating pairing token...</span>
            </div>
          )}

          {error && (
            <div className="rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-950/50 dark:text-red-400">
              {error}
            </div>
          )}

          {token && (
            <div className="space-y-3">
              <div className="flex items-start gap-2 rounded-lg bg-amber-50 p-3 dark:bg-amber-950/30">
                <Clock className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
                <div className="text-xs text-amber-700 dark:text-amber-400">
                  <p className="font-medium">This token expires in 10 minutes</p>
                  <p className="mt-0.5">It can only be used once. Close and reopen this dialog to generate a new token.</p>
                </div>
              </div>

              {PLATFORM_ORDER.map((p) => (
                <PlatformSection
                  key={p}
                  config={PLATFORMS[p]}
                  token={token}
                  backendUrl={backendUrl}
                  expanded={expandedPlatforms.has(p)}
                  onToggle={() => togglePlatform(p)}
                  isDetected={p === detectedPlatform}
                />
              ))}

              <p className="text-center text-xs text-gray-500 dark:text-gray-400">
                Once installed, your agent will appear in the dashboard within seconds.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
