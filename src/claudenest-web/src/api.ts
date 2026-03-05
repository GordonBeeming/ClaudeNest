import type { AdminUserInfo, Agent, UserProfile, PlanInfo, AccountInfo, AgentCredentialInfo, CouponValidation, LedgerEntry, PaginatedResult, CouponInfo, CompanyDeal, DiscountType, FolderPreference } from "./types";

const BASE = "/api";

let _getAccessToken: ((forceRefresh?: boolean) => Promise<string | undefined>) | null = null;
let _onAuthFailure: (() => void) | null = null;

export function setAccessTokenGetter(getter: (forceRefresh?: boolean) => Promise<string | undefined>) {
  _getAccessToken = getter;
}

export function setOnAuthFailure(handler: () => void) {
  _onAuthFailure = handler;
}

async function apiFetch<T>(
  path: string,
  options?: RequestInit,
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options?.headers as Record<string, string>),
  };

  // Attach Bearer token when Auth0 is configured
  if (_getAccessToken) {
    try {
      const token = await _getAccessToken();
      if (token) {
        headers["Authorization"] = `Bearer ${token}`;
      }
    } catch {
      // Token acquisition failed — proceed without token (dev mode will still work)
    }
  }

  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers,
  });

  if (res.status === 401 && _getAccessToken) {
    // Token may be expired — force refresh and retry once
    try {
      const freshToken = await _getAccessToken(true);
      if (freshToken) {
        headers["Authorization"] = `Bearer ${freshToken}`;
        const retryRes = await fetch(`${BASE}${path}`, { ...options, headers });
        if (retryRes.ok) {
          if (retryRes.status === 204) return undefined as T;
          return retryRes.json();
        }
        if (retryRes.status === 401) {
          _onAuthFailure?.();
        }
        const text = await retryRes.text();
        throw new Error(`API ${retryRes.status}: ${text}`);
      }
    } catch (refreshErr) {
      if (refreshErr instanceof Error && refreshErr.message.startsWith("API ")) throw refreshErr;
      _onAuthFailure?.();
    }
  }

  if (!res.ok) {
    const text = await res.text();
    throw new Error(`API ${res.status}: ${text}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return res.json();
}

export async function getMe(): Promise<UserProfile> {
  return apiFetch<UserProfile>("/me");
}

export async function getAgents(): Promise<Agent[]> {
  return apiFetch<Agent[]>("/agents");
}

export async function getAgent(agentId: string): Promise<Agent> {
  return apiFetch<Agent>(`/agents/${agentId}`);
}

export async function generatePairingToken(): Promise<{ token: string }> {
  return apiFetch<{ token: string }>("/pairing/generate", { method: "POST" });
}

export async function getPlans(): Promise<PlanInfo[]> {
  return apiFetch<PlanInfo[]>("/plans");
}

export async function selectPlan(planId: string, couponCode?: string): Promise<{ redirectUrl: string | null; action: string; account?: AccountInfo }> {
  return apiFetch<{ redirectUrl: string | null; action: string; account?: AccountInfo }>("/account/select-plan", {
    method: "POST",
    body: JSON.stringify({ planId, ...(couponCode ? { couponCode } : {}) }),
  });
}

export async function getAccount(): Promise<AccountInfo> {
  return apiFetch<AccountInfo>("/account");
}

export async function updateDisplayName(displayName: string): Promise<{ displayName: string }> {
  return apiFetch<{ displayName: string }>("/account/display-name", {
    method: "PUT",
    body: JSON.stringify({ displayName }),
  });
}

export async function getAgentCredentials(agentId: string): Promise<AgentCredentialInfo[]> {
  return apiFetch<AgentCredentialInfo[]>(`/agents/${agentId}/credentials`);
}

export async function rotateAgentSecret(agentId: string): Promise<{ credentialId: string; secret: string }> {
  return apiFetch<{ credentialId: string; secret: string }>(`/agents/${agentId}/rotate-secret`, {
    method: "POST",
  });
}

export async function deleteAgent(agentId: string): Promise<void> {
  return apiFetch<void>(`/agents/${agentId}`, {
    method: "DELETE",
  });
}

export async function triggerAgentUpdate(agentId: string): Promise<{ message: string }> {
  return apiFetch<{ message: string }>(`/agents/${agentId}/update`, { method: "POST" });
}

export async function getFolderPreferences(agentId: string): Promise<FolderPreference[]> {
  return apiFetch<FolderPreference[]>(`/agents/${agentId}/folder-preferences`);
}

export async function upsertFolderPreference(agentId: string, data: { path: string; isFavorite: boolean; color: string | null }): Promise<FolderPreference> {
  return apiFetch<FolderPreference>(`/agents/${agentId}/folder-preferences`, {
    method: "PUT",
    body: JSON.stringify(data),
  });
}

export async function deleteFolderPreference(agentId: string, preferenceId: string): Promise<void> {
  return apiFetch<void>(`/agents/${agentId}/folder-preferences/${preferenceId}`, {
    method: "DELETE",
  });
}

export async function revokeAgentCredential(agentId: string, credentialId: string): Promise<void> {
  return apiFetch<void>(`/agents/${agentId}/credentials/${credentialId}`, {
    method: "DELETE",
  });
}

export async function updatePermissionMode(mode: string): Promise<{ permissionMode: string }> {
  return apiFetch<{ permissionMode: string }>("/account/permission-mode", {
    method: "POST",
    body: JSON.stringify({ mode }),
  });
}

export interface LocalBuildAvailability {
  available: boolean;
  rids: string[];
  devWorkspacePath: string | null;
  source: string;
}

export async function getLocalBuildAvailability(): Promise<LocalBuildAvailability | null> {
  try {
    return await apiFetch<LocalBuildAvailability>("/agent-download/available");
  } catch {
    return null;
  }
}

export async function createBillingPortalSession(): Promise<{ url: string }> {
  return apiFetch<{ url: string }>("/account/billing-portal", { method: "POST" });
}

export async function redeemCoupon(code: string): Promise<CouponValidation> {
  return apiFetch<CouponValidation>("/account/redeem-coupon", {
    method: "POST",
    body: JSON.stringify({ code }),
  });
}

export async function getLedger(page = 1, pageSize = 20): Promise<PaginatedResult<LedgerEntry>> {
  return apiFetch<PaginatedResult<LedgerEntry>>(`/account/ledger?page=${page}&pageSize=${pageSize}`);
}

// Admin APIs

export async function getAdminCoupons(): Promise<CouponInfo[]> {
  return apiFetch<CouponInfo[]>("/admin/coupons");
}

export async function createAdminCoupon(data: {
  code: string;
  planId: string;
  freeMonths: number;
  maxRedemptions: number;
  expiresAt?: string;
  discountType: DiscountType;
  percentOff?: number | null;
  amountOffCents?: number | null;
  freeDays?: number | null;
  durationMonths?: number | null;
}): Promise<CouponInfo> {
  return apiFetch<CouponInfo>("/admin/coupons", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export async function updateAdminCoupon(id: string, data: { maxRedemptions?: number; expiresAt?: string | null; isActive?: boolean }): Promise<CouponInfo> {
  return apiFetch<CouponInfo>(`/admin/coupons/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

export async function deleteAdminCoupon(id: string): Promise<void> {
  return apiFetch<void>(`/admin/coupons/${id}`, { method: "DELETE" });
}

export async function getAdminCompanyDeals(): Promise<CompanyDeal[]> {
  return apiFetch<CompanyDeal[]>("/admin/company-deals");
}

export async function createAdminCompanyDeal(data: { domain: string; planId: string }): Promise<CompanyDeal> {
  return apiFetch<CompanyDeal>("/admin/company-deals", {
    method: "POST",
    body: JSON.stringify(data),
  });
}

export async function updateAdminCompanyDeal(id: string, data: { planId: string }): Promise<CompanyDeal> {
  return apiFetch<CompanyDeal>(`/admin/company-deals/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
}

export async function deactivateAdminCompanyDeal(id: string): Promise<void> {
  return apiFetch<void>(`/admin/company-deals/${id}/deactivate`, { method: "POST" });
}

export async function getAdminUsers(filters?: { domain?: string; hasCoupon?: boolean; page?: number; pageSize?: number }): Promise<PaginatedResult<AdminUserInfo>> {
  const params = new URLSearchParams();
  if (filters?.domain) params.set("domain", filters.domain);
  if (filters?.hasCoupon !== undefined) params.set("hasCoupon", String(filters.hasCoupon));
  if (filters?.page) params.set("page", String(filters.page));
  if (filters?.pageSize) params.set("pageSize", String(filters.pageSize));
  const qs = params.toString();
  return apiFetch<PaginatedResult<AdminUserInfo>>(`/admin/users${qs ? `?${qs}` : ""}`);
}

export async function adminCancelSubscription(userId: string): Promise<AdminUserInfo> {
  return apiFetch<AdminUserInfo>(`/admin/users/${userId}/cancel-subscription`, { method: "POST" });
}

export async function adminToggleAdmin(userId: string): Promise<AdminUserInfo> {
  return apiFetch<AdminUserInfo>(`/admin/users/${userId}/toggle-admin`, { method: "POST" });
}

export async function adminGiveCoupon(userId: string, couponId: string): Promise<AdminUserInfo> {
  return apiFetch<AdminUserInfo>(`/admin/users/${userId}/give-coupon`, {
    method: "POST",
    body: JSON.stringify({ couponId }),
  });
}

export async function adminOverridePlan(userId: string, planId: string): Promise<AdminUserInfo> {
  return apiFetch<AdminUserInfo>(`/admin/users/${userId}/override-plan`, {
    method: "POST",
    body: JSON.stringify({ planId }),
  });
}

export async function adminRevertPlan(userId: string): Promise<AdminUserInfo> {
  return apiFetch<AdminUserInfo>(`/admin/users/${userId}/revert-plan`, { method: "POST" });
}
