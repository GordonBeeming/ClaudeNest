import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { Auth0Provider, useAuth0 } from "@auth0/auth0-react";
import { Layout } from "./components/Layout";
import { Dashboard } from "./pages/Dashboard";
import { AgentDetail } from "./pages/AgentDetail";
import { PlanSelection } from "./pages/PlanSelection";
import { AccountPage } from "./pages/AccountPage";
import { HomePage } from "./pages/HomePage";
import { PrivacyPolicy } from "./pages/PrivacyPolicy";
import { CouponManagement } from "./pages/admin/CouponManagement";
import { CompanyDeals } from "./pages/admin/CompanyDeals";
import { Users } from "./pages/admin/Users";
import { UserProvider, useUserContext } from "./contexts/UserContext";
import { RefreshCw } from "lucide-react";
import { auth0Domain, auth0ClientId, auth0Audience, isAuth0Configured } from "./config";
import { setCouponIntent } from "./utils/couponIntent";

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth0();

  if (isLoading) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <RefreshCw className="h-6 w-6 animate-spin text-nest-500" />
        <p className="text-sm text-gray-500">Authenticating...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

function RequirePlan({ children }: { children: React.ReactNode }) {
  const { user, loading } = useUserContext();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  if (!user?.account?.planId) {
    return <Navigate to="/plans" replace />;
  }

  return <>{children}</>;
}

function RequireAdmin({ children }: { children: React.ReactNode }) {
  const { isAdmin, loading } = useUserContext();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  if (!isAdmin) {
    return <Navigate to="/dashboard" replace />;
  }

  return <>{children}</>;
}

/** Smart wrapper for /plans: redirect authenticated users with active sub to dashboard */
function SmartPlanSelection() {
  const { user, loading } = useUserContext();

  if (loading) {
    return (
      <div className="flex items-center justify-center py-20">
        <RefreshCw className="h-5 w-5 animate-spin text-gray-400" />
      </div>
    );
  }

  const hasActiveSub =
    user?.account?.hasStripeSubscription &&
    (user.account.subscriptionStatus === "Active" ||
      user.account.subscriptionStatus === "Trialing" ||
      user.account.subscriptionStatus === "PastDue") &&
    !user.account.cancelAtPeriodEnd;

  if (user?.account?.planId && hasActiveSub) {
    return <Navigate to="/dashboard" replace />;
  }

  return <PlanSelection />;
}

function AuthenticatedRoutes() {
  return (
    <UserProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/plans" element={<SmartPlanSelection />} />
          <Route
            path="/account"
            element={
              <RequirePlan>
                <AccountPage />
              </RequirePlan>
            }
          />
          <Route
            path="/dashboard"
            element={
              <RequirePlan>
                <Dashboard />
              </RequirePlan>
            }
          />
          <Route
            path="/agents/:agentId"
            element={
              <RequirePlan>
                <AgentDetail />
              </RequirePlan>
            }
          />
          <Route
            path="/admin/users"
            element={
              <RequireAdmin>
                <Users />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/coupons"
            element={
              <RequireAdmin>
                <CouponManagement />
              </RequireAdmin>
            }
          />
          <Route
            path="/admin/company-deals"
            element={
              <RequireAdmin>
                <CompanyDeals />
              </RequireAdmin>
            }
          />
        </Route>
      </Routes>
    </UserProvider>
  );
}

/** Capture ?coupon= from any URL, persist to localStorage, and strip from URL */
function CouponCapture() {
  const params = new URLSearchParams(window.location.search);
  const coupon = params.get("coupon");
  if (coupon) {
    setCouponIntent(coupon);
    params.delete("coupon");
    const newSearch = params.toString();
    const newUrl = window.location.pathname + (newSearch ? `?${newSearch}` : "") + window.location.hash;
    window.history.replaceState({}, "", newUrl);
  }
  return null;
}

function AppRoutes() {
  return (
    <Routes>
      <Route index element={<HomePage />} />
      <Route path="/privacy" element={<PrivacyPolicy />} />
      {isAuth0Configured ? (
        <Route
          path="/*"
          element={
            <RequireAuth>
              <AuthenticatedRoutes />
            </RequireAuth>
          }
        />
      ) : (
        <Route path="/*" element={<AuthenticatedRoutes />} />
      )}
    </Routes>
  );
}

export default function App() {
  const inner = (
    <BrowserRouter>
      <CouponCapture />
      {isAuth0Configured ? (
        <Auth0Provider
          domain={auth0Domain}
          clientId={auth0ClientId}
          cacheLocation="localstorage"
          useRefreshTokens={true}
          authorizationParams={{
            redirect_uri: window.location.origin,
            audience: auth0Audience,
          }}
        >
          <AppRoutes />
        </Auth0Provider>
      ) : (
        <AppRoutes />
      )}
    </BrowserRouter>
  );

  return inner;
}
