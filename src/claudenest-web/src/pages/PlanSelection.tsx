import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Bird, ChevronDown, ChevronUp, Sparkles, Clock } from "lucide-react";
import { clsx } from "clsx";
import { getPlans, selectPlan } from "../api";
import { useUserContext } from "../contexts/UserContext";
import type { PlanInfo } from "../types";

export function PlanSelection() {
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [selecting, setSelecting] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(false);
  const navigate = useNavigate();
  const { updateAccount } = useUserContext();

  useEffect(() => {
    getPlans()
      .then(setPlans)
      .finally(() => setLoading(false));
  }, []);

  const handleSelect = async (planId: string) => {
    setSelecting(planId);
    try {
      const account = await selectPlan(planId);
      updateAccount(account);
      navigate("/");
    } catch {
      setSelecting(null);
    }
  };

  const mainPlans = plans.filter((p) => p.sortOrder <= 3);
  const extraPlans = plans.filter((p) => p.sortOrder > 3);

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <div className="h-5 w-5 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl py-8">
      <div className="text-center">
        <Bird className="mx-auto h-12 w-12 text-nest-500" />
        <h1 className="mt-4 text-3xl font-bold text-gray-900 dark:text-white">
          Choose your plan
        </h1>
        <p className="mt-2 text-gray-500 dark:text-gray-400">
          All plans include full access to ClaudeNest. Pick the one that fits your workflow.
        </p>
      </div>

      <div className="mt-10 grid gap-6 md:grid-cols-3">
        {mainPlans.map((plan) => {
          const isPopular = plan.name === "Robin";
          const hasTrial = plan.trialDays > 0;

          return (
            <div
              key={plan.id}
              className={clsx(
                "relative flex flex-col rounded-2xl border p-6",
                isPopular
                  ? "border-nest-500 bg-nest-50/50 shadow-lg shadow-nest-500/10 dark:border-nest-400 dark:bg-nest-950/20"
                  : "border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900",
              )}
            >
              {isPopular && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-nest-500 px-3 py-1 text-xs font-semibold text-white">
                    <Sparkles className="h-3 w-3" />
                    Most Popular
                  </span>
                </div>
              )}

              {hasTrial && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-amber-500 px-3 py-1 text-xs font-semibold text-white">
                    <Clock className="h-3 w-3" />
                    {plan.trialDays}-day free trial
                  </span>
                </div>
              )}

              <div className="mt-2">
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                  {plan.name}
                </h3>
                <div className="mt-2 flex items-baseline gap-1">
                  <span className="text-3xl font-bold text-gray-900 dark:text-white">
                    ${(plan.priceCents / 100).toFixed(0)}
                  </span>
                  <span className="text-sm text-gray-500 dark:text-gray-400">
                    AUD/mo
                  </span>
                </div>
              </div>

              <ul className="mt-6 flex-1 space-y-3 text-sm text-gray-600 dark:text-gray-300">
                <li className="flex items-center gap-2">
                  <span className="text-nest-500">&#10003;</span>
                  {plan.maxAgents} {plan.maxAgents === 1 ? "agent" : "agents"}
                </li>
                <li className="flex items-center gap-2">
                  <span className="text-nest-500">&#10003;</span>
                  {plan.maxSessions} concurrent sessions
                </li>
                {hasTrial && (
                  <li className="flex items-center gap-2">
                    <span className="text-amber-500">&#10003;</span>
                    {plan.trialDays}-day free trial
                  </li>
                )}
              </ul>

              <button
                onClick={() => handleSelect(plan.id)}
                disabled={selecting !== null}
                className={clsx(
                  "mt-6 w-full rounded-lg px-4 py-2.5 text-sm font-semibold transition-colors disabled:opacity-50",
                  isPopular
                    ? "bg-nest-500 text-white hover:bg-nest-600"
                    : "border border-gray-300 text-gray-900 hover:bg-gray-50 dark:border-gray-700 dark:text-white dark:hover:bg-gray-800",
                )}
              >
                {selecting === plan.id ? "Selecting..." : hasTrial ? "Start free trial" : "Get started"}
              </button>
            </div>
          );
        })}
      </div>

      {extraPlans.length > 0 && (
        <div className="mt-10">
          <button
            onClick={() => setExpanded((e) => !e)}
            className="mx-auto flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            Need more?
            {expanded ? (
              <ChevronUp className="h-4 w-4" />
            ) : (
              <ChevronDown className="h-4 w-4" />
            )}
          </button>

          {expanded && (
            <div className="mt-6 overflow-hidden rounded-xl border border-gray-200 dark:border-gray-800">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-gray-200 bg-gray-50 dark:border-gray-800 dark:bg-gray-900">
                    <th className="px-4 py-3 font-medium text-gray-500 dark:text-gray-400">Plan</th>
                    <th className="px-4 py-3 font-medium text-gray-500 dark:text-gray-400">Price</th>
                    <th className="px-4 py-3 font-medium text-gray-500 dark:text-gray-400">Agents</th>
                    <th className="px-4 py-3 font-medium text-gray-500 dark:text-gray-400">Sessions</th>
                    <th className="px-4 py-3"></th>
                  </tr>
                </thead>
                <tbody>
                  {extraPlans.map((plan) => (
                    <tr
                      key={plan.id}
                      className="border-b border-gray-100 last:border-0 dark:border-gray-800"
                    >
                      <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                        {plan.name}
                      </td>
                      <td className="px-4 py-3 text-gray-600 dark:text-gray-300">
                        ${(plan.priceCents / 100).toFixed(0)} AUD/mo
                      </td>
                      <td className="px-4 py-3 text-gray-600 dark:text-gray-300">
                        {plan.maxAgents}
                      </td>
                      <td className="px-4 py-3 text-gray-600 dark:text-gray-300">
                        {plan.maxSessions}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          onClick={() => handleSelect(plan.id)}
                          disabled={selecting !== null}
                          className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
                        >
                          {selecting === plan.id ? "..." : "Select"}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
