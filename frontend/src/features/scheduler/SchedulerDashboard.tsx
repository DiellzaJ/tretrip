import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import * as L from "leaflet";
import "leaflet/dist/leaflet.css";
import {
  ArrowRight, CalendarClock, CarFront, Check, Clock3, LoaderCircle,
  LogOut, MapPin, Navigation, Plus, RefreshCw, Route, X,
} from "lucide-react";
import type { LoginResponse } from "../../api/client";
import { cancelTrip, createTrip, getMyTrips, type Trip } from "./tripsApi";
import { useTripUpdates } from "../../realtime/useTripUpdates";
import NotificationsBell from "../notifications/NotificationsBell";
import "./scheduler.css";

type Point = { lat: number; lng: number };
type PinType = "pickup" | "destination";

const INITIAL_CENTER: [number, number] = [42.6629, 21.1655]; // Prishtina

function formatLocalDateInput(date: Date): string {
  const adjusted = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return adjusted.toISOString().slice(0, 16);
}

function formatTripDate(date: string): string {
  return new Date(date).toLocaleString(undefined, {
    dateStyle: "medium", timeStyle: "short",
  });
}

function estimateTrip(pickup: Point, destination: Point) {
  const toRadians = (degrees: number) => degrees * Math.PI / 180;
  const dLat = toRadians(destination.lat - pickup.lat);
  const dLng = toRadians(destination.lng - pickup.lng);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRadians(pickup.lat)) *
    Math.cos(toRadians(destination.lat)) * Math.sin(dLng / 2) ** 2;
  const straightKm = 6371 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  const distanceKm = Math.round(straightKm * 1.3 * 100) / 100;
  const minutes = Math.max(5, Math.ceil(distanceKm / 30 * 60));
  return { distanceKm, minutes, price: (2.5 + distanceKm * 0.8).toFixed(2) };
}

function TripMap({
  pickup, destination, activePin, onMapPick,
}: {
  pickup: Point | null;
  destination: Point | null;
  activePin: PinType;
  onMapPick: (point: Point, type: PinType) => void;
}) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<L.Map | null>(null);
  const pinLayerRef = useRef<L.LayerGroup | null>(null);
  const activePinRef = useRef(activePin);
  const onMapPickRef = useRef(onMapPick);

  useEffect(() => { activePinRef.current = activePin; }, [activePin]);
  useEffect(() => { onMapPickRef.current = onMapPick; }, [onMapPick]);

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = L.map(containerRef.current, { zoomControl: false }).setView(INITIAL_CENTER, 13);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      maxZoom: 19,
    }).addTo(map);
    L.control.zoom({ position: "bottomright" }).addTo(map);
    const pinLayer = L.layerGroup().addTo(map);
    pinLayerRef.current = pinLayer;
    mapRef.current = map;
    const onClick = (event: L.LeafletMouseEvent) => {
      onMapPickRef.current(
        { lat: event.latlng.lat, lng: event.latlng.lng }, activePinRef.current
      );
    };
    map.on("click", onClick);
    const resize = window.setTimeout(() => map.invalidateSize(), 200);
    return () => {
      window.clearTimeout(resize);
      map.off("click", onClick);
      pinLayerRef.current = null;
      mapRef.current = null;
      map.remove();
    };
  }, []);

  useEffect(() => {
    const pinLayer = pinLayerRef.current;
    if (!pinLayer) return;
    pinLayer.clearLayers();
    const mark = (point: Point, color: string, title: string) => {
      L.circleMarker([point.lat, point.lng], {
        radius: 11, color: "#ffffff", weight: 3, fillColor: color, fillOpacity: 1,
      }).bindTooltip(title).addTo(pinLayer);
    };
    if (pickup) mark(pickup, "#193b24", "Pickup");
    if (destination) mark(destination, "#89be3c", "Destination");
    if (pickup && destination) {
      L.polyline([[pickup.lat, pickup.lng], [destination.lat, destination.lng]], {
        color: "#315a23", weight: 3, dashArray: "7 9", opacity: 0.8,
      }).addTo(pinLayer);
    }
  }, [pickup, destination]);

  return <div ref={containerRef} className="tt-book-map" aria-label="Interactive map: click to set pickup or destination" />;
}

function Brand() {
  return <div className="tt-brand"><span className="tt-brand-mark"><Navigation size={17} strokeWidth={3} /></span>
    <span>tretrip<span className="tt-brand-period">.</span></span></div>;
}

function TripStatusLabel({ status }: { status: string }) {
  return <span className={`tt-trip-status tt-trip-status--${status.toLowerCase()}`}>{status}</span>;
}

export default function SchedulerDashboard({ user, onLogout }: {
  user: LoginResponse;
  onLogout: () => void;
}) {
  const [trips, setTrips] = useState<Trip[]>([]);
  const [notificationsRevision, setNotificationsRevision] = useState(0);
  const [loadingTrips, setLoadingTrips] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [bookingOpen, setBookingOpen] = useState(false);
  const [activePin, setActivePin] = useState<PinType>("pickup");
  const [pickup, setPickup] = useState<Point | null>(null);
  const [destination, setDestination] = useState<Point | null>(null);
  const [pickupAddress, setPickupAddress] = useState("");
  const [destinationAddress, setDestinationAddress] = useState("");
  const [departure, setDeparture] = useState(() => formatLocalDateInput(new Date(Date.now() + 60 * 60 * 1000)));
  const [saving, setSaving] = useState(false);
  const [busyTrip, setBusyTrip] = useState<number | null>(null);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  const refreshTrips = useCallback(async (initial = false) => {
    // Initial loading is already true in useState; updates follow the async fetch.
    try {
      const result = await getMyTrips(user.accessToken);
      setTrips(result);
      setLoadError("");
    } catch (reason) {
      setLoadError(reason instanceof Error ? reason.message : "Could not load trips.");
    } finally {
      if (initial) setLoadingTrips(false);
    }
  }, [user.accessToken]);

  useEffect(() => {
    const initialFetch = window.setTimeout(() => void refreshTrips(true), 0);
    const interval = window.setInterval(() => void refreshTrips(), 15_000);
    return () => {
      window.clearTimeout(initialFetch);
      window.clearInterval(interval);
    };
  }, [refreshTrips]);

  const onNotificationsUpdated = useCallback(() => {
    setNotificationsRevision(previous => previous + 1);
  }, []);
  const liveConnected = useTripUpdates(user.accessToken, refreshTrips, onNotificationsUpdated);

  const onMapPick = useCallback((point: Point, type: PinType) => {
    const coords = `${point.lat.toFixed(5)}, ${point.lng.toFixed(5)}`;
    if (type === "pickup") {
      setPickup(point);
      setPickupAddress(`Pickup (${coords})`);
      setActivePin("destination");
    } else {
      setDestination(point);
      setDestinationAddress(`Destination (${coords})`);
    }
  }, []);

  const preview = pickup && destination ? estimateTrip(pickup, destination) : null;
  // Show recently CREATED active bookings first, not the latest departure time.
  // GET /api/trips is sorted by scheduledAt, so we sort explicitly for the UI.
  const upcoming = trips
    .filter(t => ["Pending", "Assigned", "Arrived", "InProgress"].includes(t.status))
    .sort((a, b) =>
      new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime() || b.id - a.id
    );
  const historyTrips = trips
    .filter(t => ["Completed", "Cancelled", "Expired"].includes(t.status))
    .sort((a, b) =>
      new Date(b.scheduledAt).getTime() - new Date(a.scheduledAt).getTime() || b.id - a.id
    );
  const completed = trips.filter(t => t.status === "Completed");

  async function handleBooking(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(""); setNotice("");
    if (!pickup || !destination) {
      setError("Choose both pickup and destination pins on the map.");
      return;
    }
    if (pickupAddress.trim().length < 3 || destinationAddress.trim().length < 3) {
      setError("Enter a valid pickup and destination address.");
      return;
    }
    const date = new Date(departure);
    if (!Number.isFinite(date.getTime()) || date.getTime() <= Date.now()) {
      setError("Choose a future departure date and time.");
      return;
    }
    if (preview && preview.distanceKm < 0.07) {
      setError("Pickup and destination are too close. Choose different points.");
      return;
    }
    setSaving(true);
    try {
      const created = await createTrip(user.accessToken, {
        pickupAddress: pickupAddress.trim(),
        pickupLatitude: pickup.lat,
        pickupLongitude: pickup.lng,
        destinationAddress: destinationAddress.trim(),
        destinationLatitude: destination.lat,
        destinationLongitude: destination.lng,
        scheduledAt: date.toISOString(),
      });
      setNotice(`Trip #${created.id} created — ${created.status.toLowerCase()}.`);
      setBookingOpen(false);
      setPickup(null); setDestination(null);
      setPickupAddress(""); setDestinationAddress("");
      setActivePin("pickup");
      await refreshTrips();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Booking failed.");
    } finally { setSaving(false); }
  }

  async function handleCancel(id: number) {
    if (!window.confirm(`Cancel trip #${id}?`)) return;
    setBusyTrip(id); setError(""); setNotice("");
    try {
      await cancelTrip(user.accessToken, id);
      setNotice(`Trip #${id} cancelled.`);
      await refreshTrips();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Cancellation failed.");
    } finally { setBusyTrip(null); }
  }

  return (
    <main className="tt-workspace tt-scheduler-root">
      <header className="tt-workspace-header">
        <Brand />
        <div className="tt-header-actions">
          <span className="tt-header-role">SCHEDULER WORKSPACE</span>
          <NotificationsBell key={user.accessToken} accessToken={user.accessToken} refreshVersion={notificationsRevision} />
          <span className="tt-user-avatar">{user.fullName.trim().charAt(0).toUpperCase() || "T"}</span>
          <button type="button" className="tt-logout" onClick={onLogout}><LogOut size={16}/> <span>Log out</span></button>
        </div>
      </header>
      <div className="tt-workspace-inner tt-scheduler-inner">
        <section className="tt-sch-intro">
          <div><div className="tt-sch-eyebrow"><span className="tt-sch-dot"/> YOUR WORKSPACE</div>
            <h1>Where to next<span>?</span></h1>
            <p>Welcome, {user.fullName.split(" ")[0]}. Book a ride and keep every trip in one place.</p>
          </div>
          <button type="button" className="tt-sch-new" onClick={() => {setBookingOpen(!bookingOpen);setError("");}}>
            {bookingOpen ? <X size={19}/> : <Plus size={19}/>} {bookingOpen ? "Close booking" : "Book a trip"}
          </button>
        </section>

        {(notice || error) && <div role={error ? "alert" : "status"} className={`tt-sch-alert ${error ? "tt-sch-alert--error" : ""}`}>{error || notice}</div>}

        <section className="tt-sch-summary" aria-label="Trip summary">
          <div className="tt-sch-stat"><span>ACTIVE TRIPS</span><strong>{upcoming.length}</strong><span><Route size={16}/> Upcoming & ongoing</span></div>
          <div className="tt-sch-stat"><span>COMPLETED</span><strong>{completed.length}</strong><span><Check size={16}/> Finished journeys</span></div>
          <div className="tt-sch-stat tt-sch-stat--dark"><span>TOTAL BOOKINGS</span><strong>{trips.length}</strong><span><CarFront size={16}/> Your trip history</span></div>
        </section>

        {bookingOpen && (
          <section className="tt-sch-booking">
            <div className="tt-sch-booking-head"><div><span>01 / NEW BOOKING</span><h2>Plan your ride<span>.</span></h2></div><p>Choose pickup and destination on the map.</p></div>
            <div className="tt-sch-booking-grid">
              <div className="tt-sch-map-col">
                <div className="tt-sch-picker" role="group" aria-label="Select map pin">
                  <button type="button" className={activePin === "pickup" ? "selected" : ""} onClick={() => setActivePin("pickup")}><MapPin size={17}/> 1. Pickup {pickup && <Check size={15}/>}</button>
                  <button type="button" className={activePin === "destination" ? "selected" : ""} onClick={() => setActivePin("destination")}><Navigation size={17}/> 2. Destination {destination && <Check size={15}/>}</button>
                </div>
                <TripMap pickup={pickup} destination={destination} activePin={activePin} onMapPick={onMapPick} />
                <p className="tt-sch-map-note">Map preview shows a straight line, not a driving route. Map data © OpenStreetMap.</p>
              </div>
              <form className="tt-sch-trip-form" onSubmit={handleBooking}>
                <label htmlFor="tt-pickup">Pickup address</label>
                <input id="tt-pickup" value={pickupAddress} onChange={e => setPickupAddress(e.target.value)} placeholder="Click pickup on the map" required minLength={3} maxLength={250}/>
                <label htmlFor="tt-destination">Destination address</label>
                <input id="tt-destination" value={destinationAddress} onChange={e => setDestinationAddress(e.target.value)} placeholder="Click destination on the map" required minLength={3} maxLength={250}/>
                <label htmlFor="tt-departure">Departure date and time</label>
                <input id="tt-departure" type="datetime-local" value={departure} min={formatLocalDateInput(new Date())} onChange={e => setDeparture(e.target.value)} required/>
                <div className="tt-sch-preview"><span>ESTIMATED FARE</span>
                  <strong>{preview ? `€${preview.price}` : "—"}</strong>
                  <p>{preview ? `${preview.distanceKm.toFixed(2)} km · about ${preview.minutes} min` : "Select both map pins to get an estimate."}</p>
                  <small>Approximation using straight-line distance × 1.3; final values are calculated by the backend. Not a real road ETA.</small>
                </div>
                <button disabled={saving || !pickup || !destination} type="submit" className="tt-sch-submit">{saving ? <LoaderCircle size={18}/> : <ArrowRight size={18}/>} {saving ? "Creating booking..." : "Confirm booking"}</button>
              </form>
            </div>
          </section>
        )}

        <section className="tt-sch-list-section">
          <div className="tt-sch-list-heading"><div><span>01 / UPCOMING</span><h2>Upcoming trips</h2></div><span style={{fontSize: 12, color: "#70806b", marginLeft: "auto"}}>{liveConnected ? "● Live updates" : "Updates every 15s"}</span><button type="button" onClick={() => void refreshTrips(true)} className="tt-sch-refresh"><RefreshCw size={16}/> Refresh</button></div>
          {loadError && <div className="tt-sch-alert tt-sch-alert--error" role="alert">{loadError}</div>}
          {loadingTrips ? <div className="tt-sch-empty"><LoaderCircle size={24}/> Loading your trips...</div> : upcoming.length === 0 ?
            <div className="tt-sch-empty"><CarFront size={33}/><h3>No active bookings</h3><p>Your next trip will appear here as soon as you book it.</p></div> :
            <div className="tt-sch-trip-list">
              {upcoming.map(trip => <article className="tt-sch-trip-row" key={trip.id}>
                <div className="tt-sch-trip-symbol"><CarFront size={23}/></div>
                <div className="tt-sch-trip-main"><div className="tt-sch-trip-meta">TRIP #{trip.id} <TripStatusLabel status={trip.status}/></div>
                  <strong>{trip.pickupAddress} <ArrowRight size={15}/> {trip.destinationAddress}</strong>
                  <span className="tt-sch-trip-detail"><CalendarClock size={15}/> {formatTripDate(trip.scheduledAt)} <span>·</span> <Clock3 size={15}/> {trip.estimatedDurationMinutes} min</span>
                  {trip.driverName && <span className="tt-sch-trip-driver">Driver: {trip.driverName} · {trip.vehiclePlateNumber ?? "Vehicle pending"}</span>}
                  {trip.status === "Pending" && <span className="tt-sch-trip-driver">Waiting for an available driver.</span>}
                </div>
                <div className="tt-sch-trip-side"><strong>€{Number(trip.price).toFixed(2)}</strong><small>{Number(trip.distanceKm).toFixed(2)} km</small>
                  {["Pending", "Assigned"].includes(trip.status) && <button type="button" disabled={busyTrip === trip.id} onClick={() => void handleCancel(trip.id)}>{busyTrip === trip.id ? "Cancelling..." : "Cancel trip"}</button>}
                </div>
              </article>)}
            </div>
          }
        </section>

        <section className="tt-sch-list-section tt-sch-history-section">
          <div className="tt-sch-list-heading"><div><span>02 / HISTORY</span><h2>Trip history</h2></div></div>
          {loadingTrips ? <div className="tt-sch-empty">Loading trip history...</div> : historyTrips.length === 0 ?
            <div className="tt-sch-empty">Completed, cancelled and expired trips will appear here.</div> :
            <div className="tt-sch-history-list">
              {historyTrips.map(trip => <div className="tt-sch-history-row" key={trip.id}>
                <span className="tt-sch-history-id">#{trip.id}</span>
                <div className="tt-sch-history-route">{trip.pickupAddress} <ArrowRight size={14}/> {trip.destinationAddress}</div>
                <span className="tt-sch-history-date">{formatTripDate(trip.scheduledAt)}</span>
                <TripStatusLabel status={trip.status}/>
              </div>)}
            </div>
          }
        </section>
      </div>
      <footer className="tt-workspace-footer"><span>tretrip. / KEEP MOVING</span><span>BUILT FOR BUSINESS MOBILITY</span></footer>
    </main>
  );
}
