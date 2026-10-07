export type Phase = 'claim' | 'setup' | 'draft' | 'attack' | 'occupy' | 'fortify' | 'finished';
export type Difficulty = 'easy' | 'normal' | 'hard' | 'expert' | 'master' | 'ultimate';
export type CardMode = 'fixed' | 'progressive';
export type SetupMode = 'automatic' | 'manual';
export type CardSymbol = 'infantry' | 'cavalry' | 'artillery' | 'wild';
export interface Options { cards: CardMode; setup: SetupMode }
export interface TerritoryDefinition { id: number; key: string; name: string; continent: string; x: number; z: number; neighbors: number[]; shape: number[][]; parts?: number[][][] }
export interface Continent { id: string; name: string; bonus: number; color: string }
export interface Territory { id: number; owner: number; troops: number }
export interface Card { id: number; territory: number; symbol: CardSymbol }
export interface Capture { from: number; to: number; minimum: number; maximum: number }
export interface Battle { from: number; to: number; attackerLosses: number; defenderLosses: number; attackDice: number[]; defendDice: number[]; captured: boolean }
export interface Player { id: number; name: string; isBot: boolean; difficulty: Difficulty; strategy: string | null; connected: boolean; eliminated: boolean; cards: number; territories: number; troops: number }
export interface GameView { phase: Phase; currentPlayer: number; round: number; reinforcements: number; trades: number; winner: number; setupTroops: number; forcedTrade: boolean; territories: Territory[]; hand: Card[]; capture: Capture | null; battle: Battle | null; log: string[] }
export interface Spectator { id: number; name: string; connected: boolean }
export interface Snapshot { code: string; revision: number; host: number; options: Options; players: Player[]; game: GameView | null; aiOnly: boolean; spectators: Spectator[] }
export interface Welcome { code: string; token: string; seat: number; snapshot: Snapshot }
export interface Command { kind: 'claim' | 'place' | 'trade' | 'attack' | 'occupy' | 'endAttack' | 'fortify' | 'endTurn' | 'surrender'; from?: number; to?: number; count?: number; dice?: number; blitz?: boolean; cards?: number[]; bonusTerritory?: number }
