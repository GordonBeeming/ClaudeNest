import { useState, useEffect, useMemo } from "react";
import { X, Copy, Check, ChevronDown, ChevronRight, Download, Terminal, Clock } from "lucide-react";
import { clsx } from "clsx";
import { generatePairingToken, getLocalBuildAvailability, type LocalBuildAvailability } from "../api";

interface InstallAgentModalProps {
  open: boolean;
  onClose: () => void;
}

type Platform = "windows" | "macos" | "linux";

interface PlatformConfig {
  label: string;
  icon: string;
  downloads: { label: string; rid: string; filename: string }[];
  installNotes: string[];
}

const PLATFORMS: Record<Platform, PlatformConfig> = {
  windows: {
    label: "Windows",
    icon: "🪟",
    downloads: [
      { label: "x64", rid: "win-x64", filename: "claudenest-agent-win-x64.exe" },
    ],
    installNotes: [
      "Run the install command in an elevated (Administrator) PowerShell",
      "The agent will be installed as a Windows Service",
    ],
  },
  macos: {
    label: "macOS",
    icon: "🍎",
    downloads: [
      { label: "Apple Silicon (ARM)", rid: "osx-arm64", filename: "claudenest-agent-osx-arm64" },
      { label: "Intel (x64)", rid: "osx-x64", filename: "claudenest-agent-osx-x64" },
    ],
    installNotes: [
      "After downloading, make the binary executable: chmod +x ./claudenest-agent-osx-*",
      "The agent can be configured as a launchd service for auto-start",
    ],
  },
  linux: {
    label: "Linux",
    icon: "🐧",
    downloads: [
      { label: "x64", rid: "linux-x64", filename: "claudenest-agent-linux-x64" },
    ],
    installNotes: [
      "After downloading, make the binary executable: chmod +x ./claudenest-agent-linux-x64",
      "The agent can be configured as a systemd service for auto-start",
    ],
  },
};

const PLATFORM_ORDER: Platform[] = ["windows", "macos", "linux"];

const RELEASE_BASE = "https://github.com/GordonBeeming/ClaudeNest/releases/latest/download";

function detectPlatform(): Platform {
  const ua = navigator.userAgent.toLowerCase();
  if (ua.includes("win")) return "windows";
  if (ua.includes("mac")) return "macos";
  return "linux";
}

function getLocalBuildRidsForPlatform(platform: Platform, availableRids: string[]): { label: string; rid: string; filename: string }[] {
  const ridMap: Record<Platform, { label: string; rid: string }[]> = {
    windows: [{ label: "x64", rid: "win-x64" }],
    macos: [
      { label: "Apple Silicon (ARM)", rid: "osx-arm64" },
      { label: "Intel (x64)", rid: "osx-x64" },
    ],
    linux: [{ label: "x64", rid: "linux-x64" }],
  };

  return ridMap[platform]
    .filter((r) => availableRids.includes(r.rid))
    .map((r) => ({
      ...r,
      filename: r.rid.startsWith("win-") ? `claudenest-agent-${r.rid}.exe` : `claudenest-agent-${r.rid}`,
    }));
}

function installCommand(filename: string, token: string, backendUrl: string, options?: { localBuild?: boolean; devWorkspacePath?: string | null }): string {
  const isWindows = filename.endsWith(".exe");
  const isMac = filename.includes("osx-");
  const pathArg = options?.devWorkspacePath ? ` --path "${options.devWorkspacePath}"` : "";
  const run = `./${filename} install --token ${token} --backend ${backendUrl}${pathArg}`;
  if (isWindows) return run;
  const quarantine = isMac ? `xattr -d com.apple.quarantine ./${filename} 2>/dev/null; ` : "";
  return `${quarantine}chmod +x ./${filename} && ${run}`;
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
          {/* Downloads */}
          <div>
            <h4 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
              1. Download the agent
            </h4>
            <div className="flex flex-wrap gap-2">
              {config.downloads.map((dl) => (
                <a
                  key={dl.rid}
                  href={`${RELEASE_BASE}/${dl.filename}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:bg-gray-800 dark:text-gray-200 dark:hover:bg-gray-700"
                >
                  <Download className="h-4 w-4" />
                  {dl.label}
                </a>
              ))}
            </div>
          </div>

          {/* Install command */}
          <div>
            <h4 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
              2. Run the install command
            </h4>
            {config.downloads.map((dl) => (
              <div key={dl.rid} className="mb-2">
                {config.downloads.length > 1 && (
                  <p className="mb-1 text-xs text-gray-500 dark:text-gray-400">{dl.label}:</p>
                )}
                <div className="relative rounded-lg bg-gray-900 p-3 dark:bg-gray-800">
                  <div className="flex items-start gap-2">
                    <Terminal className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" />
                    <code className="mr-8 break-all text-sm text-green-400">
                      {installCommand(dl.filename, token, backendUrl)}
                    </code>
                    <div className="absolute right-2 top-2">
                      <CopyButton text={installCommand(dl.filename, token, backendUrl)} />
                    </div>
                  </div>
                </div>
              </div>
            ))}
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
  const [localBuild, setLocalBuild] = useState<LocalBuildAvailability | null>(null);
  const detectedPlatform = useMemo(() => detectPlatform(), []);
  const [expandedPlatforms, setExpandedPlatforms] = useState<Set<Platform>>(
    new Set([detectedPlatform]),
  );

  const backendUrl = window.location.origin;

  // Reset state and fetch data when modal opens
  useEffect(() => {
    if (!open) return;

    let cancelled = false;

    // Fetch token and local build availability — setState calls in .then/.catch/.finally
    // are in async callbacks, not synchronous in the effect body.
    Promise.resolve()
      .then(() => {
        if (cancelled) return;
        setLoading(true);
        setError(null);
        setToken(null);
        setLocalBuild(null);
        setExpandedPlatforms(new Set([detectedPlatform]));
        return Promise.all([generatePairingToken(), getLocalBuildAvailability()]);
      })
      .then((results) => {
        if (cancelled || !results) return;
        const [tokenResult, buildAvailability] = results;
        setToken(tokenResult.token);
        setLocalBuild(buildAvailability);
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
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={handleClose}
    >
      <div
        className="mx-4 max-h-[85vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-xl dark:bg-gray-900"
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
          Download and install the ClaudeNest agent on the machine you want to connect.
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

              {localBuild?.available && localBuild.source === "local-build" && (() => {
                const localDownloads = getLocalBuildRidsForPlatform(detectedPlatform, localBuild.rids);
                if (localDownloads.length === 0) return null;
                return (
                  <div className="rounded-lg border border-green-300 bg-green-50/50 dark:border-green-700 dark:bg-green-950/30">
                    <div className="flex items-center gap-3 px-4 py-3">
                      <span className="text-lg">🔨</span>
                      <span className="font-medium text-gray-900 dark:text-white">Local Build</span>
                      <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700 dark:bg-green-900/50 dark:text-green-300">
                        Built from local source
                      </span>
                    </div>
                    <div className="space-y-4 border-t border-green-200 px-4 py-4 dark:border-green-700">
                      <div>
                        <h4 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                          1. Download the agent
                        </h4>
                        <div className="flex flex-wrap gap-2">
                          {localDownloads.map((dl) => (
                            <a
                              key={dl.rid}
                              href={`/api/agent-download/${dl.rid}`}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="inline-flex items-center gap-2 rounded-lg border border-green-200 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-green-50 dark:border-green-600 dark:bg-gray-800 dark:text-gray-200 dark:hover:bg-green-900/30"
                            >
                              <Download className="h-4 w-4" />
                              {dl.label}
                            </a>
                          ))}
                        </div>
                        <p className="mt-1.5 text-xs text-gray-500 dark:text-gray-400">
                          Build may take a moment — the download starts after compilation completes.
                        </p>
                      </div>

                      <div>
                        <h4 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                          2. Run the install command
                        </h4>
                        {localDownloads.map((dl) => (
                          <div key={dl.rid} className="mb-2">
                            {localDownloads.length > 1 && (
                              <p className="mb-1 text-xs text-gray-500 dark:text-gray-400">{dl.label}:</p>
                            )}
                            <div className="relative rounded-lg bg-gray-900 p-3 dark:bg-gray-800">
                              <div className="flex items-start gap-2">
                                <Terminal className="mt-0.5 h-4 w-4 shrink-0 text-gray-500" />
                                <code className="mr-8 break-all text-sm text-green-400">
                                  {installCommand(dl.filename, token, backendUrl, { localBuild: true, devWorkspacePath: localBuild?.devWorkspacePath })}
                                </code>
                                <div className="absolute right-2 top-2">
                                  <CopyButton text={installCommand(dl.filename, token, backendUrl, { localBuild: true, devWorkspacePath: localBuild?.devWorkspacePath })} />
                                </div>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                );
              })()}

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
