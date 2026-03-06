import { useEffect, type RefObject } from "react";

export function useClickOutside(
  ref: RefObject<HTMLElement | null>,
  handler: () => void,
  enabled = true,
) {
  useEffect(() => {
    if (!enabled) return;
    function onEvent(e: MouseEvent | TouchEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        handler();
      }
    }
    document.addEventListener("mousedown", onEvent);
    document.addEventListener("touchstart", onEvent);
    return () => {
      document.removeEventListener("mousedown", onEvent);
      document.removeEventListener("touchstart", onEvent);
    };
  }, [ref, handler, enabled]);
}
