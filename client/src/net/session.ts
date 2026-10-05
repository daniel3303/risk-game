import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { Command, Options, Snapshot, Welcome } from '../game/types';
import { acceptsSnapshot } from '../game/moves';
import { actionId } from './action-id';

interface SavedSeat { code: string; token: string; seat: number; name: string }
interface SessionState { room: Snapshot | null; seat: number; status: 'offline' | 'connecting' | 'connected' | 'reconnecting'; error: string; pending: boolean }
const storageKey = 'risk-game-seat';

export class Session {
  private connection = new HubConnectionBuilder().withUrl('/play').withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.Error).build();
  private listeners = new Set<() => void>();
  private saved: SavedSeat | null = null;
  private state: SessionState = { room: null, seat: -1, status: 'offline', error: '', pending: false };
  private starting: Promise<void> | null = null;
  private restored = false;
  subscribe = (listener: () => void) => { this.listeners.add(listener); return () => { this.listeners.delete(listener); }; };
  getSnapshot = () => this.state;

  constructor() {
    this.connection.on('Snapshot', (room: Snapshot) => this.receive(room));
    this.connection.onreconnecting(() => this.update({ status: 'reconnecting' }));
    this.connection.onreconnected(() => { void this.restoreConnection(); });
    this.connection.onclose(() => this.update({ status: 'offline', pending: false }));
  }

  private update(patch: Partial<SessionState>) { this.state = { ...this.state, ...patch }; this.listeners.forEach(listener => listener()); }
  private receive(room: Snapshot) {
    if (this.state.seat < 0 || (this.saved && this.saved.code !== room.code)) return;
    if (acceptsSnapshot(this.state.room, room)) this.update({ room });
  }
  clearError = () => this.update({ error: '' });

  private async connect() {
    if (this.connection.state === HubConnectionState.Connected) return;
    if (this.starting) return this.starting;
    this.update({ status: 'connecting' });
    this.starting = this.connection.start();
    try { await this.starting; this.update({ status: 'connected' }); }
    finally { this.starting = null; }
  }

  async invoke(method: string, ...args: unknown[]) {
    if (this.state.pending) return;
    this.update({ pending: true, error: '' });
    try { await this.connect(); await this.connection.invoke(method, ...args); }
    catch (error) { this.failure(error); }
    finally { this.update({ pending: false }); }
  }

  private failure(error: unknown) {
    const message = error instanceof Error ? error.message.replace(/^.*HubException: /, '').replace(/^Failed to invoke '[^']+' due to an error on the server\. /, '') : 'Connection failed.';
    this.update({ error: message, status: this.connection.state === HubConnectionState.Connected ? 'connected' : 'offline' });
  }

  private welcome(welcome: Welcome, name: string) {
    this.saved = { code: welcome.code, token: welcome.token, seat: welcome.seat, name };
    sessionStorage.setItem(storageKey, JSON.stringify(this.saved));
    this.update({ seat: welcome.seat, status: 'connected' });
    this.receive(welcome.snapshot);
  }

  async create(name: string, options: Options) {
    await this.enter('Create', name, [name, options]);
  }
  async join(code: string, name: string) {
    await this.enter('Join', name, [code.toUpperCase(), name, null]);
  }
  private async enter(method: string, name: string, args: unknown[]) {
    if (this.state.pending) return;
    this.update({ pending: true, error: '' });
    try { await this.connect(); this.welcome(await this.connection.invoke<Welcome>(method, ...args), name); }
    catch (error) { this.failure(error); }
    finally { this.update({ pending: false }); }
  }

  async restore() {
    if (this.restored) return;
    this.restored = true;
    const text = sessionStorage.getItem(storageKey);
    if (!text) return;
    try { this.saved = JSON.parse(text) as SavedSeat; await this.connect(); await this.restoreConnection(); }
    catch (error) { this.failure(error); }
  }

  retry = async () => {
    try { await this.connect(); await this.restoreConnection(); }
    catch (error) { this.failure(error); }
  };

  private async restoreConnection() {
    if (!this.saved) { this.update({ status: 'connected' }); return; }
    this.update({ status: 'reconnecting' });
    try {
      const { code, name, token } = this.saved;
      this.welcome(await this.connection.invoke<Welcome>('Join', code, name, token), name);
    } catch (error) {
      this.failure(error);
      this.update({ status: 'offline' });
    }
  }

  act(command: Command) {
    if (!this.state.room || this.state.status !== 'connected') return;
    return this.invoke('Act', { id: actionId(), revision: this.state.room.revision, command });
  }

  async leave() {
    if (this.state.pending) return;
    if (this.connection.state === HubConnectionState.Connected) {
      this.update({ error: '' });
      try { await this.connection.invoke('Leave'); }
      catch (error) { this.failure(error); return; }
    }
    this.saved = null;
    sessionStorage.removeItem(storageKey);
    this.update({ room: null, seat: -1 });
    history.replaceState({}, '', location.pathname);
  }
}

export const session = new Session();
