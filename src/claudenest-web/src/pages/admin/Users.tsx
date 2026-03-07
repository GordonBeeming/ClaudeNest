import { useState, useEffect } from "react";
import { UsersRound, RefreshCw, XCircle, ChevronLeft, ChevronRight } from "lucide-react";
import { format } from "date-fns";
import {
  getAdminUsers,
  getAdminCompanyDeals,
  getAdminCoupons,
  getPlans,
  adminCancelSubscription,
  adminGiveCoupon,
  adminToggleAdmin,
  adminOverridePlan,
  adminRevertPlan,
} from "../../api";
import type { AdminUserInfo, CompanyDeal, CouponInfo, PlanInfo } from "../../types";
import { formatDiscountDescription } from "../../types";
import { Select } from "../../components/Select";
import { StatusBadge, ActionsDropdown } from "../../components/AdminUserTable";
import { ScrollableTable } from "../../components/ScrollableTable";
import { useSignalRContext } from "../../contexts/SignalRContext";
import { useSEO } from "../../hooks/useSEO";

const PAGE_SIZE = 25;

export function Users() {
  useSEO({ title: "Admin - Users", noindex: true });
  const { adminAgentSummary } = useSignalRContext();
  const [users, setUsers] = useState<AdminUserInfo[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [deals, setDeals] = useState<CompanyDeal[]>([]);
  const [coupons, setCoupons] = useState<CouponInfo[]>([]);
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [domainFilter, setDomainFilter] = useState("");
  const [couponFilter, setCouponFilter] = useState<"" | "true" | "false">("");

  const [giveCouponUserId, setGiveCouponUserId] = useState<string | null>(null);
  const [selectedCouponId, setSelectedCouponId] = useState("");
  const [overridePlanUserId, setOverridePlanUserId] = useState<string | null>(null);
  const [selectedOverridePlanId, setSelectedOverridePlanId] = useState("");
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  function loadUsers(p: number) {
    setLoading(true);
    setError(null);
    const filters: { domain?: string; hasCoupon?: boolean; page: number; pageSize: number } = {
      page: p,
      pageSize: PAGE_SIZE,
    };
    if (domainFilter) filters.domain = domainFilter;
    if (couponFilter === "true") filters.hasCoupon = true;
    if (couponFilter === "false") filters.hasCoupon = false;
    getAdminUsers(filters)
      .then((result) => {
        setUsers(result.items);
        setTotalCount(result.totalCount);
        setPage(result.page);
      })
      .catch(() => setError("Failed to load users"))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    Promise.all([
      getAdminUsers({ page: 1, pageSize: PAGE_SIZE }).then((result) => {
        setUsers(result.items);
        setTotalCount(result.totalCount);
        setPage(result.page);
      }),
      getAdminCompanyDeals().then(setDeals),
      getAdminCoupons().then(setCoupons),
      getPlans().then(setPlans),
    ])
      .catch(() => setError("Failed to load data"))
      .finally(() => setLoading(false));
  }, []);

  // Reset to page 1 when filters change
  useEffect(() => {
    loadUsers(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [domainFilter, couponFilter]);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  async function handleCancelSubscription(user: AdminUserInfo) {
    if (!confirm(`Cancel subscription for ${user.email}? This will cancel their Stripe subscription immediately.`))
      return;

    setActionLoading(user.id);
    setError(null);
    setSuccess(null);

    try {
      const updated = await adminCancelSubscription(user.id);
      setUsers((prev) => prev.map((u) => (u.id === user.id ? updated : u)));
      setSuccess(`Subscription cancelled for ${user.email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to cancel subscription");
    } finally {
      setActionLoading(null);
    }
  }

  async function handleToggleAdmin(user: AdminUserInfo) {
    const action = user.isAdmin ? "remove admin from" : "make admin";
    if (!confirm(`${user.isAdmin ? "Remove admin access from" : "Grant admin access to"} ${user.email}?`))
      return;

    setActionLoading(user.id);
    setError(null);
    setSuccess(null);

    try {
      const updated = await adminToggleAdmin(user.id);
      setUsers((prev) => prev.map((u) => (u.id === user.id ? updated : u)));
      setSuccess(`${user.email} is ${updated.isAdmin ? "now" : "no longer"} an admin`);
    } catch (err) {
      setError(err instanceof Error ? err.message : `Failed to ${action}`);
    } finally {
      setActionLoading(null);
    }
  }

  async function handleGiveCoupon(userId: string) {
    if (!selectedCouponId) return;

    setActionLoading(userId);
    setError(null);
    setSuccess(null);

    try {
      const updated = await adminGiveCoupon(userId, selectedCouponId);
      setUsers((prev) => prev.map((u) => (u.id === userId ? updated : u)));
      setSuccess(`Coupon applied successfully`);
      setGiveCouponUserId(null);
      setSelectedCouponId("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to give coupon");
    } finally {
      setActionLoading(null);
    }
  }

  async function handleOverridePlan(userId: string) {
    if (!selectedOverridePlanId) return;

    setActionLoading(userId);
    setError(null);
    setSuccess(null);

    try {
      const updated = await adminOverridePlan(userId, selectedOverridePlanId);
      setUsers((prev) => prev.map((u) => (u.id === userId ? updated : u)));
      setSuccess(`Plan overridden successfully`);
      setOverridePlanUserId(null);
      setSelectedOverridePlanId("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to override plan");
    } finally {
      setActionLoading(null);
    }
  }

  async function handleRevertPlan(user: AdminUserInfo) {
    if (!confirm(`Revert ${user.email} back to the company deal plan (${user.companyDealPlanName})?`))
      return;

    setActionLoading(user.id);
    setError(null);
    setSuccess(null);

    try {
      const updated = await adminRevertPlan(user.id);
      setUsers((prev) => prev.map((u) => (u.id === user.id ? updated : u)));
      setSuccess(`Plan reverted to company deal plan for ${user.email}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to revert plan");
    } finally {
      setActionLoading(null);
    }
  }

  const activeDealDomains = deals.filter((d) => d.isActive).map((d) => d.domain);
  const activeCoupons = coupons.filter((c) => c.isActive);

  if (loading && users.length === 0) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <UsersRound className="h-6 w-6 text-nest-500" />
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">User Management</h1>
      </div>

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/20 dark:text-red-400">
          {error}
        </div>
      )}

      {success && (
        <div className="rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700 dark:border-green-900/50 dark:bg-green-950/20 dark:text-green-400">
          {success}
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <Select
          value={domainFilter}
          onChange={setDomainFilter}
          placeholder="All domains"
          options={[
            { value: "", label: "All domains" },
            ...activeDealDomains.map((d) => ({ value: d, label: d })),
          ]}
        />

        <Select
          value={couponFilter}
          onChange={(v) => setCouponFilter(v as "" | "true" | "false")}
          placeholder="Any coupon status"
          options={[
            { value: "", label: "Any coupon status" },
            { value: "true", label: "Has active coupon" },
            { value: "false", label: "No active coupon" },
          ]}
        />

        {(domainFilter || couponFilter) && (
          <button
            onClick={() => {
              setDomainFilter("");
              setCouponFilter("");
            }}
            className="flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
          >
            <XCircle className="h-4 w-4" />
            Clear filters
          </button>
        )}

        <span className="ml-auto text-sm text-gray-500 dark:text-gray-400">
          {totalCount} user{totalCount !== 1 ? "s" : ""}
        </span>
      </div>

      {/* Give Coupon Modal */}
      {giveCouponUserId && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 sm:items-center">
          <div className="w-full max-w-md rounded-t-2xl border border-gray-200 bg-white p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-xl dark:border-gray-700 dark:bg-gray-900 sm:mx-4 sm:rounded-xl sm:p-6">
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
                onClick={() => {
                  setGiveCouponUserId(null);
                  setSelectedCouponId("");
                }}
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
          <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 sm:items-center">
            <div className="w-full max-w-md rounded-t-2xl border border-gray-200 bg-white p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-xl dark:border-gray-700 dark:bg-gray-900 sm:mx-4 sm:rounded-xl sm:p-6">
              <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-4">Override Plan</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400 mb-2">
                Override the plan for{" "}
                <strong>{targetUser?.email}</strong>
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
                  options={eligiblePlans.map((p) => ({
                    value: p.id,
                    label: p.name,
                  }))}
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
                  onClick={() => {
                    setOverridePlanUserId(null);
                    setSelectedOverridePlanId("");
                  }}
                  className="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        );
      })()}

      {/* Users Table */}
      <section className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
        {users.length === 0 ? (
          <div className="px-6 py-12 text-center text-sm text-gray-500 dark:text-gray-400">
            No users found
          </div>
        ) : (
          <>
            <ScrollableTable>
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100 dark:border-gray-800">
                    <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">User</th>
                    <th className="hidden sm:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Plan</th>
                    <th className="hidden sm:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Status</th>
                    <th className="hidden md:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Agents</th>
                    <th className="hidden md:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Coupon</th>
                    <th className="hidden md:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Joined</th>
                    <th className="px-4 py-3 text-right font-medium text-gray-500 dark:text-gray-400"><span className="sr-only">Actions</span></th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <tr key={user.id} className="border-b border-gray-50 last:border-0 dark:border-gray-800/50">
                      <td className="px-4 py-3">
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
                        {user.companyDealDomain && (
                          <div className="text-xs text-gray-400 dark:text-gray-500">{user.companyDealDomain}</div>
                        )}
                        {/* Show plan/status inline on mobile */}
                        <div className="mt-1 flex flex-wrap items-center gap-2 sm:hidden">
                          <span className="text-xs text-gray-500 dark:text-gray-400">{user.planName || "No plan"}</span>
                          <StatusBadge status={user.subscriptionStatus} cancelAtPeriodEnd={user.cancelAtPeriodEnd} />
                        </div>
                      </td>
                      <td className="hidden sm:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">{user.planName || "None"}</td>
                      <td className="hidden sm:table-cell px-4 py-3">
                        <StatusBadge status={user.subscriptionStatus} cancelAtPeriodEnd={user.cancelAtPeriodEnd} />
                      </td>
                      <td className="hidden md:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">
                        {(() => {
                          const stats = adminAgentSummary?.accounts[user.accountId];
                          if (!stats) return "-";
                          return (
                            <span className="font-mono text-xs">
                              {stats.online}/{stats.installed}/{stats.maxAgents}
                            </span>
                          );
                        })()}
                      </td>
                      <td className="hidden md:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">
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
                      <td className="hidden md:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">
                        {format(new Date(user.createdAt), "dd MMM yyyy")}
                      </td>
                      <td className="px-4 py-3 text-right">
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
            </ScrollableTable>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between border-t border-gray-100 px-4 py-3 dark:border-gray-800">
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  Page {page} of {totalPages}
                </span>
                <div className="flex items-center gap-1">
                  <button
                    onClick={() => loadUsers(page - 1)}
                    disabled={page <= 1 || loading}
                    className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors disabled:opacity-40 disabled:pointer-events-none"
                  >
                    <ChevronLeft className="h-3.5 w-3.5" />
                    Prev
                  </button>
                  <button
                    onClick={() => loadUsers(page + 1)}
                    disabled={page >= totalPages || loading}
                    className="inline-flex items-center gap-1 rounded-lg px-2.5 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors disabled:opacity-40 disabled:pointer-events-none"
                  >
                    Next
                    <ChevronRight className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}
