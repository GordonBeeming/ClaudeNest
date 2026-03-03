import { useState } from "react";
import { AlertTriangle } from "lucide-react";
import { useUserContext } from "../contexts/UserContext";
import { createBillingPortalSession } from "../api";

export function PastDueBanner() {
  const { user } = useUserContext();
  const [redirecting, setRedirecting] = useState(false);

  if (user?.account?.subscriptionStatus !== "PastDue") return null;

  const handleUpdatePayment = async () => {
    setRedirecting(true);
    try {
      const { url } = await createBillingPortalSession();
      window.location.href = url;
    } catch {
      setRedirecting(false);
    }
  };

  return (
    <div className="mb-4 rounded-lg border border-amber-300 bg-amber-50 px-4 py-3 dark:border-amber-700 dark:bg-amber-950/30">
      <div className="flex items-center gap-3">
        <AlertTriangle className="h-5 w-5 flex-shrink-0 text-amber-600 dark:text-amber-400" />
        <p className="flex-1 text-sm text-amber-800 dark:text-amber-200">
          Your payment is past due. Update your payment method to continue using ClaudeNest.
        </p>
        <button
          onClick={handleUpdatePayment}
          disabled={redirecting}
          className="flex-shrink-0 rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-amber-700 disabled:opacity-50"
        >
          {redirecting ? "Redirecting..." : "Update Payment"}
        </button>
      </div>
    </div>
  );
}
