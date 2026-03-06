import { useState } from "react";
import { ChevronDown, ChevronUp, Sparkles, Star, Check, Gift } from "lucide-react";
import { clsx } from "clsx";
import { addMonths, addDays, format } from "date-fns";
import type { PlanInfo, CouponValidation } from "../types";
import { formatDiscountDescription } from "../types";
import { ScrollableTable } from "./ScrollableTable";

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
  offerCoupon?: CouponValidation | null;
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
  offerCoupon,
}: PricingCardsProps) {
  const [expanded, setExpanded] = useState(false);

  const hasOffer = !!offerCoupon?.valid;
  const basePlans = plans.filter((p) => p.sortOrder <= 3);
  const offerPlanInMain = hasOffer
    ? basePlans.some((p) => p.id === offerCoupon.planId)
    : false;

  // The offer card takes one slot: remove matching plan from main, or drop first card
  let mainPlans: PlanInfo[];
  let displacedPlan: PlanInfo | null = null;
  if (hasOffer) {
    if (offerPlanInMain) {
      // Remove the offer's plan from main — it moves to "Need more?"
      displacedPlan = basePlans.find((p) => p.id === offerCoupon.planId) ?? null;
      mainPlans = basePlans.filter((p) => p.id !== offerCoupon.planId);
    } else {
      // Offer plan is hidden (sortOrder > 3), drop first main card to make room
      displacedPlan = basePlans[0] ?? null;
      mainPlans = basePlans.slice(1);
    }
  } else {
    mainPlans = basePlans;
  }

  // Extra plans: everything with sortOrder > 3, minus the offer plan if shown as offer card
  const baseExtraPlans = plans.filter((p) => {
    if (p.sortOrder <= 3) return false;
    if (hasOffer && p.id === offerCoupon.planId) return false;
    return true;
  });
  // Displaced card moves to "Need more?" section
  const allExtraPlans = displacedPlan ? [displacedPlan, ...baseExtraPlans] : baseExtraPlans;

  return (
    <>
      <div className="grid gap-6 md:grid-cols-3">
        {offerCoupon?.valid && <OfferCard offer={offerCoupon} onSelectPlan={onSelectPlan} selecting={selecting} currentPlanId={currentPlanId} hasActiveSubscription={hasActiveSubscription} />}
        {mainPlans.map((plan) => {
          const isPopular = plan.name === "Robin";
          const coupon = plan.defaultCoupon;
          const couponDesc = coupon
            ? formatDiscountDescription(
                coupon.discountType,
                coupon.freeMonths,
                coupon.percentOff,
                coupon.amountOffCents,
                coupon.freeDays,
                coupon.durationMonths,
              )
            : null;

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

              <div className="mt-2">
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
                    {plan.name}
                  </h3>
                  {coupon && <CouponBadge description={couponDesc!} />}
                </div>
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
                  <span className="text-nest-500" aria-hidden="true">&#10003;</span>
                  {plan.maxAgents} {plan.maxAgents === 1 ? "agent" : "agents"}
                </li>
                <li className="flex items-center gap-2">
                  <span className="text-nest-500" aria-hidden="true">&#10003;</span>
                  {plan.maxSessions} concurrent sessions
                </li>
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
                  {selecting === plan.id ? "Selecting..." : "Get started"}
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

      {allExtraPlans.length > 0 && (
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
              <ScrollableTable>
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
                    {allExtraPlans.map((plan) => (
                      <tr
                        key={plan.id}
                        className="border-b border-gray-100 last:border-0 dark:border-gray-800"
                      >
                        <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                          <span className="inline-flex items-center gap-1.5">
                            {plan.name}
                            {plan.defaultCoupon && (
                              <CouponBadge
                                description={formatDiscountDescription(
                                  plan.defaultCoupon.discountType,
                                  plan.defaultCoupon.freeMonths,
                                  plan.defaultCoupon.percentOff,
                                  plan.defaultCoupon.amountOffCents,
                                  plan.defaultCoupon.freeDays,
                                  plan.defaultCoupon.durationMonths,
                                )}
                              />
                            )}
                          </span>
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
              </ScrollableTable>
            </div>
          )}
        </div>
      )}
    </>
  );
}

function OfferCard({
  offer,
  onSelectPlan,
  selecting,
  currentPlanId,
  hasActiveSubscription,
}: {
  offer: CouponValidation;
  onSelectPlan: (planId: string) => void;
  selecting: string | null;
  currentPlanId?: string | null;
  hasActiveSubscription?: boolean;
}) {
  const discountDesc = formatDiscountDescription(
    offer.discountType ?? "FreeMonths",
    offer.freeMonths,
    offer.percentOff,
    offer.amountOffCents,
    offer.freeDays,
    offer.durationMonths,
  );

  const priceCents = offer.planPriceCents ?? 0;
  const price = (priceCents / 100).toFixed(0);

  // Calculate the effective price during the discount period
  let effectivePrice: string | null = null;
  if (offer.discountType === "PercentOff" && offer.percentOff) {
    const discounted = priceCents * (1 - offer.percentOff / 100);
    effectivePrice = `$${(discounted / 100).toFixed(0)}`;
  } else if (offer.discountType === "AmountOff" && offer.amountOffCents) {
    const discounted = Math.max(0, priceCents - offer.amountOffCents);
    effectivePrice = `$${(discounted / 100).toFixed(0)}`;
  } else if (offer.discountType === "FreeMonths" || offer.discountType === "FreeDays") {
    effectivePrice = "$0";
  }

  // Calculate when the normal price kicks in
  let normalPriceDate: string | null = null;
  const now = new Date();
  if (offer.discountType === "FreeMonths" && offer.freeMonths) {
    normalPriceDate = format(addMonths(now, offer.freeMonths), "dd MMM yyyy");
  } else if (offer.discountType === "FreeDays" && offer.freeDays) {
    normalPriceDate = format(addDays(now, offer.freeDays), "dd MMM yyyy");
  } else if ((offer.discountType === "PercentOff" || offer.discountType === "AmountOff") && offer.durationMonths) {
    normalPriceDate = format(addMonths(now, offer.durationMonths), "dd MMM yyyy");
  }

  return (
    <div className="relative flex flex-col rounded-2xl border-2 border-amber-400 bg-amber-50/50 p-6 shadow-lg shadow-amber-500/10 dark:border-amber-500 dark:bg-amber-950/20">
      <div className="absolute -top-3 left-1/2 -translate-x-1/2">
        <span className="inline-flex items-center gap-1 rounded-full bg-amber-500 px-3 py-1 text-xs font-semibold text-white">
          <Gift className="h-3 w-3" aria-hidden="true" />
          Your Offer
        </span>
      </div>

      <div className="mt-2">
        <div className="flex items-center gap-2">
          <h3 className="text-lg font-semibold text-gray-900 dark:text-white">
            {offer.planName}
          </h3>
        </div>
        <div className="mt-1 rounded-lg bg-amber-100 px-3 py-2 text-sm font-medium text-amber-800 dark:bg-amber-900/40 dark:text-amber-300">
          {discountDesc}
        </div>
        <div className="mt-3 flex items-baseline gap-1">
          {effectivePrice && effectivePrice !== `$${price}` ? (
            <>
              <span className="text-3xl font-bold text-gray-900 dark:text-white">
                {effectivePrice}
              </span>
              <span className="text-lg text-gray-400 line-through dark:text-gray-500">
                ${price}
              </span>
              <span className="text-sm text-gray-500 dark:text-gray-400">
                AUD/mo
              </span>
            </>
          ) : (
            <>
              <span className="text-3xl font-bold text-gray-900 dark:text-white">
                ${price}
              </span>
              <span className="text-sm text-gray-500 dark:text-gray-400">
                AUD/mo
              </span>
            </>
          )}
        </div>
      </div>

      <ul className="mt-4 flex-1 space-y-3 text-sm text-gray-600 dark:text-gray-300">
        <li className="flex items-center gap-2">
          <span className="text-nest-500" aria-hidden="true">&#10003;</span>
          {offer.planMaxAgents} {(offer.planMaxAgents ?? 0) === 1 ? "agent" : "agents"}
        </li>
        <li className="flex items-center gap-2">
          <span className="text-nest-500" aria-hidden="true">&#10003;</span>
          {offer.planMaxSessions} concurrent sessions
        </li>
      </ul>

      {normalPriceDate && (
        <p className="mt-3 text-xs text-gray-500 dark:text-gray-400">
          Normal price of ${price} AUD/mo applies from {normalPriceDate}
        </p>
      )}

      {offer.expiresAt && (
        <p className="mt-1 text-xs text-amber-600 dark:text-amber-400">
          Offer expires {format(new Date(offer.expiresAt), "dd MMM yyyy")}
        </p>
      )}

      {currentPlanId === offer.planId && hasActiveSubscription ? (
        <div className="mt-4 flex w-full items-center justify-center gap-1.5 rounded-lg border border-green-300 bg-green-50 px-4 py-2.5 text-sm font-semibold text-green-700 dark:border-green-700 dark:bg-green-950/30 dark:text-green-400">
          <Check className="h-4 w-4" aria-hidden="true" />
          Current plan
        </div>
      ) : (
        <button
          onClick={() => onSelectPlan(offer.planId!)}
          disabled={selecting !== null || !!hasActiveSubscription}
          className="mt-4 w-full rounded-lg bg-amber-500 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-amber-600 disabled:opacity-50"
        >
          {selecting === offer.planId ? "Selecting..." : "Claim offer"}
        </button>
      )}
    </div>
  );
}

function CouponBadge({ description }: { description: string }) {
  const [open, setOpen] = useState(false);

  return (
    <span className="relative inline-block">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        onMouseEnter={() => setOpen(true)}
        onMouseLeave={() => setOpen(false)}
        className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900/30 dark:text-amber-400"
        aria-label="Offer available"
      >
        <Star className="h-3 w-3 fill-current" aria-hidden="true" />
        Offer
      </button>
      {open && (
        <span className="absolute left-1/2 top-full z-10 mt-2 w-64 -translate-x-1/2 rounded-lg border border-gray-200 bg-white p-3 text-left text-xs shadow-lg dark:border-gray-700 dark:bg-gray-800">
          <span className="block font-semibold text-gray-900 dark:text-white">
            {description}
          </span>
          <span className="mt-1.5 block text-gray-500 dark:text-gray-400">
            Applied automatically at checkout. Limited availability — if the offer has ended by checkout, take a screenshot showing it was listed and reach out at{" "}
            <a
              href="https://gordonbeeming.com"
              target="_blank"
              rel="noopener noreferrer"
              className="font-medium text-nest-600 underline hover:text-nest-500 dark:text-nest-400"
            >
              gordonbeeming.com
            </a>{" "}
            and I'll apply a coupon to your account.
          </span>
        </span>
      )}
    </span>
  );
}
