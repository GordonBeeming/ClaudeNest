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
  defaultCoupon?: { freeMonths: number } | null;
  sortOrder: number;
}

export interface AccountInfo {
  id: string;
  name: string;
  planId: string | null;
  planName: string | null;
  subscriptionStatus: SubscriptionStatus;
  hasBillingAccount: boolean;
  hasStripeSubscription: boolean;
  activeCoupon?: { code: string; freeUntil: string; planName: string } | null;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
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
  version: string | null;
  architecture: string | null;
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
  isAdmin: boolean;
  account: AccountInfo | null;
}

export interface FolderPreference {
  id: string;
  path: string;
  isFavorite: boolean;
  color: string | null;
  updatedAt: string;
}

export interface AgentCredentialInfo {
  id: string;
  issuedAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
  isActive: boolean;
}

export type LedgerEntryType = "PaymentDue" | "PaymentReceived" | "CouponCredit" | "CompanyDealCredit" | "Refund";

export interface LedgerEntry {
  id: string;
  entryType: LedgerEntryType;
  amountCents: number;
  currency: string;
  description: string;
  planId: string | null;
  stripeInvoiceId: string | null;
  couponId: string | null;
  companyDealId: string | null;
  createdAt: string;
}

export type DiscountType = "FreeMonths" | "PercentOff" | "AmountOff" | "FreeDays";

export interface CouponInfo {
  id: string;
  code: string;
  planId: string;
  planName: string;
  freeMonths: number;
  discountType: DiscountType;
  percentOff: number | null;
  amountOffCents: number | null;
  freeDays: number | null;
  durationMonths: number;
  maxRedemptions: number;
  timesRedeemed: number;
  expiresAt: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface CompanyDeal {
  id: string;
  domain: string;
  planId: string;
  planName: string;
  isActive: boolean;
  createdAt: string;
  deactivatedAt: string | null;
}

export interface AdminUserInfo {
  id: string;
  email: string;
  displayName: string | null;
  isAdmin: boolean;
  createdAt: string;
  accountId: string;
  accountName: string;
  planName: string | null;
  subscriptionStatus: SubscriptionStatus;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  hasBillingAccount: boolean;
  hasStripeSubscription: boolean;
  activeCoupon: { couponId: string; code: string; freeUntil: string } | null;
  companyDealDomain: string | null;
}

export interface CouponValidation {
  valid: boolean;
  couponId?: string;
  code?: string;
  planId?: string;
  planName?: string;
  freeMonths?: number;
  discountType?: DiscountType;
  percentOff?: number | null;
  amountOffCents?: number | null;
  freeDays?: number | null;
  durationMonths?: number;
  reason?: string;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function formatDiscountDescription(
  discountType: DiscountType,
  freeMonths?: number,
  percentOff?: number | null,
  amountOffCents?: number | null,
  freeDays?: number | null,
  durationMonths?: number,
): string {
  switch (discountType) {
    case "FreeMonths":
      return `${freeMonths ?? 0} month${(freeMonths ?? 0) !== 1 ? "s" : ""} free`;
    case "PercentOff":
      return `${percentOff ?? 0}% off for ${durationMonths ?? 0}mo`;
    case "AmountOff":
      return `$${((amountOffCents ?? 0) / 100).toFixed(0)} off/mo for ${durationMonths ?? 0}mo`;
    case "FreeDays":
      return `${freeDays ?? 0} days free`;
    default:
      return `${freeMonths ?? 0} months free`;
  }
}
