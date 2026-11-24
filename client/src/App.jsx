import { Routes, Route, Navigate, Link, useNavigate, useLocation } from "react-router-dom";
import { auth, CONSOLE_ROLES } from "./api";
import Home from "./pages/Home";
import Login from "./pages/Login";
import CitizenRequest from "./pages/CitizenRequest";
import CoordinatorQueue from "./pages/CoordinatorQueue";
import Donate from "./pages/Donate";
import MapPage from "./pages/MapPage";
import Console, { Dashboard, Cases, Audit } from "./pages/Console";
import { Users, Analytics, Organisations, Crises, Duplicates, Anomalies } from "./pages/Manage";
import Notifier from "./components/Notifier";
import AlertBar from "./components/AlertBar";
import { RequestDetail, Profile, Volunteer } from "./pages/Citizen";
import { Requests, RequestView, Shelters, Resources, Volunteers, Donations, Alerts, Review } from "./pages/Staff";
import Updates, { UpdateDetail } from "./pages/Updates";
import Brand from "./components/Brand";
import ChatBot from "./components/ChatBot";

function Protected({ roles, children }) {
  if (!auth.token) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(auth.role)) return <div className="card">You do not have access to this page.</div>;
  return children;
}

export default function App() {
  const nav = useNavigate();
  const path = useLocation().pathname;
  const onHome = path === "/";
  const inConsole = path.startsWith("/console");
  const wide = ["/login", "/donate", "/map"].includes(path) || path.startsWith("/updates");
  return (
    <>
      {!inConsole && <AlertBar />}
      {!onHome && !inConsole && (
        <header className="bar">
          <Link to="/" className="brand-link"><Brand /></Link>
          <nav>
            <Link to="/donate">Donate</Link>
            <Link to="/map">Shelters map</Link>
            {auth.role === "Citizen" && <Link to="/request">Request help</Link>}
            {auth.role === "Citizen" && <Link to="/profile">My household</Link>}
            {auth.token && <Link to="/volunteer">Volunteer</Link>}
            {CONSOLE_ROLES.includes(auth.role) && <Link to="/console">Console</Link>}
            {auth.token ? (
              <a href="#" onClick={(e) => { e.preventDefault(); auth.clear(); nav("/login"); }}>Log out{auth.name ? ` (${auth.name})` : ""}</a>
            ) : <Link to="/login">Log in</Link>}
          </nav>
        </header>
      )}
      <main className={onHome ? "full" : inConsole ? "console" : wide ? "wide" : ""}>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/login" element={<Login />} />
          <Route path="/donate" element={<Donate />} />
          <Route path="/map" element={<MapPage />} />
          <Route path="/updates" element={<Updates />} />
          <Route path="/updates/:slug" element={<UpdateDetail />} />
          <Route path="/request" element={<Protected roles={["Citizen"]}><CitizenRequest /></Protected>} />
          <Route path="/console" element={<Protected roles={CONSOLE_ROLES}><Console /></Protected>}>
            <Route index element={<Dashboard />} />
            <Route path="cases/:filter" element={<Cases />} />
            <Route path="queue" element={<CoordinatorQueue />} />
            <Route path="users" element={<Users />} />
            <Route path="requests" element={<Requests />} />
            <Route path="requests/:id" element={<RequestView />} />
            <Route path="shelters" element={<Shelters />} />
            <Route path="resources" element={<Resources />} />
            <Route path="volunteers" element={<Volunteers />} />
            <Route path="donations" element={<Donations />} />
            <Route path="alerts" element={<Alerts />} />
            <Route path="review" element={<Review />} />
            <Route path="analytics" element={<Analytics />} />
            <Route path="organisations" element={<Organisations />} />
            <Route path="crises" element={<Crises />} />
            <Route path="duplicates" element={<Duplicates />} />
            <Route path="anomalies" element={<Anomalies />} />
            <Route path="audit" element={<Audit />} />
          </Route>
          <Route path="/request/:id" element={<Protected roles={["Citizen"]}><RequestDetail /></Protected>} />
          <Route path="/profile" element={<Protected><Profile /></Protected>} />
          <Route path="/volunteer" element={<Protected><Volunteer /></Protected>} />
          <Route path="/queue" element={<Navigate to="/console/queue" replace />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      <Notifier />
      <ChatBot />
    </>
  );
}
