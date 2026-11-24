import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { UPDATES, getUpdate } from "../data/updates";
import Photo from "../components/Photo";

function Card({ u }) {
  return (
    <Link to={`/updates/${u.slug}`} className="news-link">
      <article className="news">
        <div className="thumb"><Photo src={u.img} /><em>{u.tag}</em></div>
        <small className="muted">{u.date}</small>
        <h3>{u.title}</h3><p>{u.summary}</p>
      </article>
    </Link>
  );
}

export default function Updates() {
  const [tag, setTag] = useState("All");
  const tags = ["All", ...new Set(UPDATES.map((u) => u.tag))];
  return (
    <div>
      <h2>Latest updates</h2>
      <div className="chips">{tags.map((t) => <button key={t} className={tag === t ? "on" : ""} onClick={() => setTag(t)}>{t} ({t === "All" ? UPDATES.length : UPDATES.filter((u) => u.tag === t).length})</button>)}</div>
      <div className="grid3 upd-grid">{[...UPDATES].sort((a, b) => new Date(b.date) - new Date(a.date)).filter((u) => tag === "All" || u.tag === tag).map((u) => <Card key={u.slug} u={u} />)}</div>
    </div>
  );
}

export function UpdateDetail() {
  const u = getUpdate(useParams().slug);
  if (!u) return <div className="card"><p>We could not find that update.</p><Link to="/updates">Back to all updates</Link></div>;
  return (
    <>
      <article className="article">
        <Link to="/updates" className="back">← All updates</Link>
        <div className="art-hero"><Photo src={u.img} /></div>
        <p className="muted"><span className="tagpill">{u.tag}</span> {u.date}</p>
        <h1>{u.title}</h1>
        {u.body.map((b, i) =>
          b.t === "h" ? <h3 key={i}>{b.x}</h3>
          : b.t === "ul" ? <ul key={i}>{b.items.map((x) => <li key={x}>{x}</li>)}</ul>
          : b.t === "ol" ? <ol key={i}>{b.items.map((x) => <li key={x}>{x}</li>)}</ol>
          : b.t === "ex" ? <div className="ex" key={i}><div className="weak"><b>Weak</b><p>{b.weak}</p></div><div className="strong"><b>Strong</b><p>{b.strong}</p></div></div>
          : <p key={i}>{b.x}</p>)}
        {u.cta && <Link to={u.cta[1]} className="cta-btn">{u.cta[0]}</Link>}
      </article>
      <h3 className="more-h">More updates</h3>
      <div className="grid3 upd-grid">{UPDATES.filter((x) => x.slug !== u.slug).map((x) => <Card key={x.slug} u={x} />)}</div>
    </>
  );
}
