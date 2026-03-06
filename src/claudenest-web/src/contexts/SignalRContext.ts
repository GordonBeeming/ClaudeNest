import { createContext, useContext } from "react";
import type { useSignalR } from "../hooks/useSignalR";
import type { AdminAgentSummary } from "../types";

type SignalRContextType = ReturnType<typeof useSignalR> & {
  adminAgentSummary: AdminAgentSummary | null;
};

export const SignalRContext = createContext<SignalRContextType | null>(null);

export function useSignalRContext(): SignalRContextType {
  const ctx = useContext(SignalRContext);
  if (!ctx) throw new Error("useSignalRContext must be used within Layout");
  return ctx;
}
