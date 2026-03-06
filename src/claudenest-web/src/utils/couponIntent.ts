const CODE_KEY = "claudenest_coupon_code";
const TIME_KEY = "claudenest_coupon_time";
const EXPIRY_MS = 5 * 60 * 1000; // 5 minutes

export function setCouponIntent(code: string) {
  localStorage.setItem(CODE_KEY, code);
  localStorage.setItem(TIME_KEY, Date.now().toString());
}

export function getCouponIntent(): string | null {
  const code = localStorage.getItem(CODE_KEY);
  if (!code) return null;

  const savedAt = Number(localStorage.getItem(TIME_KEY) || "0");
  if (Date.now() - savedAt > EXPIRY_MS) {
    clearCouponIntent();
    return null;
  }

  return code;
}

export function clearCouponIntent() {
  localStorage.removeItem(CODE_KEY);
  localStorage.removeItem(TIME_KEY);
}
