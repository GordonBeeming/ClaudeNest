import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { User, CreditCard, BarChart3, RefreshCw, AlertTriangle, ArrowRight, Server, Settings, Check, Receipt } from "lucide-react";
import { clsx } from "clsx";
import { useUserContext } from "../contexts/UserContext";
import { getAccount, getAgents, updatePermissionMode, createBillingPortalSession, getLedger } from "../api";
import type { AccountInfo, Agent, LedgerEntry, PaginatedResult } from "../types";
import { format } from "date-fns";
import { AgentCredentialManager } from "../components/AgentCredentialManager";

function StatusBadge({ status }: { status: string }) {
  const styles: Record<string, string> = {
    Active: "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
    Trialing: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
    PastDue: "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
    Cancelling: "bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400",
    Cancelled: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400",
    None: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400",
  };

  return (
    <span className={clsx("rounded-full px-2.5 py-0.5 text-xs font-medium", styles[status] || styles.None)}>
      {status}
    </span>
  );
}

function UsageBar({ used, max, label }: { used: number; max: number; label: string }) {
  const pct = max > 0 ? Math.min((used / max) * 100, 100) : 0;
  const isHigh = pct >= 80;

  return (
    <div>
      <div className="flex items-center justify-between text-sm">
        <span className="text-gray-600 dark:text-gray-400">{label}</span>
        <span className="font-medium text-gray-900 dark:text-white">
          {used} / {max}
        </span>
      </div>
      <div className="mt-1.5 h-2 rounded-full bg-gray-100 dark:bg-gray-800">
        <div
          className={clsx(
            "h-2 rounded-full transition-all",
            isHigh ? "bg-amber-500" : "bg-nest-500",
          )}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

const PERMISSION_MODES = [
  {
    value: "default",
    label: "Default",
    description: "Claude will ask for permission before running commands or editing files. Recommended for most users.",
  },
  {
    value: "acceptEdits",
    label: "Accept Edits",
    description: "File edits are auto-approved. Claude will still ask before running terminal commands.",
  },
  {
    value: "plan",
    label: "Plan Mode",
    description: "Claude must create and get approval for a plan before making any changes. Best for complex tasks where you want to review the approach first.",
  },
  {
    value: "dontAsk",
    label: "Don't Ask",
    description: "Most actions are auto-approved. Claude will still ask before potentially dangerous commands like deleting files or force pushing.",
  },
  {
    value: "bypassPermissions",
    label: "Bypass Permissions",
    description: "All permission checks are skipped entirely. Only recommended for sandboxed environments with no internet access.",
  },
];

export function AccountPage() {
  const { user } = useUserContext();
  const [account, setAccount] = useState<AccountInfo | null>(null);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [loading, setLoading] = useState(true);
  const [permissionSaved, setPermissionSaved] = useState(false);
  const [billingRedirecting, setBillingRedirecting] = useState(false);
  const [ledger, setLedger] = useState<PaginatedResult<LedgerEntry> | null>(null);
  const [ledgerPage, setLedgerPage] = useState(1);

  useEffect(() => {
    Promise.all([
      getAccount().then(setAccount).catch(() => {}),
      getAgents().then(setAgents).catch(() => {}),
      getLedger(1).then(setLedger).catch(() => {}),
    ]).finally(() => setLoading(false));
  }, []);

  const loadMoreLedger = async () => {
    const nextPage = ledgerPage + 1;
    try {
      const result = await getLedger(nextPage);
      setLedger((prev) =>
        prev ? { ...result, items: [...prev.items, ...result.items] } : result,
      );
      setLedgerPage(nextPage);
    } catch {
      // ignore
    }
  };

  const handleManageBilling = async () => {
    setBillingRedirecting(true);
    try {
      const { url } = await createBillingPortalSession();
      window.location.href = url;
    } catch {
      setBillingRedirecting(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Account</h1>

      {/* User info */}
      <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
        <div className="flex items-center gap-3 mb-4">
          <User className="h-5 w-5 text-gray-400" />
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Profile</h2>
        </div>
        <dl className="space-y-3 text-sm">
          {user?.displayName && (
            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Name</dt>
              <dd className="font-medium text-gray-900 dark:text-white">{user.displayName}</dd>
            </div>
          )}
          {user?.email && (
            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Email</dt>
              <dd className="font-medium text-gray-900 dark:text-white">{user.email}</dd>
            </div>
          )}
        </dl>
      </section>

      {/* Plan info */}
      {account && (
        <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-3">
              <CreditCard className="h-5 w-5 text-gray-400" />
              <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Plan</h2>
            </div>
            <StatusBadge status={account.cancelAtPeriodEnd ? "Cancelling" : account.subscriptionStatus} />
          </div>

          <dl className="space-y-3 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Current plan</dt>
              <dd className="font-medium text-gray-900 dark:text-white">
                {account.planName || "None"}
              </dd>
            </div>

            {account.activeCoupon && (
              <div className="flex justify-between">
                <dt className="text-gray-500 dark:text-gray-400">Coupon</dt>
                <dd className="font-medium text-green-600 dark:text-green-400">
                  Free until {format(new Date(account.activeCoupon.freeUntil), "MMM d, yyyy")} via coupon {account.activeCoupon.code}
                </dd>
              </div>
            )}

            {account.subscriptionStatus === "PastDue" && (
              <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 dark:border-red-800 dark:bg-red-950/30">
                <p className="text-sm text-red-700 dark:text-red-400">
                  Your payment is past due. Please{" "}
                  <button
                    onClick={handleManageBilling}
                    className="font-medium underline hover:no-underline"
                  >
                    update your payment method
                  </button>.
                </p>
              </div>
            )}

            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Max agents</dt>
              <dd className="font-medium text-gray-900 dark:text-white">{account.maxAgents}</dd>
            </div>

            <div className="flex justify-between">
              <dt className="text-gray-500 dark:text-gray-400">Max concurrent sessions</dt>
              <dd className="font-medium text-gray-900 dark:text-white">{account.maxSessions}</dd>
            </div>

            {account.cancelAtPeriodEnd && account.currentPeriodEnd && (
              <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 dark:border-amber-800 dark:bg-amber-950/30">
                <p className="text-sm text-amber-700 dark:text-amber-400">
                  Your subscription is set to cancel. You will have access until{" "}
                  <span className="font-medium">{format(new Date(account.currentPeriodEnd), "MMM d, yyyy 'at' h:mm a")}</span>.{" "}
                  <button
                    onClick={handleManageBilling}
                    className="font-medium underline hover:no-underline"
                  >
                    Resume subscription
                  </button>
                </p>
              </div>
            )}

            {account.currentPeriodEnd && !account.cancelAtPeriodEnd && (
              <div className="flex justify-between">
                <dt className="text-gray-500 dark:text-gray-400">Next billing date</dt>
                <dd className="font-medium text-gray-900 dark:text-white">
                  {format(new Date(account.currentPeriodEnd), "MMM d, yyyy 'at' h:mm a")}
                </dd>
              </div>
            )}
          </dl>

          <div className="mt-4 flex items-center gap-4">
            {account.hasStripeSubscription ? (
              <>
                <button
                  onClick={handleManageBilling}
                  disabled={billingRedirecting}
                  className="inline-flex items-center gap-1.5 text-sm font-medium text-nest-600 hover:text-nest-700 disabled:opacity-50 dark:text-nest-400 dark:hover:text-nest-300"
                >
                  <CreditCard className="h-3.5 w-3.5" />
                  {billingRedirecting ? "Redirecting..." : "Manage Billing"}
                </button>
                <span className="text-sm text-gray-400 dark:text-gray-500">
                  To change plans, cancel your current subscription first or{" "}
                  <a href="mailto:support@claudenest.com" className="text-nest-600 hover:underline dark:text-nest-400">
                    contact support
                  </a>
                </span>
              </>
            ) : (
              <Link
                to="/plans"
                className="inline-flex items-center gap-1.5 text-sm font-medium text-nest-600 hover:text-nest-700 dark:text-nest-400 dark:hover:text-nest-300"
              >
                Choose a plan
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            )}
          </div>
        </section>
      )}

      {/* Session Settings */}
      {account && (
        <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-center gap-3 mb-4">
            <Settings className="h-5 w-5 text-gray-400" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Session Settings</h2>
          </div>

          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1.5">
            Permission mode
          </label>
          <div className="relative">
            <select
              value={account.permissionMode}
              onChange={async (e) => {
                const mode = e.target.value;
                setAccount({ ...account, permissionMode: mode });
                try {
                  await updatePermissionMode(mode);
                  setPermissionSaved(true);
                  setTimeout(() => setPermissionSaved(false), 2000);
                } catch {
                  // Revert on error
                  setAccount((prev) => prev ? { ...prev, permissionMode: account.permissionMode } : prev);
                }
              }}
              className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
            >
              {PERMISSION_MODES.map((m) => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
            {permissionSaved && (
              <span className="absolute right-10 top-1/2 -translate-y-1/2 flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                <Check className="h-3.5 w-3.5" />
                Saved
              </span>
            )}
          </div>

          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
            {PERMISSION_MODES.find((m) => m.value === account.permissionMode)?.description}
          </p>
        </section>
      )}

      {/* Usage */}
      {account && (
        <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-center gap-3 mb-4">
            <BarChart3 className="h-5 w-5 text-gray-400" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Usage</h2>
          </div>
          <div className="space-y-4">
            <UsageBar used={account.agentCount} max={account.maxAgents} label="Agents" />
            <UsageBar used={account.activeSessionCount} max={account.maxSessions} label="Active sessions" />
          </div>
        </section>
      )}

      {/* Billing History */}
      <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
        <div className="flex items-center gap-3 mb-4">
          <Receipt className="h-5 w-5 text-gray-400" />
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Billing History</h2>
        </div>
        {!ledger || ledger.items.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No billing history yet</p>
        ) : (
          <>
            <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-800">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-gray-200 bg-gray-50 dark:border-gray-800 dark:bg-gray-900">
                    <th className="px-4 py-2.5 font-medium text-gray-500 dark:text-gray-400">Date</th>
                    <th className="px-4 py-2.5 font-medium text-gray-500 dark:text-gray-400">Description</th>
                    <th className="px-4 py-2.5 text-right font-medium text-gray-500 dark:text-gray-400">Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.items.map((entry) => (
                    <tr key={entry.id} className="border-b border-gray-100 last:border-0 dark:border-gray-800">
                      <td className="px-4 py-2.5 text-gray-600 dark:text-gray-300">
                        {format(new Date(entry.createdAt), "MMM d, yyyy")}
                      </td>
                      <td className="px-4 py-2.5 text-gray-900 dark:text-white">
                        {entry.description}
                      </td>
                      <td
                        className={clsx(
                          "px-4 py-2.5 text-right font-medium",
                          entry.amountCents >= 0
                            ? "text-green-600 dark:text-green-400"
                            : "text-red-600 dark:text-red-400",
                        )}
                      >
                        {entry.amountCents < 0 ? "-" : ""}${Math.abs(entry.amountCents / 100).toFixed(2)} {entry.currency.toUpperCase()}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {ledger.items.length < ledger.totalCount && (
              <button
                onClick={loadMoreLedger}
                className="mt-3 text-sm font-medium text-nest-600 hover:text-nest-700 dark:text-nest-400 dark:hover:text-nest-300"
              >
                Load more
              </button>
            )}
          </>
        )}
      </section>

      {/* Agents & Credentials */}
      {agents.length > 0 && (
        <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-center gap-3 mb-4">
            <Server className="h-5 w-5 text-gray-400" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Agents & Credentials</h2>
          </div>
          <p className="mb-4 text-sm text-gray-500 dark:text-gray-400">
            Manage agent secrets and revoke access. Rotating a secret will immediately disconnect the agent until it is reconfigured.
          </p>
          <div className="space-y-2">
            {agents.map((agent) => (
              <AgentCredentialManager key={agent.id} agent={agent} />
            ))}
          </div>
        </section>
      )}

      {/* Danger zone placeholder */}
      <section className="rounded-xl border border-red-200 bg-red-50/50 p-6 dark:border-red-900/50 dark:bg-red-950/20">
        <div className="flex items-center gap-3 mb-2">
          <AlertTriangle className="h-5 w-5 text-red-500" />
          <h2 className="text-lg font-semibold text-red-700 dark:text-red-400">Danger zone</h2>
        </div>
        <p className="text-sm text-red-600/80 dark:text-red-400/80">
          Account deletion will be available in a future update.
        </p>
      </section>
    </div>
  );
}
