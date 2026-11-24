// One brand mark used on every page: English wordmark + Sinhala name.
// light = for dark backgrounds, stack = Sinhala under the English name.
export default function Brand({ light = false, stack = false }) {
  return (
    <span className={"bm" + (light ? " bm-light" : "") + (stack ? " bm-stack" : "")}>
      <span className="bm-en">Suwa<b>rakshaa</b></span>
      <span className="bm-si">සුවරක්ෂා</span>
    </span>
  );
}
