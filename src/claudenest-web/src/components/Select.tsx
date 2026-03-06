import { useState, useEffect, useRef, useCallback } from "react";
import { ChevronDown, Check } from "lucide-react";
import { clsx } from "clsx";
import { useClickOutside } from "../hooks/useClickOutside";

export interface SelectOption {
  value: string;
  label: string;
  description?: string;
}

interface SelectProps {
  options: SelectOption[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
}

export function Select({ options, value, onChange, placeholder = "Select...", className }: SelectProps) {
  const [open, setOpen] = useState(false);
  const [dropUp, setDropUp] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  const selected = options.find((o) => o.value === value);

  const closeDropdown = useCallback(() => setOpen(false), []);
  useClickOutside(ref, closeDropdown, open);

  useEffect(() => {
    if (!open) return;
    function handleKey(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", handleKey);
    return () => document.removeEventListener("keydown", handleKey);
  }, [open]);

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
    <div ref={ref} className={clsx("relative", className)}>
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
          <span className="truncate">{selected.label}</span>
        ) : (
          <span className="text-gray-400 dark:text-gray-500">{placeholder}</span>
        )}
        <ChevronDown className={clsx("ml-2 h-4 w-4 shrink-0 text-gray-400 transition-transform", open && "rotate-180")} />
      </button>

      {open && (
        <div className={clsx(
          "absolute z-50 max-h-60 w-full overflow-auto rounded-lg border border-gray-200 bg-white py-1 shadow-lg dark:border-gray-700 dark:bg-gray-900",
          dropUp ? "bottom-full mb-1" : "top-full mt-1",
        )}>
          {options.map((option) => (
            <button
              key={option.value}
              type="button"
              onClick={() => {
                onChange(option.value);
                setOpen(false);
              }}
              className={clsx(
                "flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition-colors",
                option.value === value
                  ? "bg-nest-50 dark:bg-nest-950/30"
                  : "hover:bg-gray-50 dark:hover:bg-gray-800"
              )}
            >
              <div className="min-w-0 flex-1">
                <span className="font-medium text-gray-900 dark:text-white">{option.label}</span>
                {option.description && (
                  <p className="text-xs text-gray-500 dark:text-gray-400">{option.description}</p>
                )}
              </div>
              {option.value === value && (
                <Check className="h-4 w-4 shrink-0 text-nest-500" />
              )}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
