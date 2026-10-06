import { Bot } from 'lucide-react';

export function CommanderPortrait({ player = 0, bot = false }: { player?: number; bot?: boolean }) {
  const index = player % 6;
  return <div className="commander-portrait" aria-hidden="true">
    <div className="portrait-art"><img src="/assets/commanders.png" alt="" draggable={false} style={{ left: `${index % 3 * -100}%`, top: `${Math.floor(index / 3) * -100}%` }} /></div>
    <svg className="portrait-wreath" viewBox="0 0 100 100">
      <path d="M13 54Q11 80 44 94M87 54Q89 80 56 94" fill="none" stroke="#b17b25" strokeWidth="2" />
      {Array.from({ length: 8 }, (_, i) => <g key={i} fill="#e8c65d" stroke="#855a20" strokeWidth=".5"><ellipse cx={11 + i * 3.5} cy={59 + i * 4.5} rx="3" ry="6" transform={`rotate(-38 ${11 + i * 3.5} ${59 + i * 4.5})`} /><ellipse cx={89 - i * 3.5} cy={59 + i * 4.5} rx="3" ry="6" transform={`rotate(38 ${89 - i * 3.5} ${59 + i * 4.5})`} /></g>)}
    </svg>
    {bot && <span className="portrait-bot"><Bot size={18} strokeWidth={3} /></span>}
  </div>;
}
