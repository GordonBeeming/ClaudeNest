import { useEffect, useRef } from "react";

export function JsonLd({ data }: { data: Record<string, unknown> }) {
  const scriptRef = useRef<HTMLScriptElement | null>(null);

  useEffect(() => {
    const script = document.createElement("script");
    script.type = "application/ld+json";
    script.textContent = JSON.stringify(data);
    document.head.appendChild(script);
    scriptRef.current = script;

    return () => {
      script.remove();
    };
  }, [data]);

  return null;
}
