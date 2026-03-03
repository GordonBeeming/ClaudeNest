import { useState, useEffect } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { Bird, ChevronDown, ChevronUp, Sparkles, Tag, Check } from "lucide-react";
import { clsx } from "clsx";
import { getPlans, selectPlan, redeemCoupon } from "../api";
import { useUserContext } from "../contexts/UserContext";
import type { PlanInfo, CouponValidation } from "../types";
import { formatDiscountDescription } from "../types";

export function PlanSelection() {
  const { user } = useUserContext();
  const hasActiveSubscription = user?.account?.hasStripeSubscription &&
    (user.account.subscriptionStatus === "Active" || user.account.subscriptionStatus === "Trialing" || user.account.subscriptionStatus === "PastDue") &&
    !user.account.cancelAtPeriodEnd;
  const currentPlanId = user?.account?.planId;
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [selecting, setSelecting] = useState<string | null>(null);
  const [expanded, setExpanded] = useState(false);
  const [couponCode, setCouponCode] = useState("");
  const [couponResult, setCouponResult] = useState<CouponValidation | null>(null);
  const [couponLoading, setCouponLoading] = useState(false);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { updateAccount, refreshUser } = useUserContext();

  useEffect(() => {
    if (searchParams.get("success") === "true") {
      setSearchParams({}, { replace: true });
      refreshUser().then(() => navigate("/", { replace: true }));
    } else if (searchParams.get("cancelled") === "true") {
      setMessage({ type: "error", text: "Payment was cancelled." });
      setSearchParams({}, { replace: true });
    }
  }, [searchParams, setSearchParams, refreshUser, navigate]);

  useEffect(() => {
    getPlans()
      .then(setPlans)
      .finally(() => setLoading(false));
  }, []);

  const handleSelect = async (planId: string) => {
    setSelecting(planId);
    try {
      const validCoupon = couponResult?.valid ? couponResult.code : undefined;
      const result = await selectPlan(planId, validCoupon);
      if (result.action === "redirect" && result.redirectUrl) {
        window.location.href = result.redirectUrl;
      } else if (result.action === "local" && result.account) {
        updateAccount(result.account);
        navigate("/");
      } else {
        setSelecting(null);
      }
    } catch {
      setSelecting(null);
    }
  };

  const handleApplyCoupon = async () => {
    if (!couponCode.trim()) return;
    setCouponLoading(true);
    setCouponResult(null);
    try {
      const result = await redeemCoupon(couponCode.trim());
      setCouponResult(result);
    } catch {
      setCouponResult({ valid: false, reason: "Failed to validate coupon" });
    } finally {
      setCouponLoading(false);
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

      {hasActiveSubscription && (
        <div className="mt-6 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-700 dark:bg-amber-950/30 dark:text-amber-200">
          You have an active subscription. To change plans, cancel your current subscription from{" "}
          <Link to="/account" className="font-medium underline hover:no-underline">Account settings</Link>
          {" "}first, or{" "}
          <a href="mailto:support@claudenest.com" className="font-medium underline hover:no-underline">contact support</a>
          {" "}for assistance.
        </div>
      )}

      {message && (
        <div
          className={clsx(
            "mt-6 rounded-lg px-4 py-3 text-sm",
            message.type === "success"
              ? "border border-green-300 bg-green-50 text-green-800 dark:border-green-700 dark:bg-green-950/30 dark:text-green-200"
              : "border border-red-300 bg-red-50 text-red-800 dark:border-red-700 dark:bg-red-950/30 dark:text-red-200",
          )}
        >
          {message.text}
        </div>
      )}

      <div className="mt-10 grid gap-6 md:grid-cols-3">
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
                    <Sparkles className="h-3 w-3" />
                    Most Popular
                  </span>
                </div>
              )}

              {hasCoupon && !isPopular && (
                <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                  <span className="inline-flex items-center gap-1 rounded-full bg-amber-500 px-3 py-1 text-xs font-semibold text-white">
                    <Tag className="h-3 w-3" />
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
                  <span className="text-nest-500">&#10003;</span>
                  {plan.maxAgents} {plan.maxAgents === 1 ? "agent" : "agents"}
                </li>
                <li className="flex items-center gap-2">
                  <span className="text-nest-500">&#10003;</span>
                  {plan.maxSessions} concurrent sessions
                </li>
                {hasCoupon && (
                  <li className="flex items-center gap-2">
                    <span className="text-amber-500">&#10003;</span>
                    {freeDays}-day free trial
                  </li>
                )}
              </ul>

              {currentPlanId === plan.id && hasActiveSubscription ? (
                <div className="mt-6 flex w-full items-center justify-center gap-1.5 rounded-lg border border-green-300 bg-green-50 px-4 py-2.5 text-sm font-semibold text-green-700 dark:border-green-700 dark:bg-green-950/30 dark:text-green-400">
                  <Check className="h-4 w-4" />
                  Current plan
                </div>
              ) : (
                <button
                  onClick={() => handleSelect(plan.id)}
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
      <div className="mt-8 flex flex-col items-center gap-3">
        <p className="text-sm text-gray-500 dark:text-gray-400">Have a coupon code?</p>
        <div className="flex gap-2">
          <input
            type="text"
            value={couponCode}
            onChange={(e) => setCouponCode(e.target.value.toUpperCase())}
            placeholder="Enter coupon code"
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm dark:border-gray-700 dark:bg-gray-900 dark:text-white"
          />
          <button
            onClick={handleApplyCoupon}
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
                        {currentPlanId === plan.id && hasActiveSubscription ? (
                          <span className="inline-flex items-center gap-1 text-xs font-medium text-green-600 dark:text-green-400">
                            <Check className="h-3 w-3" />
                            Current
                          </span>
                        ) : (
                          <button
                            onClick={() => handleSelect(plan.id)}
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
    </div>
  );
}
