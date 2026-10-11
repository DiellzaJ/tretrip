import { useCallback, useEffect, useRef, useState } from "react";
import { Bell, Check, LoaderCircle, RefreshCw, X } from "lucide-react";
import { getNotifications, markNotificationRead, type TripNotification } from "./notificationsApi";
import "./notifications.css";

interface Props {
  accessToken: string;
  /** Incremented by the existing SignalR NotificationsUpdated event. */
  refreshVersion: number;
}

function notificationDate(iso: string): string {
  const date = new Date(iso);
  return Number.isNaN(date.getTime())
    ? ""
    : date.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

function currentDesktopPermission(): NotificationPermission | "unsupported" {
  return typeof window !== "undefined" && "Notification" in window
    ? window.Notification.permission
    : "unsupported";
}

export default function NotificationsBell({ accessToken, refreshVersion }: Props) {
  const [notifications, setNotifications] = useState<TripNotification[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [busyId, setBusyId] = useState<number | null>(null);
  const [toast, setToast] = useState<TripNotification | null>(null);
  const [desktopPermission, setDesktopPermission] = useState(currentDesktopPermission);
  const rootRef = useRef<HTMLDivElement>(null);
  const requestId = useRef(0);
  const knownIds = useRef<Set<number>>(new Set());
  const initialNotificationsLoaded = useRef(false);

  // Never show another user's old notifications when the logged-in account changes.
  useEffect(() => {
    requestId.current += 1;
    knownIds.current = new Set();
    initialNotificationsLoaded.current = false;
    setNotifications([]);
    setToast(null);
    setOpen(false);
    setLoading(true);
    setError("");
  }, [accessToken]);

  const refresh = useCallback(async () => {
    const id = ++requestId.current;
    try {
      const data = await getNotifications(accessToken);
      if (id !== requestId.current) return;

      // The initial fetch establishes a baseline: don't replay historical alerts
      // after login, refresh or a new browser tab.
      if (initialNotificationsLoaded.current) {
        const arrived = data.filter(item => !item.isRead && !knownIds.current.has(item.id));
        if (arrived.length > 0) {
          const newest = arrived[0]; // backend returns latest first
          setToast(newest);

          // Optional OS alert for a background tab. Permission is requested only
          // by the explicit button in the notification panel, never automatically.
          if (document.visibilityState === "hidden" &&
              "Notification" in window && window.Notification.permission === "granted") {
            try {
              const browserNotification = new window.Notification(`TreTrip · Trip #${newest.tripId}`, {
                body: newest.message,
                tag: `tretrip-${newest.id}`,
              });
              browserNotification.onclick = () => {
                window.focus();
                browserNotification.close();
              };
            } catch {
              // OS notifications are optional. The bell/toast remain functional.
            }
          }
        }
      }
      data.forEach(item => knownIds.current.add(item.id));
      initialNotificationsLoaded.current = true;
      setNotifications(data);
      setError("");
    } catch (reason) {
      if (id !== requestId.current) return;
      setError(reason instanceof Error ? reason.message : "Could not load notifications.");
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [accessToken]);

  // Re-fetch as soon as the trip SignalR event arrives, with polling fallback.
  useEffect(() => {
    void refresh();
    const pollId = window.setInterval(() => void refresh(), 30_000);
    return () => {
      window.clearInterval(pollId);
      requestId.current += 1;
    };
  }, [refresh, refreshVersion]);

  // An on-screen toast is transient. The original notification stays in the DB
  // and remains unread in the bell until the user explicitly marks it as read.
  useEffect(() => {
    if (!toast) return;
    const timeout = window.setTimeout(() => setToast(null), 6500);
    return () => window.clearTimeout(timeout);
  }, [toast]);

  useEffect(() => {
    if (!open) return;
    const onOutside = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    document.addEventListener("pointerdown", onOutside);
    document.addEventListener("keydown", onEscape);
    return () => {
      document.removeEventListener("pointerdown", onOutside);
      document.removeEventListener("keydown", onEscape);
    };
  }, [open]);

  async function handleMarkRead(id: number) {
    if (busyId !== null) return;
    setBusyId(id);
    setError("");
    try {
      await markNotificationRead(accessToken, id);
      setNotifications(previous => previous.map(item =>
        item.id === id ? { ...item, isRead: true } : item
      ));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not mark notification as read.");
    } finally {
      setBusyId(null);
    }
  }

  async function enableDesktopNotifications() {
    if (!("Notification" in window)) return;
    try {
      const result = await window.Notification.requestPermission();
      setDesktopPermission(result);
    } catch {
      setDesktopPermission(currentDesktopPermission());
    }
  }

  const unread = notifications.filter(item => !item.isRead).length;

  return (
    <>
      <div className="tt-notify-anchor" ref={rootRef}>
        <button
          type="button"
          className={`tt-notify-trigger ${open ? "tt-notify-trigger--open" : ""}`}
          aria-label={`Notifications, ${unread} unread`}
          aria-expanded={open}
          aria-controls="tt-notify-panel"
          onClick={() => { setOpen(previous => !previous); setToast(null); void refresh(); }}
          title="Notifications"
        >
          <Bell size={19} strokeWidth={1.9} />
          {unread > 0 && <span className="tt-notify-badge" aria-hidden="true">{unread > 99 ? "99+" : unread}</span>}
        </button>

        {open && (
          <section id="tt-notify-panel" className="tt-notify-panel" aria-label="Your notifications">
            <div className="tt-notify-heading">
              <div>
                <span className="tt-notify-eyebrow">YOUR ACTIVITY</span>
                <h2>Notifications<span>.</span></h2>
              </div>
              <button type="button" className="tt-notify-icon-button" aria-label="Close notifications" onClick={() => setOpen(false)}><X size={19}/></button>
            </div>
            <div className="tt-notify-summary">
              <span>{unread ? `${unread} unread` : "All caught up"}</span>
              <button type="button" onClick={() => void refresh()} aria-label="Refresh notifications"><RefreshCw size={15}/> Refresh</button>
            </div>
            {desktopPermission === "default" && (
              <button type="button" className="tt-notify-desktop-enable" onClick={() => void enableDesktopNotifications()}>
                <Bell size={14}/> Enable desktop alerts when this tab is in the background
              </button>
            )}
            {error && <p className="tt-notify-error" role="alert">{error}</p>}
            <div className="tt-notify-items">
              {loading && <div className="tt-notify-empty"><LoaderCircle size={20} className="tt-notify-spin"/> Loading notifications...</div>}
              {!loading && notifications.length === 0 && <div className="tt-notify-empty"><Bell size={24}/><strong>No notifications yet</strong><span>Trip updates will appear here.</span></div>}
              {!loading && notifications.map(item => (
                <article key={item.id} className={`tt-notify-item ${item.isRead ? "" : "tt-notify-item--unread"}`}>
                  <span className={`tt-notify-dot ${item.isRead ? "tt-notify-dot--read" : ""}`} aria-hidden="true"/>
                  <div className="tt-notify-item-content">
                    <div className="tt-notify-item-top"><strong>Trip #{item.tripId}</strong><time dateTime={item.createdAt}>{notificationDate(item.createdAt)}</time></div>
                    <p>{item.message}</p>
                    {!item.isRead && (
                      <button type="button" className="tt-notify-read" disabled={busyId !== null} onClick={() => void handleMarkRead(item.id)}>
                        {busyId === item.id ? <LoaderCircle size={13} className="tt-notify-spin"/> : <Check size={14}/>} Mark as read
                      </button>
                    )}
                  </div>
                </article>
              ))}
            </div>
            <div className="tt-notify-footer">Notifications are visible only to your account.</div>
          </section>
        )}
      </div>
      {toast && !open && (
        <aside className="tt-notify-toast" role="status" aria-live="polite">
          <div className="tt-notify-toast-icon"><Bell size={18}/></div>
          <div className="tt-notify-toast-content">
            <strong>New update · Trip #{toast.tripId}</strong>
            <p>{toast.message}</p>
            <button type="button" onClick={() => { setToast(null); setOpen(true); void refresh(); }}>View notifications</button>
          </div>
          <button type="button" className="tt-notify-toast-close" aria-label="Dismiss popup" onClick={() => setToast(null)}><X size={16}/></button>
        </aside>
      )}
    </>
  );
}
