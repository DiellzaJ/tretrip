import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import {
  ArrowUpRight, Building2, CarFront, Check, ClipboardList, History, LogOut,
  Navigation, Plus, RefreshCw, ShieldCheck, Users, X,
} from "lucide-react";
import type { LoginResponse } from "../../api/client";
import {
  adminApi, type AdminDriver, type AdminUser, type AdminVehicle, type AdminTrip,
  type TaxiCompany, type CreateCompany, type CreateDriver, type CreateEmployee,
  type CreateVehicle,
} from "./adminApi";
import "./admin.css";
import { AdminTripsOverview, AdminTripsPanel } from "./AdminTrips";

type Tab = "overview" | "trips" | "employees" | "companies" | "drivers" | "vehicles";
type Creation = Exclude<Tab, "overview" | "trips">;
const tabs: { value: Tab; label: string; Icon: typeof Users }[] = [
  { value: "overview", label: "Overview", Icon: ClipboardList },
  { value: "trips", label: "Trip history", Icon: History },
  { value: "employees", label: "Users", Icon: Users },
  { value: "companies", label: "Taxi companies", Icon: Building2 },
  { value: "drivers", label: "Drivers", Icon: ShieldCheck },
  { value: "vehicles", label: "Vehicles", Icon: CarFront },
];

function Brand() {
  return <div className="tt-brand"><span className="tt-brand-mark"><Navigation size={17} strokeWidth={3} /></span>
    <span>tretrip<span className="tt-brand-period">.</span></span></div>;
}
function Status({ active, available = false }: { active: boolean; available?: boolean }) {
  return <span className={`tt-admin-pill ${active ? "tt-admin-pill--on" : "tt-admin-pill--off"}`}>
    <span className="tt-admin-pill-dot" />{active ? (available ? "Available" : "Active") : (available ? "Unavailable" : "Inactive")}
  </span>;
}
function CompanySelect({ companies, value, onChange }: {
  companies: TaxiCompany[];
  value: number;
  onChange: (v: number) => void;
}) {
  return <select required value={value || ""} onChange={(e) => onChange(Number(e.target.value))}>
    <option value="">Select company</option>
    {companies.filter((c) => c.isActive).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
  </select>;
}

export default function AdminDashboard({ user, onLogout }: { user: LoginResponse; onLogout: () => void }) {
  const [tab, setTab] = useState<Tab>("overview");
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [trips, setTrips] = useState<AdminTrip[]>([]);
  const [tripFocus, setTripFocus] = useState<number | "all">("all");
  const [companies, setCompanies] = useState<TaxiCompany[]>([]);
  const [drivers, setDrivers] = useState<AdminDriver[]>([]);
  const [vehicles, setVehicles] = useState<AdminVehicle[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [working, setWorking] = useState<string | null>(null);
  const [creation, setCreation] = useState<Creation | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const refresh = useCallback(async (initial = false) => {
    if (initial) setLoading(true); else setRefreshing(true);
    try {
      const [newUsers, newCompanies, newDrivers, newVehicles, newTrips] = await Promise.all([
        adminApi.getUsers(user.accessToken),
        adminApi.getCompanies(user.accessToken),
        adminApi.getDrivers(user.accessToken),
        adminApi.getVehicles(user.accessToken),
        adminApi.getTrips(user.accessToken),
      ]);
      setUsers(newUsers);
      setCompanies(newCompanies);
      setDrivers(newDrivers);
      setVehicles(newVehicles);
      setTrips(newTrips);
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load admin data.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [user.accessToken]);

  useEffect(() => { void refresh(true); }, [refresh]);

  async function execute(key: string, work: () => Promise<unknown>, success: string, reportErrorToModal = false) {
    if (working) return false;
    setWorking(key);
    setError("");
    setNotice("");
    try {
      await work();
      await refresh();
      setNotice(success);
      return true;
    } catch (reason) {
      if (reportErrorToModal) throw reason;
      setError(reason instanceof Error ? reason.message : "The action could not be completed.");
      return false;
    } finally {
      setWorking(null);
    }
  }

  function toggleUser(row: AdminUser) {
    if (row.id === user.userId) return;
    if (row.isActive && !window.confirm(`Deactivate ${row.fullName}? They will not be able to sign in.`)) return;
    void execute(`user-${row.id}`, () => adminApi.setUserActive(user.accessToken, row.id, !row.isActive), "User status updated.");
  }
  function toggleDriver(row: AdminDriver) {
    void execute(`driver-${row.id}`, () => adminApi.setDriverAvailable(user.accessToken, row.id, !row.isAvailable), "Driver availability updated.");
  }
  function toggleVehicle(row: AdminVehicle) {
    void execute(`vehicle-${row.id}`, () => adminApi.setVehicleAvailable(user.accessToken, row.id, !row.isAvailable), "Vehicle availability updated.");
  }

  const stats = useMemo(() => [
    { title: "TEAM MEMBERS", amount: users.filter((u) => u.role === "Scheduler" && u.isActive).length, caption: "Active schedulers", Icon: Users },
    { title: "DRIVERS ONLINE", amount: drivers.filter((d) => d.isAvailable && d.isActive).length, caption: `of ${drivers.length} registered`, Icon: ShieldCheck },
    { title: "VEHICLES READY", amount: vehicles.filter((v) => v.isAvailable && v.isActive).length, caption: `of ${vehicles.length} registered`, Icon: CarFront },
    { title: "TAXI COMPANIES", amount: companies.filter((c) => c.isActive).length, caption: "Active partners", Icon: Building2 },
  ], [users, drivers, vehicles, companies]);
  const companyCount = companies.length;
  function openTrips(schedulerId?: number) {
    setTripFocus(schedulerId ?? "all");
    setTab("trips");
  }

  return <main className="tt-workspace tt-admin-root">
    <header className="tt-workspace-header"><Brand /><div className="tt-header-actions">
      <span className="tt-header-role">ADMIN WORKSPACE</span>
      <span className="tt-user-avatar" aria-label={user.fullName}>{user.fullName.charAt(0).toUpperCase()}</span>
      <button className="tt-logout" type="button" onClick={onLogout}><LogOut size={16} /> Log out</button>
    </div></header>
    <div className="tt-workspace-inner tt-admin-inner">
      <div className="tt-admin-intro"><div>
        <span className="tt-admin-eyebrow"><span className="tt-admin-dot" /> OPERATIONS / CONTROL CENTER</span>
        <h1>Keep everyone <span>moving.</span></h1>
        <p>Manage your people, partner companies and vehicle availability from one place.</p>
      </div><button type="button" className="tt-admin-refresh" onClick={() => void refresh()} disabled={loading || refreshing}>
        <RefreshCw size={16} className={refreshing ? "tt-admin-spin" : ""} /> Refresh data
      </button></div>

      {error && <div className="tt-admin-alert tt-admin-alert--error" role="alert">{error}</div>}
      {notice && <div className="tt-admin-alert" role="status"><Check size={15}/>{notice}</div>}

      <nav className="tt-admin-tabs" aria-label="Admin sections">{tabs.map(({ value, label, Icon }) =>
        <button type="button" key={value} className={`tt-admin-tab ${tab === value ? "tt-admin-tab--active" : ""}`} onClick={() => setTab(value)}>
          <Icon size={17} /> {label}</button>)}</nav>

      {loading ? <section className="tt-admin-empty">Loading operational data...</section> : <>
        {tab === "overview" && <>
          <div className="tt-admin-stat-grid">{stats.map(({ title, amount, caption, Icon }, index) =>
            <div className={`tt-admin-stat ${index === 1 ? "tt-admin-stat--dark" : ""}`} key={title}>
              <div className="tt-admin-stat-top"><span>{title}</span><Icon size={19}/></div>
              <strong>{amount}</strong><small>{caption}</small>
            </div>)}</div>
          <section className="tt-admin-overview-grid">
            <div className="tt-admin-promo"><span>FLEET MANAGEMENT / 01</span>
              <div><h2>Your fleet.<br/>At a glance<span>.</span></h2>
                <p>{companyCount} partner {companyCount === 1 ? "company" : "companies"} registered. Availability updates are reflected in new assignments.</p></div>
              <button type="button" onClick={() => setTab("drivers")}>Manage drivers <ArrowUpRight size={17}/></button>
            </div>
            <div className="tt-admin-overview-side"><span className="tt-admin-eyebrow">OPERATIONAL SNAPSHOT</span>
              <h3>Everything in its place.</h3>
              <p>Use the sections above to add employees, register partners, and manage drivers and vehicles.</p>
              <div><span>ACTIVE USERS</span><strong>{users.filter((u) => u.isActive).length}</strong></div>
              <div><span>AVAILABLE FLEET</span><strong>{vehicles.filter((v) => v.isActive && v.isAvailable).length}</strong></div>
            </div>
          </section>
          <AdminTripsOverview trips={trips} users={users} onOpenTrips={openTrips} />
        </>}

        {tab === "trips" && <AdminTripsPanel key={tripFocus} trips={trips} users={users} initialSchedulerId={tripFocus} />}

        {tab !== "overview" && tab !== "trips" && <section className="tt-admin-section">
          <div className="tt-admin-section-head"><div>
            <span className="tt-admin-eyebrow">{tab.toUpperCase()} / MANAGEMENT</span>
            <h2>{tab === "employees" ? "Team & accounts" : tab === "companies" ? "Partner companies" : tab === "drivers" ? "Drivers" : "Vehicles"}</h2>
          </div><button type="button" className="tt-admin-add" onClick={() => { setError(""); setNotice(""); setCreation(tab); }}>
            <Plus size={17}/> {tab === "employees" ? "Add employee" : tab === "companies" ? "Add company" : tab === "drivers" ? "Add driver" : "Add vehicle"}
          </button></div>

          {tab === "employees" && <div className="tt-admin-table-wrap"><table className="tt-admin-table"><thead><tr>
            <th>USER</th><th>ROLE</th><th>STATUS</th><th>ACTION</th>
          </tr></thead><tbody>{users.map((row) => <tr key={row.id}>
            <td><strong>{row.fullName}</strong><small>{row.email}</small></td><td>{row.role}</td><td><Status active={row.isActive}/></td>
            <td><button disabled={Boolean(working) || row.id === user.userId} className="tt-admin-outline" type="button" onClick={() => toggleUser(row)}>
              {working === `user-${row.id}` ? "Saving..." : row.id === user.userId ? "Your account" : row.isActive ? "Deactivate" : "Activate"}
            </button></td></tr>)}</tbody></table>{users.length === 0 && <div className="tt-admin-empty">No users found.</div>}</div>}

          {tab === "companies" && <div className="tt-admin-table-wrap"><table className="tt-admin-table"><thead><tr>
            <th>COMPANY</th><th>CONTACT</th><th>CONTRACT START</th><th>CONTRACT END</th><th>STATUS</th>
          </tr></thead><tbody>{companies.map((row) => <tr key={row.id}>
            <td><strong>{row.name}</strong><small>Company #{row.id}</small></td><td>{row.phone}</td>
            <td>{formatDate(row.contractStartDate)}</td><td>{row.contractEndDate ? formatDate(row.contractEndDate) : "Open-ended"}</td><td><Status active={row.isActive}/></td>
          </tr>)}</tbody></table>{companies.length === 0 && <div className="tt-admin-empty">No taxi companies registered.</div>}</div>}

          {tab === "drivers" && <div className="tt-admin-table-wrap"><table className="tt-admin-table"><thead><tr>
            <th>DRIVER</th><th>COMPANY</th><th>LICENSE</th><th>STATUS</th><th>AVAILABILITY</th>
          </tr></thead><tbody>{drivers.map((row) => <tr key={row.id}>
            <td><strong>{row.fullName}</strong><small>{row.email}</small></td><td>{row.taxiCompanyName}</td><td>{row.licenseNumber}</td>
            <td><Status active={row.isActive}/></td><td><div className="tt-admin-availability"><Status active={row.isAvailable} available />
              <button type="button" disabled={Boolean(working) || !row.isActive} className="tt-admin-outline" onClick={() => toggleDriver(row)}>
                {working === `driver-${row.id}` ? "Saving..." : row.isAvailable ? "Set unavailable" : "Set available"}</button></div></td>
          </tr>)}</tbody></table>{drivers.length === 0 && <div className="tt-admin-empty">No drivers registered.</div>}</div>}

          {tab === "vehicles" && <div className="tt-admin-table-wrap"><table className="tt-admin-table"><thead><tr>
            <th>VEHICLE</th><th>MODEL</th><th>COMPANY</th><th>STATUS</th><th>AVAILABILITY</th>
          </tr></thead><tbody>{vehicles.map((row) => <tr key={row.id}>
            <td><strong>{row.plateNumber}</strong><small>Vehicle #{row.id}</small></td><td>{row.model}</td><td>{row.taxiCompanyName}</td>
            <td><Status active={row.isActive}/></td><td><div className="tt-admin-availability"><Status active={row.isAvailable} available/>
              <button type="button" disabled={Boolean(working) || !row.isActive} className="tt-admin-outline" onClick={() => toggleVehicle(row)}>
                {working === `vehicle-${row.id}` ? "Saving..." : row.isAvailable ? "Set unavailable" : "Set available"}</button></div></td>
          </tr>)}</tbody></table>{vehicles.length === 0 && <div className="tt-admin-empty">No vehicles registered.</div>}</div>}
        </section>}
      </>}
      <p className="tt-admin-footnote">Fleet statistics and trip history use live API data. Trip prices are estimates, not verified charges.</p>
    </div>
    <footer className="tt-workspace-footer"><span>tretrip. / KEEP MOVING</span><span>ADMIN CONTROL CENTER</span></footer>
    {creation && <CreateModal key={creation} kind={creation} companies={companies} busy={Boolean(working)}
      onClose={() => setCreation(null)} onCreate={async (body) => {
        const created = await execute(`create-${creation}`, () => {
          if (creation === "employees") return adminApi.createEmployee(user.accessToken, body as CreateEmployee);
          if (creation === "companies") return adminApi.createCompany(user.accessToken, body as CreateCompany);
          if (creation === "drivers") return adminApi.createDriver(user.accessToken, body as CreateDriver);
          return adminApi.createVehicle(user.accessToken, body as CreateVehicle);
        }, "Record created successfully.", true);
        if (created) setCreation(null);
      }} />}
  </main>;
}

function formatDate(value: string) { return new Date(value).toLocaleDateString(); }
function CreateModal({ kind, companies, busy, onClose, onCreate }: {
  kind: Creation;
  companies: TaxiCompany[];
  busy: boolean;
  onClose: () => void;
  onCreate: (payload: CreateEmployee | CreateCompany | CreateDriver | CreateVehicle) => Promise<void>;
}) {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [startDate, setStartDate] = useState(new Date().toLocaleDateString("en-CA"));
  const [endDate, setEndDate] = useState("");
  const [companyId, setCompanyId] = useState(0);
  const [license, setLicense] = useState("");
  const [plate, setPlate] = useState("");
  const [model, setModel] = useState("");
  const [modalError, setModalError] = useState("");
  function localDateUtc(value: string): string { return `${value}T00:00:00.000Z`; }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (busy) return;
    setModalError("");
    try {
      if (kind === "employees") await onCreate({ fullName: fullName.trim(), email: `${email.trim().toLowerCase()}@tretek.io`, password });
      if (kind === "companies") await onCreate({ name: name.trim(), phone: phone.trim(), contractStartDate: localDateUtc(startDate), contractEndDate: endDate ? localDateUtc(endDate) : null });
      if (kind === "drivers") await onCreate({ fullName: fullName.trim(), email: email.trim(), password, licenseNumber: license.trim(), taxiCompanyId: companyId });
      if (kind === "vehicles") await onCreate({ plateNumber: plate.trim(), model: model.trim(), taxiCompanyId: companyId });
    } catch (reason) {
      setModalError(reason instanceof Error ? reason.message : "Could not create the record. Please try again.");
    }
  }
  const title = kind === "employees" ? "Add employee" : kind === "companies" ? "Add taxi company" : kind === "drivers" ? "Register driver" : "Register vehicle";
  return <div className="tt-admin-modal-scrim" role="presentation" onMouseDown={(e) => { if (e.target === e.currentTarget && !busy) onClose(); }}>
    <section className="tt-admin-modal" role="dialog" aria-modal="true" aria-labelledby="tt-admin-dialog-title">
      <header className="tt-admin-modal-head"><div><span className="tt-admin-eyebrow">NEW RECORD</span><h2 id="tt-admin-dialog-title">{title}</h2></div>
        <button type="button" className="tt-admin-close" onClick={onClose} aria-label="Close" disabled={busy}><X size={18}/></button></header>
      {modalError && <div className="tt-admin-inline-error" role="alert">{modalError}</div>}
      <form className="tt-admin-create-form" onSubmit={(e) => void submit(e)}>
        {(kind === "employees" || kind === "drivers") && <>
          <label htmlFor="admin-fullname">Full name</label><input id="admin-fullname" required minLength={2} maxLength={120} value={fullName} onChange={(e) => setFullName(e.target.value)}/>
          {kind === "employees" ? <>
            <label htmlFor="admin-email">Work email</label>
            <div className="tt-admin-company-email">
              <input
                id="admin-email" type="text" required
                placeholder="username" autoCapitalize="none" autoComplete="off" spellCheck={false}
                maxLength={64} pattern="[A-Za-z0-9]+([._%+-][A-Za-z0-9]+)*"
                title="Enter only the part before @tretek.io"
                value={email} onChange={(e) => setEmail(e.target.value)}
              />
              <span>@tretek.io</span>
            </div>
          </> : <>
            <label htmlFor="admin-email">Email</label>
            <input id="admin-email" type="email" required value={email} onChange={(e) => setEmail(e.target.value)}/>
          </>}
          <label htmlFor="admin-password">Temporary password (minimum 8 characters)</label><input id="admin-password" type="password" autoComplete="new-password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)}/>
        </>}
        {kind === "companies" && <>
          <label htmlFor="admin-company-name">Company name</label><input id="admin-company-name" required maxLength={120} value={name} onChange={(e) => setName(e.target.value)}/>
          <label htmlFor="admin-company-phone">Phone number</label><input id="admin-company-phone" required maxLength={30} value={phone} onChange={(e) => setPhone(e.target.value)}/>
          <label htmlFor="admin-start">Contract start date</label><input id="admin-start" required type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)}/>
          <label htmlFor="admin-end">Contract end date (optional)</label><input id="admin-end" type="date" min={startDate} value={endDate} onChange={(e) => setEndDate(e.target.value)}/>
        </>}
        {kind === "drivers" && <><label htmlFor="admin-license">License number</label><input id="admin-license" required maxLength={50} value={license} onChange={(e) => setLicense(e.target.value)}/></>}
        {(kind === "drivers" || kind === "vehicles") && <>
          <label htmlFor="admin-company-id">Taxi company</label>
          <CompanySelect companies={companies} value={companyId} onChange={setCompanyId}/>
        </>}
        {kind === "vehicles" && <>
          <label htmlFor="admin-plate">Plate number</label><input id="admin-plate" required minLength={3} maxLength={20} value={plate} onChange={(e) => setPlate(e.target.value)}/>
          <label htmlFor="admin-model">Vehicle model</label><input id="admin-model" required minLength={2} maxLength={100} value={model} onChange={(e) => setModel(e.target.value)}/>
        </>}
        <div className="tt-admin-modal-actions"><button type="button" className="tt-admin-outline" disabled={busy} onClick={onClose}>Cancel</button>
          <button type="submit" className="tt-admin-add" disabled={busy}>{busy ? "Saving..." : "Create record"}<ArrowUpRight size={16}/></button></div>
      </form>
    </section>
  </div>;
}
