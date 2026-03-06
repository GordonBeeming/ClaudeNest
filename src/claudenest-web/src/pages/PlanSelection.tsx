import { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { Bird } from "lucide-react";
import { clsx } from "clsx";
import { getPlans, selectPlan, redeemCoupon, getCouponByCode } from "../api";
import { useUserContext } from "../contexts/UserContext";
import { PricingCards } from "../components/PricingCards";
import { getPlanIntent, clearPlanIntent } from "../utils/planIntent";
import { getCouponIntent, clearCouponIntent } from "../utils/couponIntent";
import type { PlanInfo, CouponValidation } from "../types";

export function PlanSelection() {
  const { user } = useUserContext();
  const hasActiveSubscription = user?.account?.hasStripeSubscription &&
    (user.account.subscriptionStatus === "Active" || user.account.subscriptionStatus === "Trialing" || user.account.subscriptionStatus === "PastDue") &&
    !user.account.cancelAtPeriodEnd;
  const currentPlanId = user?.account?.planId;
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [selecting, setSelecting] = useState<string | null>(null);
  const [couponCode, setCouponCode] = useState("");
  const [couponResult, setCouponResult] = useState<CouponValidation | null>(null);
  const [couponLoading, setCouponLoading] = useState(false);
  const [offerCoupon, setOfferCoupon] = useState<CouponValidation | null>(null);
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { updateAccount, refreshUser } = useUserContext();
  const intentHandled = useRef(false);

  useEffect(() => {
    if (searchParams.get("success") === "true") {
      setSearchParams({}, { replace: true });
      refreshUser().then(() => navigate("/dashboard", { replace: true }));
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

  // Load coupon from localStorage (saved by CouponCapture from ?coupon=CODE)
  useEffect(() => {
    const couponParam = getCouponIntent();
    if (couponParam) {
      clearCouponIntent();
      getCouponByCode(couponParam)
        .then((result) => {
          if (result.valid) {
            setOfferCoupon(result);
            setCouponCode(result.code ?? couponParam.toUpperCase());
            setCouponResult(result);
          }
        })
        .catch(() => {
          // Silently ignore invalid coupon links
        });
    }
  }, []);

  const handleSelect = useCallback(async (planId: string) => {
    setSelecting(planId);
    try {
      // If selecting a plan that doesn't match the offer coupon, clear coupon state
      const couponMatchesPlan = couponResult?.valid && couponResult.planId === planId;
      const validCoupon = couponMatchesPlan ? couponResult.code : undefined;
      const result = await selectPlan(planId, validCoupon);
      if (result.action === "redirect" && result.redirectUrl) {
        window.location.href = result.redirectUrl;
      } else if (result.action === "local" && result.account) {
        updateAccount(result.account);
        navigate("/dashboard");
      } else {
        setSelecting(null);
      }
    } catch {
      setSelecting(null);
    }
  }, [couponResult, navigate, updateAccount]);

  // Auto-select plan from localStorage intent (set by homepage)
  useEffect(() => {
    if (intentHandled.current || loading || plans.length === 0) return;
    const intentPlanId = getPlanIntent();
    if (intentPlanId) {
      intentHandled.current = true;
      clearPlanIntent();
      const matchingPlan = plans.find((p) => p.id === intentPlanId);
      if (matchingPlan) {
        handleSelect(intentPlanId);
      }
    }
  }, [loading, plans, handleSelect]);

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

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <div className="h-5 w-5 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" role="status" aria-label="Loading plans" />
      </div>
    );
  }

  return (
    <div className="py-8">
      <div className="text-center">
        <Bird className="mx-auto h-12 w-12 text-nest-500" aria-hidden="true" />
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
          role="status"
        >
          {message.text}
        </div>
      )}

      <div className="mt-10">
        <PricingCards
          plans={plans}
          onSelectPlan={handleSelect}
          selecting={selecting}
          currentPlanId={currentPlanId}
          hasActiveSubscription={!!hasActiveSubscription}
          showCouponInput
          couponCode={couponCode}
          onCouponCodeChange={setCouponCode}
          couponResult={couponResult}
          onApplyCoupon={handleApplyCoupon}
          couponLoading={couponLoading}
          offerCoupon={offerCoupon}
        />
      </div>
      <div className="mt-6 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
        <strong>Prerequisite:</strong> ClaudeNest uses Anthropic's{" "}
        <a
          href="https://code.claude.com/docs/en/remote-control"
          target="_blank"
          rel="noopener noreferrer"
          className="underline hover:text-amber-900 dark:hover:text-amber-200"
        >
          remote-control
        </a>{" "}
        feature, which requires a{" "}
        <a
          href="https://claude.ai/settings/billing"
          target="_blank"
          rel="noopener noreferrer"
          className="font-semibold underline hover:text-amber-900 dark:hover:text-amber-200"
        >
          Claude Max subscription
        </a>{" "}
        (separate from ClaudeNest).
      </div>
    </div>
  );
}
