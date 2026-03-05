import { useState, useEffect } from "react";
import { Building, Plus, RefreshCw, Trash2, Pencil } from "lucide-react";
import { clsx } from "clsx";
import { format } from "date-fns";
import { getAdminCompanyDeals, createAdminCompanyDeal, updateAdminCompanyDeal, deactivateAdminCompanyDeal, getPlans } from "../../api";
import type { CompanyDeal, PlanInfo } from "../../types";
import { PlanPicker } from "../../components/PlanPicker";

export function CompanyDeals() {
  const [deals, setDeals] = useState<CompanyDeal[]>([]);
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [domain, setDomain] = useState("");
  const [planId, setPlanId] = useState("");
  const [editingDealId, setEditingDealId] = useState<string | null>(null);
  const [editPlanId, setEditPlanId] = useState("");

  useEffect(() => {
    Promise.all([
      getAdminCompanyDeals().then(setDeals),
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
      const deal = await createAdminCompanyDeal({ domain, planId });
      setDeals((prev) => [deal, ...prev]);
      setSuccess(`Deal for "${deal.domain}" created successfully`);
      setDomain("");
      setPlanId("");
      setShowForm(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create deal");
    } finally {
      setSubmitting(false);
    }
  }

  function startEditing(deal: CompanyDeal) {
    setEditingDealId(deal.id);
    setEditPlanId(deal.planId);
  }

  async function handleUpdate(deal: CompanyDeal) {
    if (!editPlanId || editPlanId === deal.planId) {
      setEditingDealId(null);
      return;
    }
    setSubmitting(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await updateAdminCompanyDeal(deal.id, { planId: editPlanId });
      setDeals((prev) => prev.map((d) => (d.id === deal.id ? updated : d)));
      setSuccess(`Deal for "${deal.domain}" updated to ${updated.planName}`);
      setEditingDealId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update deal");
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDeactivate(deal: CompanyDeal) {
    if (
      !confirm(
        `Deactivate deal for "${deal.domain}"? Users with this domain who rely on this deal will lose access.`
      )
    )
      return;

    try {
      await deactivateAdminCompanyDeal(deal.id);
      setDeals((prev) =>
        prev.map((d) =>
          d.id === deal.id ? { ...d, isActive: false, deactivatedAt: new Date().toISOString() } : d
        )
      );
      setSuccess(`Deal for "${deal.domain}" deactivated`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to deactivate deal");
    }
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Building className="h-6 w-6 text-nest-500" />
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">Company Deals</h1>
        </div>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-3 py-1.5 text-sm font-medium text-white hover:bg-nest-600 transition-colors"
        >
          <Plus className="h-4 w-4" />
          New Deal
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
          <h2 className="mb-4 text-lg font-semibold text-gray-900 dark:text-white">Create Deal</h2>
          <form onSubmit={handleCreate} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Domain</label>
              <input
                type="text"
                required
                value={domain}
                onChange={(e) => setDomain(e.target.value)}
                placeholder="e.g. microsoft.com"
                className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-900 focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500 dark:border-gray-700 dark:bg-gray-800 dark:text-white"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Plan</label>
              <PlanPicker plans={plans} value={planId} onChange={setPlanId} required />
            </div>

            <div className="flex items-center gap-3 pt-2">
              <button
                type="submit"
                disabled={submitting}
                className="flex items-center gap-1.5 rounded-lg bg-nest-500 px-4 py-2 text-sm font-medium text-white hover:bg-nest-600 transition-colors disabled:opacity-50"
              >
                {submitting && <RefreshCw className="h-3.5 w-3.5 animate-spin" />}
                Create Deal
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
        {deals.length === 0 ? (
          <div className="px-6 py-12 text-center text-sm text-gray-500 dark:text-gray-400">
            No company deals created yet
          </div>
        ) : (
          <>
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100 dark:border-gray-800">
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Domain</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Plan</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Users</th>
                  <th className="px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Status</th>
                  <th className="hidden md:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Created</th>
                  <th className="hidden md:table-cell px-4 py-3 text-left font-medium text-gray-500 dark:text-gray-400">Deactivated</th>
                  <th className="px-4 py-3 text-right font-medium text-gray-500 dark:text-gray-400">Actions</th>
                </tr>
              </thead>
              <tbody>
                {deals.map((deal) => (
                  <tr key={deal.id} className="border-b border-gray-50 last:border-0 dark:border-gray-800/50">
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{deal.domain}</td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      {editingDealId === deal.id ? (
                        <PlanPicker plans={plans} value={editPlanId} onChange={setEditPlanId} required />
                      ) : (
                        deal.planName
                      )}
                    </td>
                    <td className="px-4 py-3 text-gray-700 dark:text-gray-300">
                      <div>{deal.userCount}</div>
                      {deal.overriddenCount > 0 && (
                        <div className="text-xs text-amber-600 dark:text-amber-400">
                          {deal.overriddenCount} overridden
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={clsx(
                          "rounded-full px-2.5 py-0.5 text-xs font-medium",
                          deal.isActive
                            ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
                            : "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400"
                        )}
                      >
                        {deal.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="hidden md:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">
                      {format(new Date(deal.createdAt), "dd MMM yyyy")}
                    </td>
                    <td className="hidden md:table-cell px-4 py-3 text-gray-700 dark:text-gray-300">
                      {deal.deactivatedAt
                        ? format(new Date(deal.deactivatedAt), "dd MMM yyyy")
                        : "-"}
                    </td>
                    <td className="px-4 py-3 text-right">
                      {deal.isActive && editingDealId === deal.id ? (
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => handleUpdate(deal)}
                            disabled={submitting}
                            className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-green-600 hover:bg-green-50 dark:text-green-400 dark:hover:bg-green-900/20 transition-colors disabled:opacity-50"
                          >
                            {submitting ? <RefreshCw className="h-3 w-3 animate-spin" /> : "Save"}
                          </button>
                          <button
                            onClick={() => setEditingDealId(null)}
                            className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-gray-600 hover:bg-gray-50 dark:text-gray-400 dark:hover:bg-gray-800 transition-colors"
                          >
                            Cancel
                          </button>
                        </div>
                      ) : deal.isActive ? (
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => startEditing(deal)}
                            className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-nest-600 hover:bg-nest-50 dark:text-nest-400 dark:hover:bg-nest-900/20 transition-colors"
                          >
                            <Pencil className="h-3 w-3" />
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeactivate(deal)}
                            className="inline-flex items-center gap-1 rounded px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20 transition-colors"
                          >
                            <Trash2 className="h-3 w-3" />
                            Deactivate
                          </button>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
      </section>
    </div>
  );
}
