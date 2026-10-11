import { useEffect, useState, type FormEvent } from "react";
import {
  ArrowUpRight,
  Eye,
  EyeOff,
  Navigation,
} from "lucide-react";
import {
  BrowserRouter,
  Navigate,
  Route,
  Routes,
  useNavigate,
  Link,
} from "react-router-dom";
import { login, type LoginResponse, type UserRole } from "./api/client";
import SchedulerDashboard from "./features/scheduler/SchedulerDashboard";
import DriverDashboard from "./features/driver/DriverDashboard";
import AdminDashboard from "./features/admin/AdminDashboard";
import { ChangePasswordPage, ForgotPasswordPage, ResetPasswordPage } from "./features/security/SecurityPages";

const SESSION_KEY = "tretrip-session";

function rolePath(role: UserRole): string {
  if (role === "Admin") return "/admin";
  if (role === "Driver") return "/driver";
  return "/scheduler";
}

function isSessionValid(session: LoginResponse | null): session is LoginResponse {
  return Boolean(
    session?.accessToken &&
      session.expiresAt &&
      Number.isFinite(Date.parse(session.expiresAt)) &&
      Date.parse(session.expiresAt) > Date.now() &&
      ["Admin", "Driver", "Scheduler"].includes(session.role)
  );
}

function loadSession(): LoginResponse | null {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const candidate: LoginResponse = JSON.parse(raw);
    if (isSessionValid(candidate)) return candidate;
    sessionStorage.removeItem(SESSION_KEY);
  } catch {
    // Handle invalid storage content or unavailable sessionStorage.
  }
  return null;
}

function Brand({ inverse = false }: { inverse?: boolean }) {
  return (
    <div className={`tt-brand ${inverse ? "tt-brand--inverse" : ""}`} aria-label="TreTrip">
      <span className="tt-brand-mark" aria-hidden="true">
        <Navigation size={17} strokeWidth={3} />
      </span>
      <span>tretrip<span className="tt-brand-period">.</span></span>
    </div>
  );
}

function ArrowButtonContent({ text }: { text: string }) {
  return (
    <>
      <span>{text}</span>
      <span className="tt-button-arrow" aria-hidden="true"><ArrowUpRight size={17} /></span>
    </>
  );
}

function TaxiArtwork() {
  return (
    <div className="tt-taxi-stage">
      <img
        className="tt-taxi-image"
        src="/taxi-illustration-transparent.png"
        alt="Yellow taxi in front of a city skyline, with a booking map and passenger"
        loading="eager"
      />
    </div>
  );
}

function LoginPage({ onLogin }: { onLogin: (session: LoginResponse) => void }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (isLoading) return;
    setError("");
    setIsLoading(true);

    try {
      const session = await login(email.trim(), password);
      if (!isSessionValid(session)) {
        throw new Error("The server returned an invalid or expired session.");
      }
      onLogin(session);
      navigate(session.mustChangePassword ? "/change-password" : rolePath(session.role), { replace: true });
    } catch (reason) {
      setError(
        reason instanceof TypeError
          ? "Cannot reach the API. Make sure the backend is running."
          : reason instanceof Error
            ? reason.message
            : "Sign-in failed. Please try again."
      );
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <main className="tt-auth-layout">
      <section className="tt-auth-story" aria-label="TreTrip taxi service">
        <div className="tt-story-topbar"><Brand /></div>
        <h1 className="tt-hero-title">Your taxi<br />is here<span>.</span></h1>
        <TaxiArtwork />
      </section>

      <section className="tt-auth-panel" aria-labelledby="sign-in-title">
        <div className="tt-auth-mobile-brand"><Brand /></div>
        <div className="tt-auth-card">
          <h2 id="sign-in-title">Sign in<span>.</span></h2>
          <form className="tt-login-form" onSubmit={submit}>
            <label htmlFor="email">Work email</label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              placeholder="name@company.com"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />

            <label htmlFor="password">Password</label>
            <div className="tt-password-control">
              <input
                id="password"
                type={showPassword ? "text" : "password"}
                autoComplete="current-password"
                placeholder="Enter your password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
              <button
                type="button"
                className="tt-toggle-password"
                aria-label={showPassword ? "Hide password" : "Show password"}
                aria-pressed={showPassword}
                onClick={() => setShowPassword((previous) => !previous)}
              >
                {showPassword ? <EyeOff size={19} /> : <Eye size={19} />}
              </button>
            </div>

            {error && <div className="tt-form-error" role="alert">{error}</div>}
            <button type="submit" className="tt-primary-button" disabled={isLoading}>
              <ArrowButtonContent text={isLoading ? "Signing in..." : "Sign in"} />
            </button>
          </form>
          <div style={{marginTop: 16, textAlign: "right"}}><Link to="/forgot-password" style={{fontSize:13,color:"#536b3e",fontWeight:700}}>Forgot password?</Link></div>
        </div>
      </section>
    </main>
  );
}

function App() {
  const [user, setUser] = useState<LoginResponse | null>(loadSession);

  useEffect(() => {
    if (!user) return;
    const timeLeft = Date.parse(user.expiresAt) - Date.now();
    // Schedule expiry cleanup rather than synchronously setting state in an effect.
    const timeout = window.setTimeout(() => {
      sessionStorage.removeItem(SESSION_KEY);
      setUser(null);
    }, Math.max(0, timeLeft));
    return () => window.clearTimeout(timeout);
  }, [user]);

  function handleLogin(session: LoginResponse) {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    setUser(session);
  }

  function handleLogout() {
    sessionStorage.removeItem(SESSION_KEY);
    setUser(null);
  }

  const activeUser = isSessionValid(user) ? user : null;

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to={activeUser ? (activeUser.mustChangePassword ? "/change-password" : rolePath(activeUser.role)) : "/login"} replace />} />
        <Route
          path="/login"
          element={activeUser ? <Navigate to={activeUser.mustChangePassword ? "/change-password" : rolePath(activeUser.role)} replace /> : <LoginPage onLogin={handleLogin} />}
        />
        <Route path="/change-password" element={activeUser ? <ChangePasswordPage user={activeUser} onLogout={handleLogout} /> : <Navigate to="/login" replace />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        {(["Scheduler", "Driver", "Admin"] as UserRole[]).map((role) => (
          <Route
            key={role}
            path={rolePath(role)}
            element={activeUser?.role === role ? (role === "Scheduler" ? <SchedulerDashboard user={activeUser} onLogout={handleLogout} /> : role === "Driver" ? <DriverDashboard user={activeUser} onLogout={handleLogout} /> : <AdminDashboard user={activeUser} onLogout={handleLogout} />) : <Navigate to={activeUser ? (activeUser.mustChangePassword ? "/change-password" : rolePath(activeUser.role)) : "/login"} replace />}
          />
        ))}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
