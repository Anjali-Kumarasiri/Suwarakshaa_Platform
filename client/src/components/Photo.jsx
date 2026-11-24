// Tries /images/name.jpg (your own photo); if missing, falls back to the bundled illustration name.svg; if that fails too, hides.
export default function Photo({ src, alt = "", className = "" }) {
  return (
    <img className={className} src={src} alt={alt} loading="lazy" onError={(e) => {
      const el = e.currentTarget;
      if (el.src.endsWith(".jpg")) el.src = src.replace(/\.jpg$/, ".svg"); else el.style.display = "none";
    }} />
  );
}
