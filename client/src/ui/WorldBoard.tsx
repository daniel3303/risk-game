import { useEffect, useRef, useState } from 'react';
import { Minus, Plus, RotateCcw } from 'lucide-react';
import type { GameView } from '../game/types';
import { Board } from '../render/board';
import { map, playerColors } from '../game/map';

export function WorldBoard({ game, selected = null, reachable = [], onSelect, preview = false, continentOverlay = false, viewer = -1 }: { game: GameView | null; selected?: number | null; reachable?: number[]; onSelect: (id: number) => void; preview?: boolean; continentOverlay?: boolean; viewer?: number }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const labels = useRef<HTMLDivElement>(null);
  const art = useRef<Board | null>(null);
  const callback = useRef(onSelect);
  callback.current = onSelect;
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    try { art.current = new Board(canvas.current!, labels.current!, id => callback.current(id), preview); }
    catch { setFailed(true); }
    return () => { art.current?.dispose(); art.current = null; };
  }, [preview]);
  useEffect(() => { art.current?.update(game, selected, reachable, continentOverlay, viewer); }, [game, selected, reachable, continentOverlay, viewer]);
  return <div className={`world-board ${preview ? 'preview-board' : ''}`}>
    <canvas ref={canvas} aria-label="Interactive 3D Classic world map. Drag to pan, scroll to zoom; use Territory list for keyboard controls." data-testid="world-board" />
    <div ref={labels} className={`map-labels ${continentOverlay ? 'continent-mode' : ''}`} aria-hidden={preview}>{map.territories.map(territory => {
      const state = game?.territories[territory.id];
      const color = state && state.owner >= 0 ? playerColors[state.owner] : map.continents.find(c => c.id === territory.continent)!.color;
      return <button key={territory.id} data-map-label={territory.id} data-testid={`map-territory-${territory.id}`} className={`map-label ${selected === territory.id ? 'selected' : ''} ${reachable.includes(territory.id) ? 'reachable' : ''}`} style={{ '--army-color': color } as React.CSSProperties} title={`${territory.name} · ${state?.troops ?? 0} troops`} aria-label={`${territory.name}, ${state?.troops ?? 0} troops${state && state.owner >= 0 ? state.owner === viewer ? ', your territory' : `, commander ${state.owner + 1}` : ', unclaimed'}`} tabIndex={preview ? -1 : 0} onClick={() => !preview && onSelect(territory.id)}><span className="troop-counter">{state?.troops ?? (territory.id % 4 + 1)}</span><span className="territory-name">{territory.name.replace('Northwest Territory', 'Northwest\nTerritory').replace('United States', 'U.S.').replace('Northern', 'N.').replace('Southern', 'S.').replace('Western', 'W.').replace('Eastern', 'E.')}</span></button>;
    })}{map.continents.map(continent => {
      const region = map.territories.filter(t => t.continent === continent.id);
      const owned = region.filter(t => game?.territories[t.id].owner === viewer).length;
      return <div key={continent.id} data-continent-label={continent.id} className="continent-label" style={{ '--continent-color': continent.color } as React.CSSProperties}><strong>+{continent.bonus}</strong><span>{continent.name}</span><small>{viewer < 0 ? `${region.length} territories` : `${owned}/${region.length} territories`}</small></div>;
    })}</div>
    {!failed && <div className="board-loading" role="status"><span />Preparing the world…</div>}
    {failed && <div className="board-fallback">3D graphics are unavailable on this device. Use the Territory list to play.</div>}
    {!preview && <><div className="board-caption">CLASSIC WORLD <span>42 TERRITORIES · 6 CONTINENTS</span></div><div className="camera-controls"><button className="icon-button" aria-label="Zoom in" onClick={() => art.current?.zoom(-1)}><Plus size={19} /></button><button className="icon-button" aria-label="Zoom out" onClick={() => art.current?.zoom(1)}><Minus size={19} /></button><button className="icon-button" aria-label="Reset camera" onClick={() => art.current?.resetCamera()}><RotateCcw size={17} /></button></div><div className="board-hint">Drag to pan · Pinch or scroll to zoom</div></>}
  </div>;
}
