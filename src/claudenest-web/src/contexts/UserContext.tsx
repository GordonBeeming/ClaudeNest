import { createContext, useContext, useState, useEffect, useCallback, type ReactNode } from "react";
import { useAuth0 } from "@auth0/auth0-react";
import { getMe, setAccessTokenGetter } from "../api";
import type { UserProfile, AccountInfo } from "../types";
import { auth0Audience, isAuth0Configured } from "../config";

interface UserContextValue {
  user: UserProfile | null;
  loading: boolean;
  refreshUser: () => Promise<void>;
  updateAccount: (account: AccountInfo) => void;
  isAdmin: boolean;
}

const UserContext = createContext<UserContextValue | null>(null);

/** Wires Auth0 access token into the API layer. Renders nothing. */
function Auth0TokenBridge() {
  const { getAccessTokenSilently } = useAuth0();

  useEffect(() => {
    setAccessTokenGetter(async () => {
      return await getAccessTokenSilently({ authorizationParams: { audience: auth0Audience } });
    });
  }, [getAccessTokenSilently]);

  return null;
}

function UserProviderInner({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchUser = useCallback(async () => {
    try {
      const data = await getMe();
      setUser(data);
    } catch {
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchUser();
  }, [fetchUser]);

  const refreshUser = useCallback(async () => {
    await fetchUser();
  }, [fetchUser]);

  const updateAccount = useCallback((account: AccountInfo) => {
    setUser((prev) => prev ? { ...prev, account } : prev);
  }, []);

  const isAdmin = user?.isAdmin ?? false;

  return (
    <UserContext.Provider value={{ user, loading, refreshUser, updateAccount, isAdmin }}>
      {children}
    </UserContext.Provider>
  );
}

export function UserProvider({ children }: { children: ReactNode }) {
  return (
    <UserProviderInner>
      {isAuth0Configured && <Auth0TokenBridge />}
      {children}
    </UserProviderInner>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useUserContext() {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUserContext must be used within UserProvider");
  return ctx;
}
