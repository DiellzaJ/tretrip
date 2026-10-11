import { useEffect, useState } from "react";
import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { API_BASE_URL } from "../api/client";

/**
 * The backend broadcasts NotificationsUpdated to the affected user's SignalR
 * connection after it commits the trip change. We then reload the authoritative
 * trip list from the API, rather than trusting potentially stale event payloads.
 * The dashboards also keep their existing 15-second polling fallback.
 */
export function useTripUpdates(
  accessToken: string,
  refreshTrips: () => void | Promise<unknown>,
  onNotificationsUpdated?: () => void,
): boolean {
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let mounted = true;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/notifications`, {
        accessTokenFactory: () => accessToken,
        withCredentials: false,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    const refresh = () => {
      if (!mounted) return;
      void refreshTrips();
      onNotificationsUpdated?.();
    };
    const scheduleRetry = () => {
      if (!mounted || retryTimer !== undefined) return;
      retryTimer = setTimeout(() => {
        retryTimer = undefined;
        void start();
      }, 5000);
    };
    const start = async () => {
      if (!mounted || connection.state !== HubConnectionState.Disconnected) return;
      try {
        await connection.start();
        if (!mounted) return;
        setConnected(true);
        refresh(); // Picks up events missed during initial connection/reconnect.
      } catch {
        if (mounted) {
          setConnected(false);
          scheduleRetry();
        }
      }
    };

    connection.on("NotificationsUpdated", refresh);
    connection.onreconnecting(() => {
      if (mounted) setConnected(false);
    });
    connection.onreconnected(() => {
      if (mounted) {
        setConnected(true);
        refresh();
      }
    });
    connection.onclose(() => {
      if (mounted) {
        setConnected(false);
        scheduleRetry();
      }
    });

    void start();
    return () => {
      mounted = false;
      if (retryTimer !== undefined) clearTimeout(retryTimer);
      connection.off("NotificationsUpdated", refresh);
      void connection.stop();
    };
  }, [accessToken, refreshTrips, onNotificationsUpdated]);

  return connected;
}
