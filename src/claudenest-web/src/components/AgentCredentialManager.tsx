import { useState, useEffect } from "react";
import { Key, RotateCw, Trash2, Copy, Check, ChevronDown, ChevronRight, AlertTriangle, ShieldCheck, ShieldOff } from "lucide-react";
import { clsx } from "clsx";
import { formatDistanceToNow } from "date-fns";
import { getAgentCredentials, rotateAgentSecret, revokeAgentCredential } from "../api";
import type { Agent, AgentCredentialInfo } from "../types";

interface AgentCredentialManagerProps {
  agent: Agent;
}

function CredentialStatusBadge({ isActive }: { isActive: boolean }) {
  return (
    <span
      className={clsx(
        "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium",
        isActive
          ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
          : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
      )}
    >
      {isActive ? <ShieldCheck className="h-3 w-3" /> : <ShieldOff className="h-3 w-3" />}
      {isActive ? "Active" : "Revoked"}
    </span>
  );
}

function OneTimeSecret({ secret }: { secret: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(secret);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 dark:border-amber-800 dark:bg-amber-950/30">
      <div className="flex items-start gap-2">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-500" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-amber-800 dark:text-amber-300">
            Save this secret now — it won't be shown again
          </p>
          <div className="mt-2 flex items-center gap-2">
            <code className="break-all rounded bg-amber-100 px-2 py-1 text-xs text-amber-900 dark:bg-amber-900/50 dark:text-amber-200">
              {secret}
            </code>
            <button
              onClick={handleCopy}
              className="shrink-0 rounded-md p-1.5 text-amber-600 hover:bg-amber-100 dark:text-amber-400 dark:hover:bg-amber-900/50"
              title="Copy secret"
            >
              {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
            </button>
          </div>
          <p className="mt-1.5 text-xs text-amber-600 dark:text-amber-400">
            Update your agent's credentials.json with this new secret.
          </p>
        </div>
      </div>
    </div>
  );
}

export function AgentCredentialManager({ agent }: AgentCredentialManagerProps) {
  const [expanded, setExpanded] = useState(false);
  const [credentials, setCredentials] = useState<AgentCredentialInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [newSecret, setNewSecret] = useState<string | null>(null);
  const [confirmRotate, setConfirmRotate] = useState(false);
  const [confirmRevokeId, setConfirmRevokeId] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  useEffect(() => {
    if (!expanded) return;
    setLoading(true);
    getAgentCredentials(agent.id)
      .then(setCredentials)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [expanded, agent.id]);

  const handleRotate = async () => {
    setActionLoading(true);
    try {
      const result = await rotateAgentSecret(agent.id);
      setNewSecret(result.secret);
      setConfirmRotate(false);
      // Refresh credentials
      const updated = await getAgentCredentials(agent.id);
      setCredentials(updated);
    } catch {
      // Could show an error toast
    } finally {
      setActionLoading(false);
    }
  };

  const handleRevoke = async (credentialId: string) => {
    setActionLoading(true);
    try {
      await revokeAgentCredential(agent.id, credentialId);
      setConfirmRevokeId(null);
      // Refresh credentials
      const updated = await getAgentCredentials(agent.id);
      setCredentials(updated);
    } catch {
      // Could show an error toast
    } finally {
      setActionLoading(false);
    }
  };

  const activeCount = credentials.filter((c) => c.isActive).length;

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700">
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex w-full items-center justify-between px-4 py-3"
      >
        <div className="flex items-center gap-3">
          {expanded ? (
            <ChevronDown className="h-4 w-4 text-gray-400" />
          ) : (
            <ChevronRight className="h-4 w-4 text-gray-400" />
          )}
          <div className="text-left">
            <div className="flex items-center gap-2">
              <span className="font-medium text-gray-900 dark:text-white">
                {agent.name || agent.hostname || "Unnamed Agent"}
              </span>
              <span
                className={clsx(
                  "h-2 w-2 rounded-full",
                  agent.isOnline ? "bg-green-500" : "bg-gray-300 dark:bg-gray-600",
                )}
              />
            </div>
            {agent.hostname && agent.name && (
              <p className="text-xs text-gray-500 dark:text-gray-400">{agent.hostname}</p>
            )}
          </div>
        </div>
        <span className="text-xs text-gray-500 dark:text-gray-400">
          {agent.os}
        </span>
      </button>

      {expanded && (
        <div className="border-t border-gray-200 px-4 py-4 dark:border-gray-700">
          {loading ? (
            <div className="flex items-center justify-center py-4">
              <div className="h-4 w-4 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" />
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <h4 className="flex items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300">
                  <Key className="h-4 w-4" />
                  Credentials ({activeCount} active)
                </h4>
                {confirmRotate ? (
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-amber-600 dark:text-amber-400">
                      This will revoke all current credentials
                    </span>
                    <button
                      onClick={handleRotate}
                      disabled={actionLoading}
                      className="rounded-md bg-amber-500 px-2.5 py-1 text-xs font-medium text-white hover:bg-amber-600 disabled:opacity-50"
                    >
                      Confirm
                    </button>
                    <button
                      onClick={() => setConfirmRotate(false)}
                      className="rounded-md px-2.5 py-1 text-xs text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-800"
                    >
                      Cancel
                    </button>
                  </div>
                ) : (
                  <button
                    onClick={() => {
                      setConfirmRotate(true);
                      setNewSecret(null);
                    }}
                    className="inline-flex items-center gap-1.5 rounded-md bg-nest-50 px-2.5 py-1 text-xs font-medium text-nest-700 hover:bg-nest-100 dark:bg-nest-950/50 dark:text-nest-300 dark:hover:bg-nest-900/50"
                  >
                    <RotateCw className="h-3 w-3" />
                    Rotate Secret
                  </button>
                )}
              </div>

              {newSecret && <OneTimeSecret secret={newSecret} />}

              <div className="space-y-2">
                {credentials.map((cred) => (
                  <div
                    key={cred.id}
                    className={clsx(
                      "flex items-center justify-between rounded-md px-3 py-2 text-sm",
                      cred.isActive
                        ? "bg-gray-50 dark:bg-gray-800/50"
                        : "bg-gray-50/50 opacity-60 dark:bg-gray-800/25",
                    )}
                  >
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <CredentialStatusBadge isActive={cred.isActive} />
                        <span className="text-xs text-gray-500 dark:text-gray-400">
                          Issued {formatDistanceToNow(new Date(cred.issuedAt), { addSuffix: true })}
                        </span>
                      </div>
                      {cred.lastUsedAt && (
                        <p className="mt-0.5 text-xs text-gray-400 dark:text-gray-500">
                          Last used {formatDistanceToNow(new Date(cred.lastUsedAt), { addSuffix: true })}
                        </p>
                      )}
                      {cred.revokedAt && (
                        <p className="mt-0.5 text-xs text-gray-400 dark:text-gray-500">
                          Revoked {formatDistanceToNow(new Date(cred.revokedAt), { addSuffix: true })}
                        </p>
                      )}
                    </div>
                    {cred.isActive && (
                      <>
                        {confirmRevokeId === cred.id ? (
                          <div className="flex items-center gap-1.5">
                            <button
                              onClick={() => handleRevoke(cred.id)}
                              disabled={actionLoading}
                              className="rounded-md bg-red-500 px-2 py-1 text-xs font-medium text-white hover:bg-red-600 disabled:opacity-50"
                            >
                              Confirm
                            </button>
                            <button
                              onClick={() => setConfirmRevokeId(null)}
                              className="rounded-md px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700"
                            >
                              Cancel
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={() => setConfirmRevokeId(cred.id)}
                            className="rounded-md p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-950/50"
                            title="Revoke credential"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        )}
                      </>
                    )}
                  </div>
                ))}

                {credentials.length === 0 && (
                  <p className="py-2 text-center text-xs text-gray-400 dark:text-gray-500">
                    No credentials found
                  </p>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
