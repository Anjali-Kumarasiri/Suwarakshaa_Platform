import { useEffect, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { api } from "../api";

const km = (a, b, c, d) => { const r = Math.PI / 180, x = Math.sin((c - a) * r / 2) ** 2 + Math.cos(a * r) * Math.cos(c * r) * Math.sin((d - b) * r / 2) ** 2; return 12742 * Math.asin(Math.sqrt(x)); };

export default function MapPage() {
  const el = useRef(null), map = useRef(null), me = useRef(null);
  const [shelters, setShelters] = useState([]);
  const [pos, setPos] = useState(null);
  const [note, setNote] = useState("");   // use state for take notes

  useEffect(() => {
    map.current = L.map(el.current).setView([7.2, 79.87], 9);
    L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", { maxZoom: 18, attribution: "© OpenStreetMap" }).addTo(map.current);
    api.publicShelters().then(setShelters).catch((e) => setNote(e.message));
    let id;
    if (navigator.geolocation) {
      id = navigator.geolocation.watchPosition(
        (p) => setPos({ lat: p.coords.latitude, lng: p.coords.longitude }),
        () => setNote("Location is off. Allow it in the browser to see where you are."), { enableHighAccuracy: true });
    }
    return () => { if (id) navigator.geolocation.clearWatch(id); map.current.remove(); };
  }, []);

  useEffect(() => {
    shelters.forEach((s) => L.circleMarker([s.latitude, s.longitude], { radius: 9, color: "#0d5c63", fillColor: "#0d5c63", fillOpacity: 0.8 })
      .bindPopup(`<b>${s.name}</b><br>${s.address ?? ""}<br>${s.spaces} spaces free${s.isAccessible ? " · accessible" : ""}`).addTo(map.current));
  }, [shelters]);

  useEffect(() => {
    if (!pos) return;
    if (me.current) me.current.setLatLng([pos.lat, pos.lng]);
    else { me.current = L.circleMarker([pos.lat, pos.lng], { radius: 8, color: "#fff", weight: 3, fillColor: "#e4572e", fillOpacity: 1 }).bindPopup("You are here").addTo(map.current); map.current.setView([pos.lat, pos.lng], 12); }
  }, [pos]);

  const near = pos ? [...shelters].map((s) => ({ ...s, d: km(pos.lat, pos.lng, s.latitude, s.longitude) })).sort((a, b) => a.d - b.d) : shelters;

  return (
    <div>
      <h2>Shelters near you</h2>
      <div ref={el} className="map" />
      {note && <p className="error">{note}</p>}
      <div className="card">
        {near.length === 0 && <p className="muted">No public shelters listed yet.</p>}
        {near.map((s) => <p key={s.shelterId}><strong>{s.name}</strong> · {s.spaces} spaces{s.d != null && ` · ${s.d.toFixed(1)} km away`}<br /><span className="muted">{s.address}</span></p>)}
      </div>
    </div>
  );
}
