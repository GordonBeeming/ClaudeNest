import { useState, useRef, useEffect } from "react";
import { Link, Outlet, useNavigate } from "react-router-dom";
import { useAuth0 } from "@auth0/auth0-react";
import { Bird, Wifi, WifiOff, LogOut, Settings, ChevronDown, Shield, UsersRound, Tag, Handshake } from "lucide-react";
import { useSignalR } from "../hooks/useSignalR";
import { SignalRContext } from "../contexts/SignalRContext";
import { useUserContext } from "../contexts/UserContext";
import { isAuth0Configured } from "../config";
import { PastDueBanner } from "./PastDueBanner";

/** Sign out button — only rendered when Auth0 is active (inside Auth0Provider). */
function Auth0SignOutButton({ onClose }: { onClose: () => void }) {
  const { logout } = useAuth0();

  return (
    <button
      onClick={() => {
        onClose();
        logout({ logoutParams: { returnTo: window.location.origin } });
      }}
      className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
    >
      <LogOut className="h-4 w-4" />
      Sign out
    </button>
  );
}

/** Compact sign-out for the header — shown when Auth0 is active but user profile hasn't loaded. */
function Auth0HeaderSignOut() {
  const { logout, isAuthenticated } = useAuth0();

  if (!isAuthenticated) return null;

  return (
    <button
      onClick={() => logout({ logoutParams: { returnTo: window.location.origin } })}
      className="flex items-center gap-1.5 rounded-lg px-2 py-1 text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20 transition-colors"
    >
      <LogOut className="h-4 w-4" />
      <span className="hidden sm:inline">Sign out</span>
    </button>
  );
}

function UserMenu() {
  const { user, isAdmin } = useUserContext();
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    if (open) document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, [open]);

  const displayName = user?.displayName || user?.email || "User";
  const initials = displayName
    .split(/\s+/)
    .map((w) => w[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <div ref={menuRef} className="relative">
      <button
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-2 rounded-lg px-2 py-1 text-sm text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800 transition-colors"
      >
        <span className="flex h-7 w-7 items-center justify-center rounded-full bg-nest-100 text-xs font-semibold text-nest-700 dark:bg-nest-900/50 dark:text-nest-300">
          {initials}
        </span>
        <span className="hidden sm:inline max-w-[120px] truncate">{displayName}</span>
        <ChevronDown className="h-3.5 w-3.5" />
      </button>

      {open && (
        <div className="absolute right-0 mt-1 w-56 rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-900 z-50">
          <div className="border-b border-gray-100 px-4 py-2.5 dark:border-gray-800">
            <p className="text-sm font-medium text-gray-900 dark:text-white truncate">
              {displayName}
            </p>
            {user?.email && user.email !== displayName && (
              <p className="text-xs text-gray-500 dark:text-gray-400 truncate">{user.email}</p>
            )}
            {user?.account?.planName && (
              <span className="mt-1.5 inline-block rounded-full bg-nest-100 px-2 py-0.5 text-xs font-medium text-nest-700 dark:bg-nest-900/50 dark:text-nest-300">
                {user.account.planName}
              </span>
            )}
          </div>

          <button
            onClick={() => { setOpen(false); navigate("/account"); }}
            className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            <Settings className="h-4 w-4" />
            Account
          </button>

          {isAdmin && (
            <>
              <div className="border-t border-gray-100 dark:border-gray-800 my-1" />
              <div className="px-4 py-1.5">
                <span className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-amber-600 dark:text-amber-400">
                  <Shield className="h-3 w-3" />
                  Admin
                </span>
              </div>
              <button
                onClick={() => { setOpen(false); navigate("/admin/users"); }}
                className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <UsersRound className="h-4 w-4" />
                Users
              </button>
              <button
                onClick={() => { setOpen(false); navigate("/admin/coupons"); }}
                className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <Tag className="h-4 w-4" />
                Coupons
              </button>
              <button
                onClick={() => { setOpen(false); navigate("/admin/company-deals"); }}
                className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:text-gray-300 dark:hover:bg-gray-800"
              >
                <Handshake className="h-4 w-4" />
                Deals
              </button>
            </>
          )}

          {isAuth0Configured && <Auth0SignOutButton onClose={() => setOpen(false)} />}
        </div>
      )}
    </div>
  );
}

export function Layout() {
  const signalR = useSignalR();
  const { user } = useUserContext();

  return (
    <SignalRContext.Provider value={signalR}>
      <div className="min-h-screen">
        <header className="sticky top-0 z-10 border-b border-gray-200 bg-white/80 backdrop-blur-sm dark:border-gray-800 dark:bg-gray-950/80">
          <div className="mx-auto flex h-14 max-w-5xl items-center justify-between px-4">
            <Link
              to="/"
              className="flex items-center gap-2 text-lg font-semibold text-gray-900 dark:text-white"
            >
              <Bird className="h-6 w-6 text-nest-500" />
              <span>ClaudeNest</span>
            </Link>

            <div className="flex items-center gap-3">
              {signalR.connected ? (
                <span className="flex items-center gap-1.5 text-xs text-green-600 dark:text-green-400">
                  <Wifi className="h-3.5 w-3.5" />
                  Live
                </span>
              ) : (
                <span className="flex items-center gap-1.5 text-xs text-gray-400">
                  <WifiOff className="h-3.5 w-3.5" />
                  Connecting...
                </span>
              )}

              {user ? <UserMenu /> : isAuth0Configured && <Auth0HeaderSignOut />}
            </div>
          </div>
        </header>

        <main className="mx-auto max-w-5xl px-4 py-6">
          <PastDueBanner />
          <Outlet />
        </main>
      </div>
    </SignalRContext.Provider>
  );
}
