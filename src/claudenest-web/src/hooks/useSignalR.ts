import { useEffect, useRef, useState, useCallback, useMemo } from "react";
import {
  HubConnectionBuilder,
  HubConnection,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import type { AdminAgentSummary, DirectoryListingResult, SessionStatus } from "../types";
import { getAccessToken } from "../api";

export function useSignalR() {
  const connectionRef = useRef<HubConnection | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl("/hubs/nest", {
        accessTokenFactory: getAccessToken,
      })
      .withAutomaticReconnect([0, 1000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Information)
      .build();

    connectionRef.current = connection;

    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection
      .start()
      .then(() => setConnected(true))
      .catch((err) => console.error("SignalR connection failed:", err));

    return () => {
      connection.stop();
    };
  }, []);

  const subscribeToAgent = useCallback((agentId: string) => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      conn.invoke("SubscribeToAgent", agentId);
    }
  }, []);

  const requestDirectoryListing = useCallback(
    (agentId: string, path: string) => {
      const conn = connectionRef.current;
      if (conn?.state === HubConnectionState.Connected) {
        conn.invoke("RequestDirectoryListing", agentId, path);
      }
    },
    [],
  );

  const requestStartSession = useCallback(
    (agentId: string, sessionId: string, path: string) => {
      const conn = connectionRef.current;
      if (conn?.state === HubConnectionState.Connected) {
        conn.invoke("RequestStartSession", agentId, sessionId, path);
      }
    },
    [],
  );

  const requestStopSession = useCallback(
    (agentId: string, sessionId: string) => {
      const conn = connectionRef.current;
      if (conn?.state === HubConnectionState.Connected) {
        conn.invoke("RequestStopSession", agentId, sessionId);
      }
    },
    [],
  );

  const requestGetSessions = useCallback((agentId: string) => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      conn.invoke("RequestGetSessions", agentId);
    }
  }, []);

  const onAgentStatusChanged = useCallback(
    (handler: (agentId: string, isOnline: boolean) => void) => {
      connectionRef.current?.on("AgentStatusChanged", handler);
      return () => connectionRef.current?.off("AgentStatusChanged", handler);
    },
    [],
  );

  const onSessionStatusChanged = useCallback(
    (handler: (update: SessionStatus) => void) => {
      connectionRef.current?.on("SessionStatusChanged", handler);
      return () =>
        connectionRef.current?.off("SessionStatusChanged", handler);
    },
    [],
  );

  const onDirectoryListingResult = useCallback(
    (handler: (result: DirectoryListingResult) => void) => {
      connectionRef.current?.on("DirectoryListingResult", handler);
      return () =>
        connectionRef.current?.off("DirectoryListingResult", handler);
    },
    [],
  );

  const onAllSessionsUpdated = useCallback(
    (handler: (agentId: string, sessions: SessionStatus[]) => void) => {
      connectionRef.current?.on("AllSessionsUpdated", handler);
      return () =>
        connectionRef.current?.off("AllSessionsUpdated", handler);
    },
    [],
  );

  const onAgentRemoved = useCallback(
    (handler: (agentId: string) => void) => {
      connectionRef.current?.on("AgentRemoved", handler);
      return () => connectionRef.current?.off("AgentRemoved", handler);
    },
    [],
  );

  const onAgentUpdateStatus = useCallback(
    (handler: (report: { agentId: string; status: string; error?: string; newVersion?: string }) => void) => {
      connectionRef.current?.on("AgentUpdateStatus", handler);
      return () => connectionRef.current?.off("AgentUpdateStatus", handler);
    },
    [],
  );

  const subscribeAsAdmin = useCallback(() => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      conn.invoke("SubscribeAsAdmin");
    }
  }, []);

  const onAdminAgentSummary = useCallback(
    (handler: (summary: AdminAgentSummary) => void) => {
      connectionRef.current?.on("AdminAgentSummary", handler);
      return () => connectionRef.current?.off("AdminAgentSummary", handler);
    },
    [],
  );

  return useMemo(() => ({
    connected,
    subscribeToAgent,
    requestDirectoryListing,
    requestStartSession,
    requestStopSession,
    requestGetSessions,
    onAgentStatusChanged,
    onSessionStatusChanged,
    onDirectoryListingResult,
    onAllSessionsUpdated,
    onAgentRemoved,
    onAgentUpdateStatus,
    subscribeAsAdmin,
    onAdminAgentSummary,
  }), [
    connected,
    subscribeToAgent,
    requestDirectoryListing,
    requestStartSession,
    requestStopSession,
    requestGetSessions,
    onAgentStatusChanged,
    onSessionStatusChanged,
    onDirectoryListingResult,
    onAllSessionsUpdated,
    onAgentRemoved,
    onAgentUpdateStatus,
    subscribeAsAdmin,
    onAdminAgentSummary,
  ]);
}
