import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ArrowRight, CalendarClock, CarFront, CheckCircle2, Clock3, ExternalLink,
  LoaderCircle, LogOut, MapPin, Navigation, RefreshCw, Route,
} from "lucide-react";
import type { LoginResponse } from "../../api/client";
import type { Trip } from "../scheduler/tripsApi";
import { getDriverTrips, updateDriverTrip, type DriverAction } from "./driverApi";
import { useTripUpdates } from "../../realtime/useTripUpdates";
import NotificationsBell from "../notifications/NotificationsBell";
import "./driver.css";

function Brand() {
  return <div className="tt-brand"><span className="tt-brand-mark"><Navigation size={17} strokeWidth={3}/></span>
    <span>tretrip<span className="tt-brand-period">.</span></span></div>;
}
function prettyDate(value: string) {
  return new Date(value).toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}
function dateTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}
function statusClass(status: string) { return `tt-trip-status tt-trip-status--${status.toLowerCase()}`; }
function navigateHref(trip: Trip) {
  const origin = `${trip.pickupLatitude},${trip.pickupLongitude}`;
  const destination = `${trip.destinationLatitude},${trip.destinationLongitude}`;
  return `https://www.google.com/maps/dir/?api=1&origin=${encodeURIComponent(origin)}&destination=${encodeURIComponent(destination)}&travelmode=driving`;
}

export default function DriverDashboard({ user, onLogout }: {
  user: LoginResponse;
  onLogout: () => void;
}) {
  const [trips, setTrips] = useState<Trip[]>([]);
  const [notificationsRevision, setNotificationsRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [busyTripId, setBusyTripId] = useState<number | null>(null);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [now, setNow] = useState(Date.now());

  const refreshTrips = useCallback(async (initial = false) => {
    if (initial) setLoading(true);
    else setRefreshing(true);
    try {
      const result = await getDriverTrips(user.accessToken);
      setTrips(result);
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load assigned trips.");
    } finally {
      if (initial) setLoading(false);
      else setRefreshing(false);
    }
  }, [user.accessToken]);

  useEffect(() => {
    void refreshTrips(true);
    const poll = window.setInterval(() => void refreshTrips(), 15_000);
    const clock = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => { window.clearInterval(poll); window.clearInterval(clock); };
  }, [refreshTrips]);

  const onNotificationsUpdated = useCallback(() => {
    setNotificationsRevision(previous => previous + 1);
  }, []);
  const liveConnected = useTripUpdates(user.accessToken, refreshTrips, onNotificationsUpdated);

  const currentTrips = useMemo(() => trips
    .filter(trip => ["Assigned", "Arrived", "InProgress"].includes(trip.status))
    .sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime()), [trips]);
  const history = useMemo(() => trips
    .filter(trip => ["Completed", "Cancelled", "Expired"].includes(trip.status))
    .sort((a, b) => new Date(b.scheduledAt).getTime() - new Date(a.scheduledAt).getTime()), [trips]);

  async function performAction(trip: Trip, action: DriverAction) {
    if (busyTripId !== null) return;
    setBusyTripId(trip.id);
    setError("");
    setMessage("");
    try {
      const result = await updateDriverTrip(user.accessToken, trip.id, action);
      setMessage(`Trip #${trip.id} updated to ${result.status}.`);
      await refreshTrips();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not update trip.");
      await refreshTrips();
    } finally {
      setBusyTripId(null);
    }
  }

  const firstName = user.fullName.trim().split(" ")[0] || "Driver";

  return (
    <main className="tt-workspace tt-driver-root">
      <header className="tt-workspace-header">
        <Brand />
        <div className="tt-header-actions">
          <span className="tt-header-role">DRIVER WORKSPACE</span>
          <NotificationsBell accessToken={user.accessToken} refreshVersion={notificationsRevision} />
          <span className="tt-user-avatar" aria-label={user.fullName}>{firstName.charAt(0).toUpperCase()}</span>
          <button type="button" className="tt-logout" onClick={onLogout}><LogOut size={16}/> <span>Log out</span></button>
        </div>
      </header>

      <div className="tt-workspace-inner tt-driver-inner">
        <div className="tt-driver-intro">
          <div>
            <span className="tt-driver-eyebrow"><span className="tt-driver-dot"/> YOUR DRIVER SPACE</span>
            <h1>Ready to roll<span>?</span></h1>
            <p>Hi, {firstName}. Your assigned rides, updates and trip history — all in one place.</p>
          </div>
          <button className="tt-driver-refresh" type="button" onClick={() => void refreshTrips()} disabled={refreshing}>
            <RefreshCw size={17} className={refreshing ? "tt-driver-spin" : ""}/> Refresh
          </button>
        </div>

        {error && <div className="tt-driver-alert tt-driver-alert--error" role="alert">{error}</div>}
        {message && <div className="tt-driver-alert" role="status">{message}</div>}

        <section className="tt-driver-stats" aria-label="Trip overview">
          <div className="tt-driver-stat tt-driver-stat--dark">
            <span>ACTIVE ASSIGNMENTS</span><strong>{currentTrips.length}</strong>
            <small><CarFront size={15}/> Rides waiting for your action</small>
          </div>
          <div className="tt-driver-stat"><span>IN PROGRESS</span>
            <strong>{trips.filter(t => t.status === "InProgress").length}</strong>
            <small><Route size={15}/> Currently on the road</small>
          </div>
          <div className="tt-driver-stat"><span>COMPLETED</span>
            <strong>{trips.filter(t => t.status === "Completed").length}</strong>
            <small><CheckCircle2 size={15}/> Trips successfully finished</small>
          </div>
        </section>

        <section className="tt-driver-section">
          <div className="tt-driver-section-head">
            <div><span className="tt-driver-eyebrow">01 / NEXT RIDES</span><h2>Assigned trips</h2></div>
            <span className="tt-driver-section-note">{liveConnected ? "Live updates connected" : "Checking for updates every 15 seconds"}</span>
          </div>

          {loading ? (
            <div className="tt-driver-empty"><LoaderCircle size={25}/> Loading assignments...</div>
          ) : currentTrips.length === 0 ? (
            <div className="tt-driver-empty"><CarFront size={32}/><strong>No active trips right now</strong>
              <span>New assignments will appear here when they're available.</span></div>
          ) : (
            <div className="tt-driver-trip-grid">
              {currentTrips.map(trip => {
                const canArrive = trip.status === "Assigned" && now >= new Date(trip.scheduledAt).getTime() - 30 * 60_000;
                const canStart = ["Assigned", "Arrived"].includes(trip.status) && now >= new Date(trip.scheduledAt).getTime() - 15 * 60_000;
                const canComplete = trip.status === "InProgress";
                const busy = busyTripId !== null;
                const waiting = busyTripId === trip.id;
                return (
                  <article className="tt-driver-trip" key={trip.id}>
                    <div className="tt-driver-trip-heading">
                      <div className="tt-driver-trip-number"><span className="tt-driver-trip-icon"><CarFront size={18}/></span>TRIP #{trip.id}</div>
                      <span className={statusClass(trip.status)}>{trip.status}</span>
                    </div>
                    <div className="tt-driver-stops">
                      <div className="tt-driver-stop"><span className="tt-driver-origin-dot"/><div><small>PICKUP</small><strong>{trip.pickupAddress}</strong></div></div>
                      <div className="tt-driver-stop-line"/>
                      <div className="tt-driver-stop"><MapPin className="tt-driver-target-icon" size={18}/><div><small>DESTINATION</small><strong>{trip.destinationAddress}</strong></div></div>
                    </div>
                    <div className="tt-driver-trip-details">
                      <span><CalendarClock size={15}/> {prettyDate(trip.scheduledAt)}</span>
                      <span><Clock3 size={15}/> About {trip.estimatedDurationMinutes} min</span>
                      <span><Route size={15}/> {Number(trip.distanceKm).toFixed(2)} km (estimated)</span>
                      {trip.vehiclePlateNumber && <span><CarFront size={15}/> {trip.vehiclePlateNumber}</span>}
                    </div>
                    <a className="tt-driver-directions" target="_blank" rel="noopener noreferrer" href={navigateHref(trip)}>
                      Open driving directions <ExternalLink size={15}/>
                    </a>
                    <div className="tt-driver-trip-actions">
                      {trip.status === "Assigned" && <button type="button" className="tt-driver-secondary" disabled={!canArrive || busy}
                        title={!canArrive ? `Arrival available 30 min before departure (${dateTime(trip.scheduledAt)})` : undefined}
                        onClick={() => void performAction(trip, "arrived")}>{waiting ? "Updating..." : "I've arrived"}</button>}
                      {(["Assigned", "Arrived"].includes(trip.status)) && <button type="button" className="tt-driver-primary" disabled={!canStart || busy}
                        title={!canStart ? `Start allowed 15 min before departure (${dateTime(trip.scheduledAt)})` : undefined}
                        onClick={() => void performAction(trip, "start")}>{waiting ? "Updating..." : "Start trip"} <ArrowRight size={17}/></button>}
                      {canComplete && <button type="button" className="tt-driver-primary" disabled={busy}
                        onClick={() => void performAction(trip, "complete")}>{waiting ? "Updating..." : "Complete trip"} <CheckCircle2 size={17}/></button>}
                    </div>
                    {trip.status === "Assigned" && !canArrive && <p className="tt-driver-time-help">Arrival can be reported 30 minutes before the departure time.</p>}
                    {trip.status === "Assigned" && canArrive && !canStart && <p className="tt-driver-time-help">Start trip will become available 15 minutes before departure.</p>}
                  </article>
                );
              })}
            </div>
          )}
        </section>

        <section className="tt-driver-section tt-driver-history">
          <div className="tt-driver-section-head"><div><span className="tt-driver-eyebrow">02 / HISTORY</span><h2>Trip history</h2></div></div>
          {history.length === 0 ? <div className="tt-driver-history-empty">Completed and cancelled trips will appear here.</div> : (
            <div className="tt-driver-history-list">
              {history.map(trip => <div className="tt-driver-history-row" key={trip.id}>
                <span className="tt-driver-history-id">#{trip.id}</span>
                <div className="tt-driver-history-route">{trip.pickupAddress} <ArrowRight size={14}/> {trip.destinationAddress}</div>
                <span className="tt-driver-history-date">{prettyDate(trip.scheduledAt)}</span>
                <span className={statusClass(trip.status)}>{trip.status}</span>
              </div>)}
            </div>
          )}
        </section>
      </div>
      <footer className="tt-workspace-footer"><span>tretrip. / KEEP MOVING</span><span>BUILT FOR BUSINESS MOBILITY</span></footer>
    </main>
  );
}
