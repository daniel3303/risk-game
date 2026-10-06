export function CommanderPortrait({ player = 0, bot = false }: { player?: number; bot?: boolean }) {
  const skin = ['#e4ad80', '#b97751', '#edc096', '#d39a72', '#b78156', '#edbc99'][player % 6];
  return <svg className="commander-portrait" viewBox="0 0 100 100" aria-hidden="true">
    <defs><linearGradient id={`coat-${player}-${bot}`} x2="1" y2="1"><stop stopColor="#3b5774" /><stop offset="1" stopColor="#14283e" /></linearGradient></defs>
    <circle cx="50" cy="50" r="48" fill="#213849" />
    <path d="M10 100Q12 77 35 70L65 70Q90 80 93 100Z" fill={`url(#coat-${player}-${bot})`} />
    <path d="M38 69L45 92 50 82 56 93 63 69" fill="#ede4ce" />
    <path d="M27 80L17 85 15 95 32 89ZM73 80L86 85 88 95 70 89Z" fill="#e4b657" />
    <path d="M42 60L41 75Q50 82 60 74L58 59Z" fill={skin} />
    {bot ? <>
      <rect x="28" y="27" width="45" height="42" rx="12" fill="#849ea5" stroke="#162b39" strokeWidth="3" />
      <path d="M24 37Q24 9 51 12 80 12 77 36Z" fill="#354c63" stroke="#d6b963" strokeWidth="3" />
      <rect x="34" y="39" width="32" height="13" rx="5" fill="#102735" />
      <circle cx="43" cy="45" r="3" fill="#77edee" /><circle cx="58" cy="45" r="3" fill="#77edee" />
      <path d="M41 59H60" stroke="#203a4b" strokeWidth="4" strokeLinecap="round" />
    </> : <>
      <path d="M31 34Q30 21 53 22 74 24 72 45L67 63Q53 81 35 63Z" fill={skin} stroke="#543c35" strokeWidth="2" />
      <path d="M32 35L30 53 35 61 39 34ZM67 32L69 55 65 60 63 32Z" fill="#47362e" />
      <path d="M43 40L49 39M58 39L64 41" stroke="#44362e" strokeWidth="3" strokeLinecap="round" />
      <ellipse cx="47" cy="44" rx="2" ry="3" fill="#162836" /><ellipse cx="61" cy="44" rx="2" ry="3" fill="#162836" />
      <path d="M55 45L58 53 52 54" fill="none" stroke="#ac724e" strokeWidth="2" />
      <path d="M44 58Q51 51 55 57 63 51 67 58 62 65 55 60 49 65 44 58Z" fill="#48372f" />
      <path d="M49 65Q55 69 61 64" fill="none" stroke="#a66d50" strokeWidth="2" />
      <path d="M8 31Q27 23 34 8L51 23Q65 8 92 27L79 40Q51 31 22 40Z" fill="#142633" stroke="#e8bc5a" strokeWidth="3" />
      <path d="M34 8L51 23 67 15" fill="none" stroke="#667078" strokeWidth="2" />
      <circle cx="50" cy="28" r="5" fill="#e2ac40" /><circle cx="50" cy="28" r="2" fill="#e36053" />
    </>}
    <path d="M38 80L31 100M64 80L74 100" stroke="#e5bb60" strokeWidth="2" />
    <circle cx="50" cy="93" r="2" fill="#e5bb60" />
  </svg>;
}
