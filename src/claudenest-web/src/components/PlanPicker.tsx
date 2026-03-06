import { useState, useRef, useCallback } from "react";
import { ChevronDown } from "lucide-react";
import { clsx } from "clsx";
import { useClickOutside } from "../hooks/useClickOutside";
import type { PlanInfo } from "../types";

interface PlanPickerProps {
  plans: PlanInfo[];
  value: string;
  onChange: (planId: string) => void;
  required?: boolean;
}

export function PlanPicker({ plans, value, onChange, required }: PlanPickerProps) {
  const [open, setOpen] = useState(false);
  const [dropUp, setDropUp] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const selected = plans.find((p) => p.id === value);

  const closeDropdown = useCallback(() => setOpen(false), []);
  useClickOutside(ref, closeDropdown, open);

  const handleToggle = useCallback(() => {
    setOpen((o) => {
      if (!o && ref.current) {
        const rect = ref.current.getBoundingClientRect();
        setDropUp(rect.bottom + 240 > window.innerHeight);
      }
      return !o;
    });
  }, []);

  return (
    <div ref={ref} className="relative">
      <input type="hidden" value={value} required={required} />
      <button
        type="button"
        onClick={handleToggle}
        className={clsx(
          "flex w-full items-center justify-between rounded-lg border px-3 py-2 text-left text-sm transition-colors",
          "border-gray-300 bg-white text-gray-900 hover:border-gray-400",
          "focus:border-nest-500 focus:outline-none focus:ring-1 focus:ring-nest-500",
          "dark:border-gray-700 dark:bg-gray-800 dark:text-white dark:hover:border-gray-600"
        )}
      >
        {selected ? (
          <span>
            <span className="font-medium">{selected.name}</span>
            <span className="ml-2 text-gray-500 dark:text-gray-400">
              ${(selected.priceCents / 100).toFixed(0)} AUD/mo
            </span>
          </span>
        ) : (
          <span className="text-gray-400 dark:text-gray-500">Select a plan...</span>
        )}
        <ChevronDown className={clsx("h-4 w-4 text-gray-400 transition-transform", open && "rotate-180")} />
      </button>

      {open && (
        <div className={clsx(
          "absolute z-50 w-full rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-900",
          dropUp ? "bottom-full mb-1" : "top-full mt-1",
        )}>
          {plans.map((plan) => (
            <button
              key={plan.id}
              type="button"
              onClick={() => {
                onChange(plan.id);
                setOpen(false);
              }}
              className={clsx(
                "flex w-full flex-col px-3 py-2 text-left text-sm transition-colors",
                plan.id === value
                  ? "bg-nest-50 dark:bg-nest-950/30"
                  : "hover:bg-gray-50 dark:hover:bg-gray-800"
              )}
            >
              <span className="font-medium text-gray-900 dark:text-white">{plan.name}</span>
              <span className="text-xs text-gray-500 dark:text-gray-400">
                ${(plan.priceCents / 100).toFixed(0)} AUD/mo &middot; {plan.maxAgents} {plan.maxAgents === 1 ? "agent" : "agents"} &middot; {plan.maxSessions} {plan.maxSessions === 1 ? "session" : "sessions"}
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
