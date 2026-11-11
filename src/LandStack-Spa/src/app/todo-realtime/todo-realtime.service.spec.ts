import { TestBed } from '@angular/core/testing';
import { HubConnectionBuilder, IRetryPolicy } from '@microsoft/signalr';
import { Subscription } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../environments/environment';
import { deferred, FakeTodoHub, settlePromises } from '../../testing/realtime/fake-todo-hub';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { BusyService } from '../busy/busy.service';
import { TODO_HUB_CONNECTION_FACTORY } from './todo-hub-connection';
import { TodoRealtimeService } from './todo-realtime.service';

describe('TodoRealtimeService transport and lifecycle', () => {
  let hub: FakeTodoHub;
  let service: TodoRealtimeService;
  let todos: ToDoItemDto[];
  let connected: boolean;
  let busy: boolean;
  let subscriptions: Subscription[];
  const item: ToDoItemDto = {
    toDoItemId: 'item-1', description: 'Realtime task', isCompleted: false,
    createdDate: new Date('2026-11-09'),
  };

  beforeEach(() => {
    vi.useFakeTimers();
    hub = new FakeTodoHub();
    TestBed.configureTestingModule({ providers: [
      { provide: TODO_HUB_CONNECTION_FACTORY, useValue: () => hub },
    ] });
    service = TestBed.inject(TodoRealtimeService);
    todos = [];
    connected = false;
    busy = false;
    subscriptions = [
      service.todos$.subscribe(value => todos = value),
      service.isConnected$.subscribe(value => connected = value),
      TestBed.inject(BusyService).isBusy.subscribe(value => busy = value),
    ];
  });

  afterEach(async () => {
    await service.stopConnection();
    subscriptions.forEach(subscription => subscription.unsubscribe());
    expect(vi.getTimerCount()).toBe(0);
    vi.useRealTimers();
  });

  it('starts once, requests GetAllItems and publishes the initial list', async () => {
    hub.invokeResult.mockResolvedValueOnce([item]);
    service.startConnection();
    service.startConnection();
    await settlePromises();
    expect(hub.start).toHaveBeenCalledOnce();
    expect(hub.invokeResult).toHaveBeenCalledExactlyOnceWith('GetAllItems');
    expect(connected).toBe(true);
    expect(todos).toEqual([item]);
    expect(busy).toBe(false);
  });

  it('applies camel-case creation/update/ID deletion events without changing other items', async () => {
    const other = { ...item, toDoItemId: 'other', description: 'Other task' };
    hub.invokeResult.mockResolvedValueOnce([item]);
    service.startConnection();
    await settlePromises();
    hub.emit('itemCreated', other);
    expect(todos).toEqual([item, other]);
    const updated = { ...item, isCompleted: true };
    hub.emit('itemUpdated', updated);
    expect(todos).toEqual([updated, other]);
    hub.emit('itemDeleted', 'item-1');
    expect(todos).toEqual([other]);
  });

  it('updates connection flags through reconnect/close callbacks', async () => {
    service.startConnection();
    await settlePromises();
    expect(connected).toBe(true);
    hub.reconnect(new Error('temporary disconnect'));
    expect(connected).toBe(false);
    hub.connected('new-connection-id');
    expect(connected).toBe(true);
    hub.close();
    expect(connected).toBe(false);
    expect(hub.invokeResult).toHaveBeenCalledOnce();
  });

  it.each(['Create', 'ToggleComplete', 'Delete'] as const)(
    'sends %s with its unchanged payload and releases busy after success', async command => {
      const pending = deferred<void>();
      hub.send.mockReturnValueOnce(pending.promise);
      if (command === 'Create') service.create({ description: 'New task' });
      else if (command === 'ToggleComplete') service.toggleComplete('item-1');
      else service.delete('item-1');
      expect(hub.send).toHaveBeenCalledExactlyOnceWith(
        command, command === 'Create' ? { description: 'New task' } : 'item-1'
      );
      vi.advanceTimersByTime(250);
      expect(busy).toBe(true);
      pending.resolve();
      await settlePromises();
      expect(busy).toBe(false);
    }
  );

  it.each(['Create', 'ToggleComplete', 'Delete'] as const)(
    'settles a rejected %s send without unhandled promises or stuck busy state', async command => {
      const pending = deferred<void>();
      hub.send.mockReturnValueOnce(pending.promise);
      if (command === 'Create') service.create({ description: 'Failed task' });
      else if (command === 'ToggleComplete') service.toggleComplete('item-1');
      else service.delete('item-1');
      vi.advanceTimersByTime(250);
      expect(busy).toBe(true);
      pending.reject(new Error('Send rejected'));
      await settlePromises();
      expect(busy).toBe(false);
    }
  );

  it('retries rejected startup after exactly10 seconds', async () => {
    hub.start.mockRejectedValueOnce(new Error('Startup rejected'));
    service.startConnection();
    await settlePromises();
    expect(connected).toBe(false);
    expect(hub.invokeResult).not.toHaveBeenCalled();
    vi.advanceTimersByTime(9999);
    expect(hub.start).toHaveBeenCalledOnce();
    vi.advanceTimersByTime(1);
    await settlePromises();
    expect(hub.start).toHaveBeenCalledTimes(2);
    expect(connected).toBe(true);
    expect(hub.invokeResult).toHaveBeenCalledExactlyOnceWith('GetAllItems');
  });

  it('cancels scheduled startup retries on stop and can subsequently start again', async () => {
    hub.start.mockRejectedValueOnce(new Error('Startup rejected'));
    service.startConnection();
    await settlePromises();
    await service.stopConnection();
    expect(vi.getTimerCount()).toBe(0);
    vi.advanceTimersByTime(20000);
    expect(hub.start).toHaveBeenCalledOnce();
    service.startConnection();
    await settlePromises();
    expect(hub.start).toHaveBeenCalledTimes(2);
    expect(connected).toBe(true);
  });

  it.each([true, false])('ignores late startup completion/rejection after stop (success=%s)', async success => {
    const pending = deferred<void>();
    hub.start.mockReturnValueOnce(pending.promise);
    service.startConnection();
    await service.stopConnection();
    if (success) pending.resolve();
    else pending.reject(new Error('Late failure'));
    await settlePromises();
    expect(connected).toBe(false);
    expect(hub.invokeResult).not.toHaveBeenCalled();
    expect(vi.getTimerCount()).toBe(0);
  });

  it('balances initial-list busy state after invoke rejection and keeps notifications usable', async () => {
    const pending = deferred<ToDoItemDto[]>();
    hub.invokeResult.mockReturnValueOnce(pending.promise);
    service.startConnection();
    await settlePromises();
    vi.advanceTimersByTime(250);
    expect(busy).toBe(true);
    pending.reject(new Error('GetAllItems rejected'));
    await settlePromises();
    expect(busy).toBe(false);
    expect(connected).toBe(true);
    expect(todos).toEqual([]);
    hub.emit('itemCreated', item);
    expect(todos).toEqual([item]);
  });

  it('stops pending initial load and sends, ignores late results/events and releases all timers', async () => {
    const list = deferred<ToDoItemDto[]>();
    const send = deferred<void>();
    hub.invokeResult.mockReturnValueOnce(list.promise);
    hub.send.mockReturnValueOnce(send.promise);
    service.startConnection();
    await settlePromises();
    service.create({ description: 'Pending task' });
    vi.advanceTimersByTime(250);
    expect(busy).toBe(true);
    await service.stopConnection();
    expect(busy).toBe(false);
    expect(connected).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
    list.resolve([item]);
    send.reject(new Error('Send ended after stop'));
    hub.emit('itemCreated', item);
    hub.connected();
    await settlePromises();
    expect(todos).toEqual([]);
    expect(connected).toBe(false);
  });

  it('settles stop rejection and cancels retry on service destruction', async () => {
    hub.start.mockRejectedValueOnce(new Error('Offline'));
    hub.stop.mockRejectedValueOnce(new Error('Already stopped'));
    service.startConnection();
    await settlePromises();
    service.ngOnDestroy();
    await settlePromises();
    expect(hub.stop).toHaveBeenCalledOnce();
    expect(connected).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
  });
});

describe('Default realtime connection factory', () => {
  afterEach(() => vi.restoreAllMocks());

  it('uses the configured hub URL and preserves all reconnect delay boundaries', () => {
    const url = vi.spyOn(HubConnectionBuilder.prototype, 'withUrl');
    const reconnect = vi.spyOn(HubConnectionBuilder.prototype, 'withAutomaticReconnect');
    TestBed.configureTestingModule({});
    TestBed.inject(TODO_HUB_CONNECTION_FACTORY)();
    expect(url).toHaveBeenCalledExactlyOnceWith(environment.hubUrl);
    const policy = reconnect.mock.calls[0][0] as IRetryPolicy;
    for (const [count, delay] of [[0, 10000], [5, 10000], [6, 30000], [9, 30000], [10, 60000], [30, 60000]]) {
      expect(policy.nextRetryDelayInMilliseconds({ previousRetryCount: count, elapsedMilliseconds: 0, retryReason: new Error('Offline') })).toBe(delay);
    }
  });
});
