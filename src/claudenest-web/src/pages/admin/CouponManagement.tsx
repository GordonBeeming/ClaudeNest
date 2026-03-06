import { useState, useEffect } from "react";
import { Tag, Plus, RefreshCw, Trash2 } from "lucide-react";
import { clsx } from "clsx";
import { format } from "date-fns";
import { getAdminCoupons, createAdminCoupon, deleteAdminCoupon, getPlans } from "../../api";
import type { CouponInfo, PlanInfo, DiscountType } from "../../types";
import { formatDiscountDescription } from "../../types";
import { PlanPicker } from "../../components/PlanPicker";
import { ScrollableTable } from "../../components/ScrollableTable";

export function CouponManagement() {
  const [coupons, setCoupons] = useState<CouponInfo[]>([]);
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [planId, setPlanId] = useState("");
  const [discountType, setDiscountType] = useState<DiscountType>("FreeMonths");
  const [freeMonths, setFreeMonths] = useState(1);
  const [percentOff, setPercentOff] = useState(10);
  const [amountOffDollars, setAmountOffDollars] = useState(5);
  const [freeDays, setFreeDays] = useState(14);
  const [durationMonths, setDurationMonths] = useState(3);
  const [maxRedemptions, setMaxRedemptions] = useState(1);
  const [expiresAt, setExpiresAt] = useState("");

  useEffect(() => {
    Promise.all([
      getAdminCoupons().then(setCoupons),
      getPlans().then(setPlans),
    ])
      .catch(() => setError("Failed to load data"))
      .finally(() => setLoading(false));
  }, []);

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    if (!planId) {
      setError("Please select a plan");
      return;
    }
    setSubmitting(true);
    setError(null);
    setSuccess(null);

    try {
      const coupon = await createAdminCoupon({
        code,
        planId,
        discountType,
        freeMonths: discountType === "FreeMonths" ? freeMonths : 0,
        percentOff: discountType === "PercentOff" ? percentOff : null,
        amountOffCents: discountType === "AmountOff" ? amountOffDollars * 100 : null,
        freeDays: discountType === "FreeDays" ? freeDays : null,
        durationMonths: discountType === "PercentOff" || discountType === "AmountOff" ? durationMonths : null,
        maxRedemptions,
        ...(expiresAt ? { expiresAt } : {}),
      });
      setCoupons((prev) => [coupon, ...prev]);
      setSuccess(`Coupon "${coupon.code}" created successfully`);
      setCode("");
      setPlanId("");
      setDiscountType("FreeMonths");
      setFreeMonths(1);
      setPercentOff(10);
      setAmountOffDollars(5);
      setFreeDays(14);
      setDurationMonths(3);
      setMaxRedemptions(1);
      setExpiresAt("");
      setShowForm(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create coupon");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDeactivate(coupon: CouponInfo) {
    if (!confirm(`Deactivate coupon "${coupon.code}"? This cannot be undone.`)) return;

    try {
      await deleteAdminCoupon(coupon.id);
      setCoupons((prev) => prev.map((c) => (c.id === coupon.id ? { ...c, isActive: false } : c)));
      setSuccess(`Coupon "${coupon.code}" deactivated`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to deactivate coupon");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  const discountTypes: { value: DiscountType; label: string }[] = [
    { value: "FreeMonths", label: "Free months" },
    { value: "PercentOff", label: "% off" },
    { value: "AmountOff", label: "$ off/mo" },
    { value: "FreeDays", label: "Free days" },
  ];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Tag className="h-6 w-6 text-nest-500" />
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Coupon Management</h1>
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-nest-600 transition-colors"
        >
          <Plus className="h-4 w-4" />
          New Coupon
        </button>
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

      {showForm && (
        <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <h2 className="mb-4 text-lg font-semibold text-gray-900 dark:text-white">Create Coupon</h2>
          <form onSubmit={handleCreate} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Code</label>
              <input
                type="text"
                required
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                placeholder="e.g. WELCOME2024"
                className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 uppercase focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Plan</label>
              <PlanPicker plans={plans} value={planId} onChange={setPlanId} required />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Discount Type</label>
              <div className="flex flex-wrap gap-2">
                {discountTypes.map((dt) => (
                  <button
                    key={dt.value}
                    type="button"
                    onClick={() => setDiscountType(dt.value)}
                    className={clsx(
                      "rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors",
                      discountType === dt.value
                        ? "border-nest-500 bg-nest-50 text-nest-700 dark:border-nest-400 dark:bg-nest-950/30 dark:text-nest-300"
                        : "border-gray-300 text-gray-600 hover:border-gray-400 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-600"
                    )}
                  >
                    {dt.label}
                  </button>
                ))}
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {discountType === "FreeMonths" && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Free Months</label>
                  <input
                    type="number"
                    required
                    min={1}
                    value={freeMonths}
                    onChange={(e) => setFreeMonths(Number(e.target.value))}
                    className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                  />
                </div>
              )}

              {discountType === "PercentOff" && (
                <>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Percent Off (%)</label>
                    <input
                      type="number"
                      required
                      min={1}
                      max={100}
                      value={percentOff}
                      onChange={(e) => setPercentOff(Number(e.target.value))}
                      className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Duration (months)</label>
                    <input
                      type="number"
                      required
                      min={1}
                      value={durationMonths}
                      onChange={(e) => setDurationMonths(Number(e.target.value))}
                      className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                    />
                  </div>
                </>
              )}

              {discountType === "AmountOff" && (
                <>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Amount Off (AUD)</label>
                    <input
                      type="number"
                      required
                      min={1}
                      value={amountOffDollars}
                      onChange={(e) => setAmountOffDollars(Number(e.target.value))}
                      className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Duration (months)</label>
                    <input
                      type="number"
                      required
                      min={1}
                      value={durationMonths}
                      onChange={(e) => setDurationMonths(Number(e.target.value))}
                      className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                    />
                  </div>
                </>
              )}

              {discountType === "FreeDays" && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Free Days</label>
                  <input
                    type="number"
                    required
                    min={1}
                    value={freeDays}
                    onChange={(e) => setFreeDays(Number(e.target.value))}
                    className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                  />
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Max Redemptions</label>
                <input
                  type="number"
                  required
                  min={1}
                  value={maxRedemptions}
                  onChange={(e) => setMaxRedemptions(Number(e.target.value))}
                  className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Expires At <span className="text-gray-400 font-normal">(optional)</span>
              </label>
              <input
                type="date"
                value={expiresAt}
                onChange={(e) => setExpiresAt(e.target.value)}
                className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>

            <div className="flex items-center gap-3 pt-2">
              <button
                type="submit"
                disabled={submitting}
                className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600 transition-colors disabled:opacity-50"
              >
                {submitting && <RefreshCw className="h-3.5 w-3.5 animate-spin" />}
                Create Coupon
              </button>
              <button
                type="button"
                onClick={() => setShowForm(false)}
                className="rounded-lg px-4 py-2 text-sm text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
              >
                Cancel
              </button>
            </div>
          </form>
        </section>
      )}

      <section className="rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
        {coupons.length === 0 ? (
          <div className="px-6 py-12 text-center text-sm text-gray-500 dark:text-gray-400">
            No coupons created yet
          </div>
        ) : (
          <ScrollableTable>
            <table className="min-w-[640px] w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 dark:border-gray-800">
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Code</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Plan</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Discount</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Redeemed</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Expires</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Status</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-500 dark:text-gray-400">Actions</th>
                </tr>
              </thead>
              <tbody>
                {coupons.map((coupon) => (
                  <tr key={coupon.id} className="border-b border-gray-50 last:border-0 dark:border-gray-800/50">
                    <td className="px-4 py-3 font-mono text-gray-900 dark:text-white">{coupon.code}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">{coupon.planName}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      {formatDiscountDescription(
                        coupon.discountType,
                        coupon.freeMonths,
                        coupon.percentOff,
                        coupon.amountOffCents,
                        coupon.freeDays,
                        coupon.durationMonths,
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      {coupon.timesRedeemed} / {coupon.maxRedemptions}
                    </td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      {coupon.expiresAt
                        ? format(new Date(coupon.expiresAt), "dd MMM yyyy")
                        : "Never"}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={clsx(
                          "rounded-full px-2.5 py-0.5 text-xs font-medium",
                          coupon.isActive
                            ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
                            : "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                        )}
                      >
                        {coupon.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      {coupon.isActive && (
                        <button
                          onClick={() => handleDeactivate(coupon)}
                          className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20 transition-colors"
                        >
                          <Trash2 className="h-3 w-3" />
                          Deactivate
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </ScrollableTable>
        )}
      </section>
    </div>
  );
}
