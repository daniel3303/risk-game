import { useEffect, useRef, useState } from 'react';
import { RotateCcw } from 'lucide-react';
import type { GameView } from '../game/types';
import { Board } from '../render/board';

export function WorldBoard({ game, selected = null, reachable = [], onSelect, preview = false }: { game: GameView | null; selected?: number | null; reachable?: number[]; onSelect: (id: number) => void; preview?: boolean }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const art = useRef<Board | null>(null);
  const callback = useRef(onSelect);
  callback.current = onSelect;
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    try { art.current = new Board(canvas.current!, id => callback.current(id), preview); }
    catch { setFailed(true); }
    return () => { art.current?.dispose(); art.current = null; };
  }, [preview]);
  useEffect(() => { art.current?.update(game, selected, reachable); }, [game, selected, reachable]);
  return <div className={`world-board ${preview ? 'preview-board' : ''}`}>
    <canvas ref={canvas} aria-label="Interactive 3D Classic world map. Drag to rotate, scroll to zoom; use Territory list for keyboard controls." data-testid="world-board" />
    {failed && <div className="board-fallback">3D graphics are unavailable on this device. Use the Territory list to play.</div>}
    {!preview && <><div className="board-caption">CLASSIC WORLD <span>42 TERRITORIES · 6 CONTINENTS</span></div><button className="camera-reset icon-button" aria-label="Reset camera" onClick={() => art.current?.resetCamera()}><RotateCcw size={17} /></button><div className="board-hint">Drag to rotate · Scroll to zoom · Select a territory</div></>}
  </div>;
}
