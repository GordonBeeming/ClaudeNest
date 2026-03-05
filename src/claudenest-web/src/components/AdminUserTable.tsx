import { useState, useEffect, useRef } from "react";
import { RefreshCw, ChevronDown, Ban, Gift, ShieldCheck, ShieldOff, ArrowUpCircle, RotateCcw } from "lucide-react";
import { clsx } from "clsx";
import { format } from "date-fns";
import {
  getAdminUsers,
  adminCancelSubscription, adminGiveCoupon, adminToggleAdmin, adminOverridePlan, adminRevertPlan,
} from "../api";
import type { AdminUserInfo, CouponInfo, PlanInfo } from "../types";
import { formatDiscountDescription } from "../types";
import { Select } from "./Select";

export function StatusBadge({ status, cancelAtPeriodEnd }: { status: string; cancelAtPeriodEnd: boolean }) {
  if (cancelAtPeriodEnd && status === "Active") {
    return (
      <span className="rounded-full bg-yellow-100 px-2.5 py-0.5 text-xs font-medium text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400">
        Cancelling
      </span>
    );
  }

  const styles: Record<string, string> = {
    Active: "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400",
    PastDue: "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400",
    Cancelled: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400",
    Trialing: "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400",
    None: "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-500",
  };

  return (
    <span className={clsx("rounded-full px-2.5 py-0.5 text-xs font-medium", styles[status] || styles.None)}>
      {status}
    </span>
  );
}

export function ActionsDropdown({
  user,
  onCancelSubscription,
  onGiveCoupon,
  onToggleAdmin,
  onOverridePlan,
  onRevertPlan,
  actionLoading,
}: {
  user: AdminUserInfo;
  onCancelSubscription: (u: AdminUserInfo) => void;
  onGiveCoupon: (userId: string) => void;
  onToggleAdmin: (u: AdminUserInfo) => void;
  onOverridePlan: (userId: string) => void;
  onRevertPlan: (u: AdminUserInfo) => void;
  actionLoading: string | null;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handleClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        disabled={actionLoading === user.id}
        className="inline-flex items-center gap-1 rounded-lg border border-gray-200 bg-white px-2.5 py-1 text-xs font-medium text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700 transition-colors disabled:opacity-50"
      >
        {actionLoading === user.id ? (
          <RefreshCw className="h-3 w-3 animate-spin" />
        ) : (
          <>
            Actions
            <ChevronDown className="h-3 w-3" />
          </>
        )}
      </button>
      {open && (
        <div className="absolute right-0 z-50 mt-1 w-48 rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-900">
          {user.hasStripeSubscription && (
            <button
              onClick={() => { setOpen(false); onCancelSubscription(user); }}
              className="flex w-full items-center gap-2 px-3 py-2 text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
            >
              <Ban className="h-3.5 w-3.5" />
              Cancel subscription
            </button>
          )}
          <button
            onClick={() => { setOpen(false); onGiveCoupon(user.id); }}
            className="flex w-full items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            <Gift className="h-3.5 w-3.5" />
            Give coupon
          </button>
          {user.companyDealDomain && !user.hasStripeSubscription && (
            <button
              onClick={() => { setOpen(false); onOverridePlan(user.id); }}
              className="flex w-full items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
            >
              <ArrowUpCircle className="h-3.5 w-3.5" />
              Override plan
            </button>
          )}
          {user.companyDealDomain && user.companyDealPlanName && user.planName !== user.companyDealPlanName && (
            <button
              onClick={() => { setOpen(false); onRevertPlan(user); }}
              className="flex w-full items-center gap-2 px-3 py-2 text-sm text-yellow-600 hover:bg-yellow-50 dark:text-yellow-400 dark:hover:bg-yellow-900/20"
            >
              <RotateCcw className="h-3.5 w-3.5" />
              Revert to deal plan
            </button>
          )}
          <button
            onClick={() => { setOpen(false); onToggleAdmin(user); }}
            className="flex w-full items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            {user.isAdmin ? (
              <>
                <ShieldOff className="h-3.5 w-3.5" />
                Remove admin
              </>
            ) : (
              <>
                <ShieldCheck className="h-3.5 w-3.5" />
                Make admin
              </>
            )}
          </button>
        </div>
      )}
    </div>
  );
}

interface AdminUserTableProps {
  domain?: string;
  plans: PlanInfo[];
  coupons: CouponInfo[];
  compact?: boolean;
}

export function AdminUserTable({ domain, plans, coupons, compact }: AdminUserTableProps) {
  const [users, setUsers] = useState<AdminUserInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const [giveCouponUserId, setGiveCouponUserId] = useState<string | null>(null);
  const [selectedCouponId, setSelectedCouponId] = useState("");
  const [overridePlanUserId, setOverridePlanUserId] = useState<string | null>(null);
  const [selectedOverridePlanId, setSelectedOverridePlanId] = useState("");

  useEffect(() => {
    getAdminUsers({ domain, pageSize: 500 })
      .then((result) => setUsers(result.items))
      .catch(() => setError("Failed to load users"))
      .finally(() => setLoading(false));
  }, [domain]);

  function updateUser(userId: string, updated: AdminUserInfo) {
    setUsers((prev) => prev.map((u) => (u.id === userId ? updated : u)));
  }

  async function handleCancelSubscription(user: AdminUserInfo) {
    if (!confirm(`Cancel subscription for ${user.email}?`)) return;
    setActionLoading(user.id);
    try {
      updateUser(user.id, await adminCancelSubscription(user.id));
    } catch { /* ignore */ } finally { setActionLoading(null); }
  }

  async function handleToggleAdmin(user: AdminUserInfo) {
    if (!confirm(`${user.isAdmin ? "Remove admin access from" : "Grant admin access to"} ${user.email}?`)) return;
    setActionLoading(user.id);
    try {
      updateUser(user.id, await adminToggleAdmin(user.id));
    } catch { /* ignore */ } finally { setActionLoading(null); }
  }

  async function handleGiveCoupon(userId: string) {
    if (!selectedCouponId) return;
    setActionLoading(userId);
    try {
      updateUser(userId, await adminGiveCoupon(userId, selectedCouponId));
      setGiveCouponUserId(null);
      setSelectedCouponId("");
    } catch { /* ignore */ } finally { setActionLoading(null); }
  }

  async function handleOverridePlan(userId: string) {
    if (!selectedOverridePlanId) return;
    setActionLoading(userId);
    try {
      updateUser(userId, await adminOverridePlan(userId, selectedOverridePlanId));
      setOverridePlanUserId(null);
      setSelectedOverridePlanId("");
    } catch { /* ignore */ } finally { setActionLoading(null); }
  }

  async function handleRevertPlan(user: AdminUserInfo) {
    if (!confirm(`Revert ${user.email} back to the company deal plan (${user.companyDealPlanName})?`)) return;
    setActionLoading(user.id);
    try {
      updateUser(user.id, await adminRevertPlan(user.id));
    } catch { /* ignore */ } finally { setActionLoading(null); }
  }

  const activeCoupons = coupons.filter((c) => c.isActive);
  const py = compact ? "py-2" : "py-3";

  if (loading) {
    return (
      <div className="flex items-center justify-center py-6">
        <RefreshCw className="h-4 w-4 animate-spin text-gray-400" />
      </div>
    );
  }

  if (error) {
    return <p className="px-4 py-3 text-sm text-red-600 dark:text-red-400">{error}</p>;
  }

  if (users.length === 0) {
    return <p className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400">No users found</p>;
  }

  return (
    <>
      {/* Give Coupon Modal */}
      {giveCouponUserId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="mx-4 w-full max-w-md rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-gray-700 dark:bg-gray-900">
            <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Give Coupon</h3>
            <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
              Select a coupon to apply to{" "}
              <strong>{users.find((u) => u.id === giveCouponUserId)?.email}</strong>
            </p>
            <Select
              value={selectedCouponId}
              onChange={setSelectedCouponId}
              placeholder="Select a coupon..."
              className="mb-4"
              options={activeCoupons.map((c) => ({
                value: c.id,
                label: `${c.code} — ${c.planName}`,
                description: formatDiscountDescription(c.discountType, c.freeMonths, c.percentOff, c.amountOffCents, c.freeDays, c.durationMonths),
              }))}
            />
            <div className="flex items-center gap-3">
              <button
                onClick={() => handleGiveCoupon(giveCouponUserId)}
                disabled={!selectedCouponId || actionLoading === giveCouponUserId}
                className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600 transition-colors disabled:opacity-50"
              >
                {actionLoading === giveCouponUserId && <RefreshCw className="h-3.5 w-3.5 animate-spin" />}
                Apply Coupon
              </button>
              <button
                onClick={() => { setGiveCouponUserId(null); setSelectedCouponId(""); }}
                className="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Override Plan Modal */}
      {overridePlanUserId && (() => {
        const targetUser = users.find((u) => u.id === overridePlanUserId);
        const dealPlan = targetUser?.companyDealPlanId
          ? plans.find((p) => p.id === targetUser.companyDealPlanId)
          : null;
        const eligiblePlans = dealPlan
          ? plans.filter((p) => p.sortOrder > dealPlan.sortOrder && p.id !== targetUser?.companyDealPlanId)
          : [];

        return (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
            <div className="mx-4 w-full max-w-md rounded-xl border border-gray-200 bg-white p-6 shadow-xl dark:border-gray-700 dark:bg-gray-900">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Override Plan</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
                Override the plan for <strong>{targetUser?.email}</strong>
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-400 mb-4">
                Current deal plan: <strong>{targetUser?.companyDealPlanName}</strong>
                {targetUser?.planName !== targetUser?.companyDealPlanName && (
                  <> (currently on: <strong>{targetUser?.planName}</strong>)</>
                )}
              </p>
              {eligiblePlans.length === 0 ? (
                <p className="mb-4 text-sm text-yellow-600 dark:text-yellow-400">
                  No higher plans available to override with.
                </p>
              ) : (
                <Select
                  value={selectedOverridePlanId}
                  onChange={setSelectedOverridePlanId}
                  placeholder="Select a plan..."
                  className="mb-4"
                  options={eligiblePlans.map((p) => ({ value: p.id, label: p.name }))}
                />
              )}
              <div className="flex items-center gap-3">
                {eligiblePlans.length > 0 && (
                  <button
                    onClick={() => handleOverridePlan(overridePlanUserId)}
                    disabled={!selectedOverridePlanId || actionLoading === overridePlanUserId}
                    className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600 transition-colors disabled:opacity-50"
                  >
                    {actionLoading === overridePlanUserId && <RefreshCw className="h-3.5 w-3.5 animate-spin" />}
                    Override Plan
                  </button>
                )}
                <button
                  onClick={() => { setOverridePlanUserId(null); setSelectedOverridePlanId(""); }}
                  className="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        );
      })()}

      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-100 dark:border-gray-800">
            <th className={clsx("px-4 text-left font-medium text-gray-500 dark:text-gray-400", py)}>User</th>
            <th className={clsx("hidden sm:table-cell px-4 text-left font-medium text-gray-500 dark:text-gray-400", py)}>Plan</th>
            <th className={clsx("hidden sm:table-cell px-4 text-left font-medium text-gray-500 dark:text-gray-400", py)}>Status</th>
            <th className={clsx("hidden md:table-cell px-4 text-left font-medium text-gray-500 dark:text-gray-400", py)}>Coupon</th>
            <th className={clsx("hidden md:table-cell px-4 text-left font-medium text-gray-500 dark:text-gray-400", py)}>Joined</th>
            <th className={clsx("px-4 text-right font-medium text-gray-500 dark:text-gray-400", py)}><span className="sr-only">Actions</span></th>
          </tr>
        </thead>
        <tbody>
          {users.map((user) => (
            <tr key={user.id} className="border-b border-gray-50 last:border-0 dark:border-gray-800/50">
              <td className={clsx("px-4", py)}>
                <div className="font-medium text-gray-900 dark:text-white">
                  {user.displayName || user.email}
                  {user.isAdmin && (
                    <span className="ml-1.5 inline-block rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-900/30 dark:text-amber-400">
                      ADMIN
                    </span>
                  )}
                </div>
                {user.displayName && (
                  <div className="text-xs text-gray-500 dark:text-gray-400">{user.email}</div>
                )}
                {!domain && user.companyDealDomain && (
                  <div className="text-xs text-gray-400 dark:text-gray-500">{user.companyDealDomain}</div>
                )}
                <div className="mt-1 flex flex-wrap items-center gap-2 sm:hidden">
                  <span className="text-xs text-gray-500 dark:text-gray-400">{user.planName || "No plan"}</span>
                  <StatusBadge status={user.subscriptionStatus} cancelAtPeriodEnd={user.cancelAtPeriodEnd} />
                </div>
              </td>
              <td className={clsx("hidden sm:table-cell px-4 text-gray-700 dark:text-gray-300", py)}>{user.planName || "None"}</td>
              <td className={clsx("hidden sm:table-cell px-4", py)}>
                <StatusBadge status={user.subscriptionStatus} cancelAtPeriodEnd={user.cancelAtPeriodEnd} />
              </td>
              <td className={clsx("hidden md:table-cell px-4 text-gray-700 dark:text-gray-300", py)}>
                {user.activeCoupon ? (
                  <div>
                    <span className="font-mono text-xs">{user.activeCoupon.code}</span>
                    <div className="text-xs text-gray-500 dark:text-gray-400">
                      until {format(new Date(user.activeCoupon.freeUntil), "dd MMM yyyy")}
                    </div>
                  </div>
                ) : (
                  "-"
                )}
              </td>
              <td className={clsx("hidden md:table-cell px-4 text-gray-700 dark:text-gray-300", py)}>
                {format(new Date(user.createdAt), "dd MMM yyyy")}
              </td>
              <td className={clsx("px-4 text-right", py)}>
                <ActionsDropdown
                  user={user}
                  onCancelSubscription={handleCancelSubscription}
                  onGiveCoupon={setGiveCouponUserId}
                  onToggleAdmin={handleToggleAdmin}
                  onOverridePlan={setOverridePlanUserId}
                  onRevertPlan={handleRevertPlan}
                  actionLoading={actionLoading}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  );
}
