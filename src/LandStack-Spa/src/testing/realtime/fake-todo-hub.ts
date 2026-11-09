import { vi } from 'vitest';
import { TodoHubConnection } from '../../app/todo-realtime/todo-hub-connection';

export function deferred<T>() {
  let resolve!: (value: T | PromiseLike<T>) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

export class FakeTodoHub implements TodoHubConnection {
  readonly start = vi.fn(() => Promise.resolve());
  readonly stop = vi.fn(() => Promise.resolve());
  readonly send = vi.fn<TodoHubConnection['send']>(() => Promise.resolve());
  readonly invokeResult = vi.fn<(method: string, ...args: unknown[]) => Promise<unknown>>(() => Promise.resolve([]));
  invoke<T>(method: string, ...args: unknown[]): Promise<T> {
    return this.invokeResult(method, ...args) as Promise<T>;
  }
  private handlers = new Map<string, Parameters<TodoHubConnection['on']>[1]>();
  private closed: Parameters<TodoHubConnection['onclose']>[0] = () => undefined;
  private reconnecting: Parameters<TodoHubConnection['onreconnecting']>[0] = () => undefined;
  private reconnected: Parameters<TodoHubConnection['onreconnected']>[0] = () => undefined;

  on: TodoHubConnection['on'] = (name, callback) => { this.handlers.set(name, callback); };
  onclose: TodoHubConnection['onclose'] = callback => { this.closed = callback; };
  onreconnecting: TodoHubConnection['onreconnecting'] = callback => { this.reconnecting = callback; };
  onreconnected: TodoHubConnection['onreconnected'] = callback => { this.reconnected = callback; };

  emit(name: string, value: unknown) { this.handlers.get(name)?.(value); }
  close(error?: Error) { this.closed(error); }
  reconnect(error?: Error) { this.reconnecting(error); }
  connected(connectionId?: string) { this.reconnected(connectionId); }
}

export async function settlePromises() {
  // Flush the start/invoke/finally chains without running scheduled retry timers.
  for (let count = 0; count < 6; count++) await Promise.resolve();
}
