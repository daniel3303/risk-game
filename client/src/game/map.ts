import classic from '../../../content/classic.json';
import type { Continent, TerritoryDefinition } from './types';

export const map = classic as { name: string; continents: Continent[]; territories: TerritoryDefinition[] };
export const playerColors = ['#47b9ed', '#eb383b', '#98c943', '#ecc339', '#dd7932', '#9563c5'];
export const phaseNames = { claim: 'Claim territories', setup: 'Place starting troops', draft: 'Draft', attack: 'Attack', occupy: 'Move after capture', fortify: 'Fortify', finished: 'World conquered' };
