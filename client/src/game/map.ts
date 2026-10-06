import classic from '../../../content/classic.json';
import type { Continent, TerritoryDefinition } from './types';

export const map = classic as { name: string; continents: Continent[]; territories: TerritoryDefinition[] };
export const playerColors = ['#f4c748', '#e95867', '#49b2ef', '#ab70e1', '#5bc98c', '#f39b46'];
export const phaseNames = { claim: 'Claim territories', setup: 'Place starting troops', draft: 'Draft', attack: 'Attack', occupy: 'Move after capture', fortify: 'Fortify', finished: 'World conquered' };
