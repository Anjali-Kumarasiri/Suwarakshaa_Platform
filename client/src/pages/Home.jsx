import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import "../home.css";
import Photo from "../components/Photo";
import Brand from "../components/Brand";
import { HOME_SLUGS, getUpdate } from "../data/updates";

// Layout inspired by NGO sites (hero slider, solutions, impact, news, partners). All content is Suwarakshaa's own.
const SLIDES = [
  { t: "Help reaches people faster when every request is seen.", p: "Ask for food, medicine, water or shelter in one place. AI suggests, people decide.", cta: ["Request help", "/request"] },
  { t: "Every decision is reviewed and recorded.", p: "Coordinators approve, modify, reject or defer each AI suggestion, with a full audit trail.", cta: ["See how it works", "#how"] },
  { t: "Neighbours helping neighbours.", p: "Volunteers, shelters and suppliers work from one shared picture of what is needed.", cta: ["Get involved", "#involved"] },
];
const SOLUTIONS = [
  ["💧", "Clean water", "Drinking water and purification supplies."],
  ["🍚", "Food", "Dry rations and hot meals for families."],
  ["💊", "Medicine", "Urgent prescriptions and first aid."],
  ["🏠", "Shelter", "Safe places with space and facilities."],
  ["🚐", "Transport", "Evacuation and delivery routes."],
  ["♿", "Accessibility", "Support for elderly and disabled people."],
];
const STATS = [[1200, "+", "Requests handled"], [48, "", "Shelters listed"], [350, "+", "Volunteers"], [25, "", "Districts covered"]]; // demo numbers: replace with real data
const PARTNERS = ["Local councils", "Health services", "Red Cross units", "Transport associations", "Community groups", "Universities"];

function A({ to, className, children }) {
  return to.startsWith("#") ? <a href={to} className={className}>{children}</a> : <Link to={to} className={className}>{children}</Link>;
}

function Count({ to, suffix }) {
  const [n, setN] = useState(0);
  const ref = useRef(null);
  useEffect(() => {
    const io = new IntersectionObserver(([e]) => {
      if (!e.isIntersecting) return;
      io.disconnect();
      const t0 = performance.now();
      const step = (t) => { const k = Math.min((t - t0) / 1200, 1); setN(Math.round(to * k)); if (k < 1) requestAnimationFrame(step); };
      requestAnimationFrame(step);
    });
    io.observe(ref.current);
    return () => io.disconnect();
  }, [to]);
  return <span ref={ref}>{n}{suffix}</span>;
}

export default function Home() {
  const [i, setI] = useState(0);
  useEffect(() => { const id = setInterval(() => setI((x) => (x + 1) % SLIDES.length), 7000); return () => clearInterval(id); }, []);
  const go = (d) => setI((x) => (x + d + SLIDES.length) % SLIDES.length);
  const s = SLIDES[i];

  return (
    <div className="home">
      <div className="topbar">In immediate danger? Call your local emergency number first, then submit a request here.</div>
      <header className="nav">
        <Link to="/" className="brand-link"><Brand /></Link>
        <nav className="nav-links">
          <a href="#how">How it works</a>
          <div className="dd"><a href="#services">Services ▾</a>
            <div className="dd-menu">{SOLUTIONS.map(([, n]) => <a key={n} href="#services">{n}</a>)}</div>
          </div>
          <a href="#impact">Impact</a><a href="#news">Updates</a><a href="#involved">Get involved</a><Link to="/map">Shelters map</Link>
        </nav>
        <div className="nav-cta"><Link to="/login" className="btn ghost">Log in</Link><Link to="/request" className="btn solid">Get help</Link></div>
      </header>

      <section className="hero" aria-roledescription="carousel">
        <Photo src="/images/hero.jpg" className="hero-bg" />
        <span className="hero-credit">Photo: Ishara S. Kodikara / AFP</span>
        <div className="hero-inner" key={i}>
          <h1>{s.t}</h1>
          <p>{s.p}</p>
          <A to={s.cta[1]} className="btn amber big">{s.cta[0]}</A>
        </div>
        <div className="hero-ctrl">
          <button onClick={() => go(-1)} aria-label="Previous slide">‹</button>
          {SLIDES.map((_, n) => <button key={n} className={"dot" + (n === i ? " on" : "")} onClick={() => setI(n)} aria-label={`Slide ${n + 1}`} />)}
          <button onClick={() => go(1)} aria-label="Next slide">›</button>
        </div>
      </section>

      <section className="intro">
        <h2>People first, with AI as an assistant.</h2>
        <p>Suwarakshaa gathers requests for help, sorts them by urgency and suggests matches to shelters, supplies and volunteers. A coordinator reviews every suggestion before anything happens.</p>
      </section>

      <div className="gallery">
        {[["flood.jpg", "Flood response teams"], ["shelter.jpg", "Safe shelters for families"], ["volunteers.jpg", "Volunteers on the ground"]].map(([f, c]) => (
          <figure key={f}><Photo src={"/images/" + f} alt={c} /><figcaption>{c}</figcaption></figure>
        ))}
      </div>

      <section className="sec soft" id="how">
        <h2>From request to help</h2>
        <div className="steps">
          {[["Ask for help", "Tell us what you need and where you are."], ["AI suggests", "Requests are sorted and possible matches are found."], ["People decide", "A coordinator approves, changes or rejects each suggestion."], ["Help arrives", "You get updates until the request is closed."]].map(([h, p], n) => (
            <div className="step" key={h}><span className="num">{n + 1}</span><h3>{h}</h3><p>{p}</p></div>
          ))}
        </div>
      </section>

      <section className="sec" id="services">
        <h2>What we coordinate</h2>
        <div className="grid">{SOLUTIONS.map(([ic, h, p]) => <div className="svc" key={h}><span>{ic}</span><h3>{h}</h3><p>{p}</p></div>)}</div>
      </section>

      <section className="impact" id="impact">
        <h2>Our reach so far</h2>
        <div className="stats">{STATS.map(([n, suf, l]) => <div key={l}><strong><Count to={n} suffix={suf} /></strong><span>{l}</span></div>)}</div>
      </section>

      <section className="donate" id="donate">
        <div><h2>Your support keeps supplies moving.</h2><p>Fund water, food and medicine, and see where every contribution goes.</p></div>
        <Link to="/donate" className="btn amber big">Donate</Link>
      </section>

      <section className="sec" id="news">
        <div className="head-row"><h2>Latest updates</h2><Link to="/updates" className="more">View all</Link></div>
        <div className="grid3">{HOME_SLUGS.map(getUpdate).map((u) => <Link to={`/updates/${u.slug}`} className="news-link" key={u.slug}><article className="news"><div className="thumb"><Photo src={u.img} /><em>{u.tag}</em></div><h3>{u.title}</h3><p>{u.summary}</p></article></Link>)}</div>
      </section>

      <section className="sec soft" id="involved">
        <h2>Get involved</h2>
        <div className="grid3">
          <div className="gcard"><h3>Volunteer</h3><p>Share your skills and availability. We match you to tasks near you.</p><Link to="/volunteer">Register as a volunteer</Link></div>
          <div className="gcard"><h3>Donate</h3><p>Fund supplies and see how every contribution is used.</p><Link to="/donate">Support a campaign</Link></div>
          <div className="gcard"><h3>Partner</h3><p>NGOs and agencies coordinate resources through one platform.</p><a href="#partners">See our partners</a></div>
        </div>
      </section>

      <section className="sec" id="partners">
        <h2>Working with</h2>
        <div className="marquee"><div className="track">{[...PARTNERS, ...PARTNERS].map((p, i) => <div className="partner" key={i} aria-hidden={i >= PARTNERS.length}><b>{p[0]}</b>{p}</div>)}</div></div>
      </section>

      <footer className="footer">
        <div><div className="brand-link foot"><Brand light /></div><p>Coordinating crisis response, with people in control.</p></div>
        <div><h4>Quick links</h4><a href="#how">How it works</a><a href="#services">Services</a><a href="#news">Updates</a></div>
        <div><h4>Account</h4><Link to="/login">Log in</Link><Link to="/request">Request help</Link></div>
        <div><h4>Contact</h4><p>relief@example.com</p><p>Colombo, Sri Lanka</p></div>
        <div className="copy">© 2026 Suwarakshaa · SE205.3 coursework prototype</div>
      </footer>
    </div>
  );
}
