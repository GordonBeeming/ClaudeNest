import type { Agent, UserProfile, PlanInfo, AccountInfo, AgentCredentialInfo } from "./types";

const BASE = "/api";

let _getAccessToken: (() => Promise<string | undefined>) | null = null;

export function setAccessTokenGetter(getter: () => Promise<string | undefined>) {
  _getAccessToken = getter;
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

export async function selectPlan(planId: string): Promise<AccountInfo> {
  return apiFetch<AccountInfo>("/account/select-plan", {
    method: "POST",
    body: JSON.stringify({ planId }),
  });
}

export async function getAccount(): Promise<AccountInfo> {
  return apiFetch<AccountInfo>("/account");
}

export async function getAgentCredentials(agentId: string): Promise<AgentCredentialInfo[]> {
  return apiFetch<AgentCredentialInfo[]>(`/agents/${agentId}/credentials`);
}

export async function rotateAgentSecret(agentId: string): Promise<{ credentialId: string; secret: string }> {
  return apiFetch<{ credentialId: string; secret: string }>(`/agents/${agentId}/rotate-secret`, {
    method: "POST",
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
}

export async function getLocalBuildAvailability(): Promise<LocalBuildAvailability | null> {
  try {
    return await apiFetch<LocalBuildAvailability>("/agent-download/available");
  } catch {
    return null;
  }
}
