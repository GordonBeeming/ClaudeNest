const KEY = "claudenest_selected_plan";

export function setPlanIntent(planId: string) {
  localStorage.setItem(KEY, planId);
}

export function getPlanIntent(): string | null {
  return localStorage.getItem(KEY);
}

export function clearPlanIntent() {
  localStorage.removeItem(KEY);
}
