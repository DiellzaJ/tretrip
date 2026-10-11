import { useState, type FormEvent, type ReactNode } from "react";
import { ArrowRight, LockKeyhole, ShieldCheck } from "lucide-react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { API_BASE_URL, type LoginResponse } from "../../api/client";
import "./security.css";

async function postJson(path: string, body: object, token?: string): Promise<void> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const data: { message?: string } = await response.json().catch(() => ({}));
    throw new Error(data.message ?? `Request failed (${response.status})`);
  }
}

function Frame({ title, detail, children }: { title: string; detail?: string; children: ReactNode }) {
  return (
    <main className="security-page">
      <div className="security-panel">
        <Link className="security-brand" to="/">↗ tretrip<span>.</span></Link>
        <span className="security-tag"><ShieldCheck size={15} /> ACCOUNT SECURITY</span>
        <h1>{title}<span>.</span></h1>
        {detail && <p>{detail}</p>}
        {children}
      </div>
    </main>
  );
}

export function ChangePasswordPage({ user, onLogout }: {
  user: LoginResponse;
  onLogout: () => void;
}) {
  const navigate = useNavigate();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next !== confirm) { setError("Passwords do not match."); return; }
    setBusy(true); setError("");
    try {
      await postJson("/api/auth/change-password", {
        currentPassword: current,
        newPassword: next,
      }, user.accessToken);
      onLogout(); // Server invalidates all previously issued JWTs through password version.
      navigate("/login?passwordChanged=1", { replace: true });
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not update password.");
    } finally { setBusy(false); }
  }

  return <Frame title="Set your password" detail={user.mustChangePassword
    ? "For your security, please replace the temporary password before using TreTrip."
    : "Choose a new password for your TreTrip account."}>
    <form className="security-form" onSubmit={submit}>
      <label>Current password<input type="password" autoComplete="current-password" value={current} onChange={e=>setCurrent(e.target.value)} required /></label>
      <label>New password<input type="password" autoComplete="new-password" minLength={12} maxLength={128} value={next} onChange={e=>setNext(e.target.value)} required /></label>
      <label>Confirm new password<input type="password" autoComplete="new-password" minLength={12} value={confirm} onChange={e=>setConfirm(e.target.value)} required /></label>
      <span className="security-note"><LockKeyhole size={15}/> Use at least 12 characters; a long passphrase is recommended.</span>
      {error && <div className="security-error" role="alert">{error}</div>}
      <button className="security-submit" disabled={busy}>{busy ? "Updating…" : "Update password"}<ArrowRight size={19}/></button>
    </form>
    {!user.mustChangePassword && <Link className="security-back" to="/">Back to dashboard</Link>}
  </Frame>;
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError("");
    try {
      await postJson("/api/auth/forgot-password", { email: email.trim() });
      setDone(true);
    } catch (e) { setError(e instanceof Error ? e.message : "Request failed."); }
    finally { setBusy(false); }
  }

  return <Frame title="Forgot password" detail="Enter your work email. We'll send a one-time reset link if this account exists.">
    {done ? <div className="security-success">If the account exists, check your email for a link valid for 15 minutes.</div> :
      <form className="security-form" onSubmit={submit}>
        <label>Work email<input type="email" autoComplete="email" value={email} onChange={e=>setEmail(e.target.value)} required placeholder="name@company.com" /></label>
        {error && <div className="security-error" role="alert">{error}</div>}
        <button className="security-submit" disabled={busy}>{busy ? "Sending…" : "Send reset link"}<ArrowRight size={19}/></button>
      </form>}
    <Link className="security-back" to="/login">Back to sign in</Link>
  </Frame>;
}

export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const email = params.get("email") ?? "";
  const token = params.get("token") ?? "";
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (next !== confirm) { setError("Passwords do not match."); return; }
    setBusy(true); setError("");
    try {
      await postJson("/api/auth/reset-password", { email, token, newPassword: next });
      setDone(true);
    } catch (e) { setError(e instanceof Error ? e.message : "Reset failed."); }
    finally { setBusy(false); }
  }

  return <Frame title="Reset password" detail="Choose a new password for your account.">
    {done ? <div className="security-success">Your password has been updated. <Link to="/login">Sign in again</Link>.</div>
      : !email || !token ? <div className="security-error">Reset link is missing required information.</div>
      : <form className="security-form" onSubmit={submit}>
        <label>New password<input type="password" autoComplete="new-password" minLength={12} maxLength={128} value={next} onChange={e=>setNext(e.target.value)} required /></label>
        <label>Confirm new password<input type="password" autoComplete="new-password" minLength={12} value={confirm} onChange={e=>setConfirm(e.target.value)} required /></label>
        {error && <div className="security-error" role="alert">{error}</div>}
        <button className="security-submit" disabled={busy}>{busy ? "Updating…" : "Reset password"}<ArrowRight size={19}/></button>
      </form>}
    <Link className="security-back" to="/login">Back to sign in</Link>
  </Frame>;
}
