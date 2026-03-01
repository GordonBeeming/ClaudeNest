export type SessionState =
  | "Requested"
  | "Starting"
  | "Running"
  | "Stopping"
  | "Stopped"
  | "Crashed";

export type SubscriptionStatus = "None" | "Trialing" | "Active" | "PastDue" | "Cancelled";

export interface PlanInfo {
  id: string;
  name: string;
  maxAgents: number;
  maxSessions: number;
  priceCents: number;
  trialDays: number;
  sortOrder: number;
}

export interface AccountInfo {
  id: string;
  name: string;
  planId: string | null;
  planName: string | null;
  subscriptionStatus: SubscriptionStatus;
  trialEndsAt: string | null;
  maxAgents: number;
  maxSessions: number;
  agentCount: number;
  activeSessionCount: number;
  permissionMode: string;
}

export interface Agent {
  id: string;
  name: string | null;
  hostname: string | null;
  os: string | null;
  isOnline: boolean;
  lastSeenAt: string | null;
  createdAt: string;
  maxSessions: number;
  maxAgents: number;
  allowedPaths: string[];
}

export interface SessionStatus {
  sessionId: string;
  agentId: string;
  path: string;
  state: SessionState;
  pid: number | null;
  startedAt: string;
  endedAt: string | null;
  exitCode: number | null;
  errorMessage: string | null;
}

export interface DirectoryListingResult {
  requestId: string;
  path: string;
  directories: string[];
  error: string | null;
}

export interface UserProfile {
  id: string;
  email: string;
  displayName: string | null;
  account: AccountInfo | null;
}

export interface AgentCredentialInfo {
  id: string;
  issuedAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
  isActive: boolean;
}
