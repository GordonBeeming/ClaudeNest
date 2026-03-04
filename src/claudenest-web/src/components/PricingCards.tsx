import { useState } from "react";
import { ChevronDown, ChevronUp, Sparkles, Tag, Check } from "lucide-react";
import { clsx } from "clsx";
import type { PlanInfo, CouponValidation } from "../types";
import { formatDiscountDescription } from "../types";

export interface PricingCardsProps {
  plans: PlanInfo[];
  onSelectPlan: (planId: string) => void;
  selecting: string | null;
  currentPlanId?: string | null;
  hasActiveSubscription?: boolean;
  showCouponInput?: boolean;
  couponCode?: string;
  onCouponCodeChange?: (code: string) => void;
  couponResult?: CouponValidation | null;
  onApplyCoupon?: () => void;
  couponLoading?: boolean;
}

export function PricingCards({
  plans,
  onSelectPlan,
  selecting,
  currentPlanId,
  hasActiveSubscription,
  showCouponInput = false,
  couponCode = "",
  onCouponCodeChange,
  couponResult,
  onApplyCoupon,
  couponLoading,
}: PricingCardsProps) {
  const [expanded, setExpanded] = useState(false);

  const mainPlans = plans.filter((p) => p.sortOrder <= 3);
  const extraPlans = plans.filter((p) => p.sortOrder > 3);

  return (
    <>
      <div className="grid gap-6 md:grid-cols-3">
        {mainPlans.map((plan) => {
          const isPopular = plan.name === "Robin";
          const hasCoupon = !!plan.defaultCoupon;
          const freeDays = hasCoupon ? plan.defaultCoupon!.freeMonths * 30 : 0;

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
                    <Sparkles className="h-3 w-3" aria-hidden="true" />
                    Most Popular
                  </span>
                </div>
              )}

              {hasCoupon && !isPopular && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-amber-500 px-3 py-1 text-xs font-semibold text-white">
                    <Tag className="h-3 w-3" aria-hidden="true" />
                    {freeDays}-day free trial
                  </span>
                </div>
              )}

              <div className="mt-2">
                <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                  {plan.name}
                </h3>
                <div className="mt-2 flex items-baseline gap-1">
                  {hasCoupon ? (
                    <span className="text-sm text-gray-500 dark:text-gray-400">
                      <span className="text-3xl font-bold text-gray-900 dark:text-white">
                        ${(plan.priceCents / 100).toFixed(0)}
                      </span>{" "}
                      AUD/mo after {freeDays}-day free trial
                    </span>
                  ) : (
                    <>
                      <span className="text-3xl font-bold text-gray-900 dark:text-white">
                        ${(plan.priceCents / 100).toFixed(0)}
                      </span>
                      <span className="text-sm text-gray-500 dark:text-gray-400">
                        AUD/mo
                      </span>
                    </>
                  )}
                </div>
              </div>

              <ul className="mt-6 flex-1 space-y-3 text-sm text-gray-600 dark:text-gray-300">
                <li className="flex items-center gap-2">
                  <span className="text-nest-500" aria-hidden="true">&#10003;</span>
                  {plan.maxAgents} {plan.maxAgents === 1 ? "agent" : "agents"}
                </li>
                <li className="flex items-center gap-2">
                  <span className="text-nest-500" aria-hidden="true">&#10003;</span>
                  {plan.maxSessions} concurrent sessions
                </li>
                {hasCoupon && (
                  <li className="flex items-center gap-2">
                    <span className="text-amber-500" aria-hidden="true">&#10003;</span>
                    {freeDays}-day free trial
                  </li>
                )}
              </ul>

              {currentPlanId === plan.id && hasActiveSubscription ? (
                <div className="mt-6 flex w-full items-center justify-center gap-1.5 rounded-lg border border-green-300 bg-green-50 px-4 py-2.5 text-sm font-semibold text-green-700 dark:border-green-700 dark:bg-green-950/30 dark:text-green-400">
                  <Check className="h-4 w-4" aria-hidden="true" />
                  Current plan
                </div>
              ) : (
                <button
                  onClick={() => onSelectPlan(plan.id)}
                  disabled={selecting !== null || !!hasActiveSubscription}
                  className={clsx(
                    "mt-6 w-full rounded-lg px-4 py-2.5 text-sm font-semibold transition-colors disabled:opacity-50",
                    isPopular
                      ? "bg-nest-500 text-white hover:bg-nest-600"
                      : "border border-gray-300 text-gray-900 hover:bg-gray-50 dark:border-gray-700 dark:text-white dark:hover:bg-gray-800",
                  )}
                >
                  {selecting === plan.id ? "Selecting..." : hasCoupon ? "Start free trial" : "Get started"}
                </button>
              )}
            </div>
          );
        })}
      </div>

      {/* Coupon code input */}
      {showCouponInput && (
        <div className="mt-8 flex flex-col items-center gap-3">
          <p className="text-sm text-gray-500 dark:text-gray-400">Have a coupon code?</p>
          <div className="flex gap-2">
            <label htmlFor="coupon-input" className="sr-only">Coupon code</label>
            <input
              id="coupon-input"
              type="text"
              value={couponCode}
              onChange={(e) => onCouponCodeChange?.(e.target.value.toUpperCase())}
              placeholder="Enter coupon code"
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900 dark:text-white"
            />
            <button
              onClick={onApplyCoupon}
              disabled={couponLoading || !couponCode.trim()}
              className="rounded-lg bg-gray-800 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50 dark:bg-gray-700 dark:hover:bg-gray-600"
            >
              {couponLoading ? "Checking..." : "Apply"}
            </button>
          </div>
          {couponResult && (
            <p
              className={clsx(
                "text-sm",
                couponResult.valid
                  ? "text-green-600 dark:text-green-400"
                  : "text-red-600 dark:text-red-400",
              )}
              role="status"
            >
              {couponResult.valid
                ? `Coupon applied: ${formatDiscountDescription(
                    couponResult.discountType ?? "FreeMonths",
                    couponResult.freeMonths,
                    couponResult.percentOff,
                    couponResult.amountOffCents,
                    couponResult.freeDays,
                    couponResult.durationMonths,
                  )} on ${couponResult.planName}`
                : couponResult.reason || "Invalid coupon code"}
            </p>
          )}
        </div>
      )}

      {extraPlans.length > 0 && (
        <div className="mt-10">
          <button
            onClick={() => setExpanded((e) => !e)}
            className="mx-auto flex items-center gap-2 text-sm font-medium text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
            aria-expanded={expanded}
          >
            Need more?
            {expanded ? (
              <ChevronUp className="h-4 w-4" aria-hidden="true" />
            ) : (
              <ChevronDown className="h-4 w-4" aria-hidden="true" />
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
                    <th className="px-4 py-3"><span className="sr-only">Actions</span></th>
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
                        {currentPlanId === plan.id && hasActiveSubscription ? (
                          <span className="inline-flex items-center gap-1 text-xs font-medium text-green-600 dark:text-green-400">
                            <Check className="h-3 w-3" aria-hidden="true" />
                            Current
                          </span>
                        ) : (
                          <button
                            onClick={() => onSelectPlan(plan.id)}
                            disabled={selecting !== null || !!hasActiveSubscription}
                            className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
                          >
                            {selecting === plan.id ? "..." : "Select"}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </>
  );
}
