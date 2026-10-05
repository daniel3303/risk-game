import classic from '../../../content/classic.json';
import type { Continent, TerritoryDefinition } from './types';

export const map = classic as { name: string; continents: Continent[]; territories: TerritoryDefinition[] };
export const playerColors = ['#e2b966', '#77aca1', '#bd7a77', '#889dce', '#b296c8', '#cba16f'];
export const phaseNames = { claim: 'Claim territories', setup: 'Place starting troops', draft: 'Draft', attack: 'Attack', occupy: 'Move after capture', fortify: 'Fortify', finished: 'World conquered' };
