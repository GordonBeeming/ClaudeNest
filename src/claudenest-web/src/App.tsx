import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { Auth0Provider, useAuth0 } from "@auth0/auth0-react";
import { Layout } from "./components/Layout";
import { Dashboard } from "./pages/Dashboard";
import { AgentDetail } from "./pages/AgentDetail";
import { PlanSelection } from "./pages/PlanSelection";
import { AccountPage } from "./pages/AccountPage";
import { CouponManagement } from "./pages/admin/CouponManagement";
import { CompanyDeals } from "./pages/admin/CompanyDeals";
import { Users } from "./pages/admin/Users";
import { UserProvider, useUserContext } from "./contexts/UserContext";
import { RefreshCw, Bird } from "lucide-react";
import { auth0Domain, auth0ClientId, auth0Audience, isAuth0Configured } from "./config";

function RequireAuth({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading, loginWithRedirect } = useAuth0();

  if (isLoading) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <RefreshCw className="h-6 w-6 animate-spin text-nest-500" />
        <p className="text-sm text-gray-500">Authenticating...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-6">
        <Bird className="h-16 w-16 text-nest-500" />
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            Welcome to ClaudeNest
          </h1>
          <p className="mt-2 text-gray-500 dark:text-gray-400">
            Sign in to manage your agents and sessions.
          </p>
        </div>
        <button
          onClick={() => loginWithRedirect()}
          className="rounded-lg bg-nest-500 px-6 py-2.5 text-sm font-semibold text-white hover:bg-nest-600 transition-colors"
        >
          Sign in
        </button>
      </div>
    );
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
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

function AppRoutes() {
  return (
    <UserProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/plans" element={<PlanSelection />} />
          <Route
            path="/account"
            element={
              <RequirePlan>
                <AccountPage />
              </RequirePlan>
            }
          />
          <Route
            index
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

export default function App() {
  const inner = (
    <BrowserRouter>
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
          <RequireAuth>
            <AppRoutes />
          </RequireAuth>
        </Auth0Provider>
      ) : (
        <AppRoutes />
      )}
    </BrowserRouter>
  );

  return inner;
}
