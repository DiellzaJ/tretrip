import { useMemo, useState } from "react";
import { ArrowRight, ArrowUpRight, CalendarDays, CarFront, Search } from "lucide-react";
import type { AdminTrip, AdminUser } from "./adminApi";
import "./adminTrips.css";

const euros = (value: number) => new Intl.NumberFormat("de-DE", { style: "currency", currency: "EUR" }).format(value);
const date = (value: string) => new Date(value).toLocaleString("en-GB", { dateStyle: "medium", timeStyle: "short" });
const historyStates = new Set(["Completed", "Cancelled", "Expired"]);
const dateValid = (s: string) => Number.isFinite(Date.parse(s));

type SchedulerSummary = {
  id: number;
  name: string;
  email: string;
  total: number;
  completed: number;
  active: number;
  completedValue: number;
};

function userSummaries(trips: AdminTrip[], users: AdminUser[]): SchedulerSummary[] {
  const byId = new Map<number, SchedulerSummary>();
  for (const user of users.filter((row) => row.role === "Scheduler")) {
    byId.set(user.id, { id: user.id, name: user.fullName, email: user.email, total: 0, completed: 0, active: 0, completedValue: 0 });
  }
  for (const trip of trips) {
    let row = byId.get(trip.schedulerId);
    if (!row) {
      row = { id: trip.schedulerId, name: trip.schedulerName, email: trip.schedulerEmail, total: 0, completed: 0, active: 0, completedValue: 0 };
      byId.set(row.id, row);
    }
    row.total++;
    if (trip.status === "Completed") {
      row.completed++;
      row.completedValue += trip.price;
    } else if (!historyStates.has(trip.status)) {
      row.active++;
    }
  }
  return [...byId.values()].sort((a, b) => b.total - a.total || a.name.localeCompare(b.name));
}

function TripStatus({ status }: { status: string }) {
  const tone = status === "Completed" ? "done" : status === "Cancelled" || status === "Expired" ? "off" : status === "InProgress" ? "progress" : "pending";
  return <span className={`tt-trip-status tt-trip-status--${tone}`}>{status}</span>;
}

export function AdminTripsOverview({ trips, users, onOpenTrips }: {
  trips: AdminTrip[];
  users: AdminUser[];
  onOpenTrips: (schedulerId?: number) => void;
}) {
  const summaries = useMemo(() => userSummaries(trips, users), [trips, users]);
  const completed = trips.filter((t) => t.status === "Completed");
  const completedValue = completed.reduce((sum, row) => sum + row.price, 0);
  return <section className="tt-trip-analytics">
    <div className="tt-trip-heading">
      <div><span className="tt-admin-eyebrow">TRIP INTELLIGENCE / 02</span><h2>People &amp; their journeys<span>.</span></h2>
        <p>Real bookings across the company. Values are estimates, not confirmed payments.</p></div>
      <button type="button" className="tt-admin-add" onClick={() => onOpenTrips()}>All trips <ArrowUpRight size={17}/></button>
    </div>
    <div className="tt-trip-metrics">
      <article><span>TOTAL BOOKINGS</span><strong>{trips.length}</strong><small>All statuses included</small></article>
      <article><span>COMPLETED TRIPS</span><strong>{completed.length}</strong><small>Finished journeys</small></article>
      <article className="tt-trip-metric-green"><span>COMPLETED TRIP VALUE</span><strong>{euros(completedValue)}</strong><small>Estimated value · EUR</small></article>
    </div>
    <div className="tt-admin-table-wrap"><table className="tt-admin-table tt-trip-summary-table">
      <thead><tr><th>EMPLOYEE</th><th>ALL BOOKINGS</th><th>COMPLETED</th><th>ACTIVE / PENDING</th><th>EST. COMPLETED VALUE</th><th></th></tr></thead>
      <tbody>{summaries.map(row => <tr key={row.id}>
        <td><strong>{row.name}</strong><small>{row.email}</small></td><td>{row.total}</td><td>{row.completed}</td><td>{row.active}</td><td><strong>{euros(row.completedValue)}</strong></td>
        <td><button type="button" className="tt-admin-outline" onClick={() => onOpenTrips(row.id)}>History <ArrowRight size={14}/></button></td>
      </tr>)}</tbody>
    </table>{summaries.length === 0 && <div className="tt-admin-empty">No employees found.</div>}</div>
    <div className="tt-trip-heading tt-trip-latest-header"><div><span className="tt-admin-eyebrow">LATEST RESERVATIONS</span><h3>Recent activity</h3></div></div>
    <div className="tt-admin-table-wrap"><table className="tt-admin-table tt-trip-recent-table"><thead><tr>
      <th>TRIP</th><th>EMPLOYEE</th><th>ROUTE</th><th>EST. PRICE</th><th>STATUS</th>
    </tr></thead><tbody>{trips.slice(0, 5).map(row => <tr key={row.id}>
      <td>#{row.id}</td><td>{row.schedulerName}</td><td className="tt-trip-route">{row.pickupAddress} <ArrowRight size={14}/> {row.destinationAddress}</td><td>{euros(row.price)}</td><td><TripStatus status={row.status}/></td>
    </tr>)}</tbody></table>{trips.length === 0 && <div className="tt-admin-empty">No trips booked yet.</div>}</div>
  </section>;
}

export function AdminTripsPanel({ trips, users, initialSchedulerId }: {
  trips: AdminTrip[];
  users: AdminUser[];
  initialSchedulerId: number | "all";
}) {
  const [schedulerId, setSchedulerId] = useState<number | "all">(initialSchedulerId);
  const [status, setStatus] = useState("all");
  const [search, setSearch] = useState("");
  const schedulers = useMemo(() => userSummaries(trips, users), [trips, users]);
  const filtered = useMemo(() => trips.filter(row => {
    const query = search.toLowerCase().trim();
    return (schedulerId === "all" || row.schedulerId === schedulerId) &&
      (status === "all" || row.status === status) &&
      (!query || [row.schedulerName, row.schedulerEmail, row.pickupAddress, row.destinationAddress, String(row.id)].some(v => v.toLowerCase().includes(query)));
  }), [trips, schedulerId, status, search]);
  const completed = filtered.filter(t => t.status === "Completed");
  const currentUser = schedulerId === "all" ? "All employees" : schedulers.find(u => u.id === schedulerId)?.name ?? "Employee";
  return <section className="tt-admin-section tt-admin-trip-panel">
    <div className="tt-admin-section-head"><div><span className="tt-admin-eyebrow">BOOKINGS / HISTORY</span><h2>Trip history<span>.</span></h2>
      <p>Filter reservations by employee, status, or location.</p></div></div>
    <div className="tt-trip-filterbar">
      <label>Employee<select aria-label="Filter by employee" value={schedulerId} onChange={e => setSchedulerId(e.target.value === "all" ? "all" : Number(e.target.value))}>
        <option value="all">All employees</option>{schedulers.map(user => <option key={user.id} value={user.id}>{user.name}</option>)}
      </select></label>
      <label>Status<select aria-label="Filter by status" value={status} onChange={e => setStatus(e.target.value)}>
        <option value="all">All statuses</option>{["Pending", "Assigned", "Arrived", "InProgress", "Completed", "Cancelled", "Expired"].map(s => <option key={s} value={s}>{s}</option>)}
      </select></label>
      <label className="tt-trip-search">Search <span><Search size={16}/><input aria-label="Search trips" value={search} onChange={e => setSearch(e.target.value)} placeholder="Name, location or trip #"/></span></label>
    </div>
    <div className="tt-trip-mini-stats">
      <span><CarFront size={16}/><strong>{filtered.length}</strong> bookings for {currentUser}</span>
      <span><CalendarDays size={16}/><strong>{completed.length}</strong> completed</span>
      <span><strong>{euros(completed.reduce((sum, row) => sum + row.price, 0))}</strong> estimated completed value</span>
    </div>
    <div className="tt-admin-table-wrap"><table className="tt-admin-table tt-trip-details-table">
      <thead><tr><th>TRIP / DATE</th><th>EMPLOYEE</th><th>PICKUP → DESTINATION</th><th>DRIVER</th><th>DISTANCE</th><th>EST. PRICE</th><th>STATUS</th></tr></thead>
      <tbody>{filtered.map(row => <tr key={row.id}>
        <td><strong>#{row.id}</strong><small>{dateValid(row.scheduledAt) ? date(row.scheduledAt) : "—"}</small></td>
        <td><strong>{row.schedulerName}</strong><small>{row.schedulerEmail}</small></td>
        <td className="tt-trip-route-cell"><strong>{row.pickupAddress}</strong><span className="tt-trip-destination"><ArrowRight size={14}/>{row.destinationAddress}</span></td>
        <td>{row.driverName ? <><strong>{row.driverName}</strong><small>{row.vehiclePlateNumber ?? "—"}</small></> : <span className="tt-trip-muted">Not assigned</span>}</td>
        <td>{row.distanceKm.toFixed(2)} km</td>
        <td><strong>{euros(row.price)}</strong></td>
        <td><TripStatus status={row.status}/></td>
      </tr>)}</tbody></table>{filtered.length === 0 && <div className="tt-admin-empty">No bookings match these filters.</div>}</div>
    <p className="tt-admin-footnote">Trip prices and distances are approximate backend estimates; cancelled and expired bookings are not included in the completed-trip value.</p>
  </section>;
}
