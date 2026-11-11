import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Subscription } from 'rxjs';
import { environment } from '../../environments/environment';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { BusyService } from '../busy/busy.service';
import { TodoRestService } from './todo-rest.service';

describe('TodoRestService HTTP contracts', () => {
  let service: TodoRestService;
  let http: HttpTestingController;
  let subscriptions: Subscription[];
  const url = `${environment.apiUrl}/todoItem`;
  const item: ToDoItemDto = {
    toDoItemId: 'item-1',
    description: 'Recorded task',
    isCompleted: false,
    createdDate: new Date('2026-11-09'),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(TodoRestService);
    http = TestBed.inject(HttpTestingController);
    subscriptions = [];
  });

  afterEach(() => {
    subscriptions.forEach(subscription => subscription.unsubscribe());
    http.verify();
  });

  it('loads and refreshes the list using GET and forwards returned items', () => {
    const lists: ToDoItemDto[][] = [];
    subscriptions.push(service.todos$.subscribe(items => lists.push(items)));
    const initial = http.expectOne(url);
    expect(initial.request.method).toBe('GET');
    initial.flush([item]);
    service.refresh();
    const refresh = http.expectOne(url);
    expect(refresh.request.method).toBe('GET');
    refresh.flush([]);
    expect(lists).toEqual([[item], []]);
  });

  it.each([
    ['create', 'POST', url, { description: 'New task' }],
    ['toggle', 'POST', `${url}/toggleComplete/item-1`, null],
    ['delete', 'DELETE', `${url}/item-1`, null],
  ] as const)('sends the %s contract and reloads only after success', (action, method, mutationUrl, body) => {
    const lists: ToDoItemDto[][] = [];
    subscriptions.push(service.todos$.subscribe(items => lists.push(items)));
    http.expectOne(url).flush([]);
    const mutation = action === 'create'
      ? service.add({ description: 'New task' })
      : action === 'toggle'
        ? service.toggleComplete('item-1')
        : service.delete('item-1');
    subscriptions.push(mutation.subscribe());
    const request = http.expectOne(mutationUrl);
    expect(request.request.method).toBe(method);
    expect(request.request.body).toEqual(body);
    http.expectNone(url);
    request.flush(null);
    const reload = http.expectOne(url);
    expect(reload.request.method).toBe('GET');
    reload.flush([item]);
    expect(lists).toEqual([[], [item]]);
  });

  it.each(['create', 'toggle', 'delete'] as const)('does not refresh after a rejected %s request', action => {
    subscriptions.push(service.todos$.subscribe());
    http.expectOne(url).flush([]);
    const rejected = vi.fn();
    const mutation = action === 'create'
      ? service.add({ description: 'Failed task' })
      : action === 'toggle'
        ? service.toggleComplete('item-1')
        : service.delete('item-1');
    subscriptions.push(mutation.subscribe({ error: rejected }));
    const request = http.expectOne(action === 'create' ? url : action === 'toggle' ? `${url}/toggleComplete/item-1` : `${url}/item-1`);
    request.flush({ message: 'Unavailable' }, { status: 503, statusText: 'Service Unavailable' });
    expect(rejected).toHaveBeenCalledOnce();
    http.expectNone(url);
  });

  it('propagates list errors and releases busy state', () => {
    vi.useFakeTimers();
    try {
      let busy = false;
      subscriptions.push(TestBed.inject(BusyService).isBusy.subscribe(value => busy = value));
      const rejected = vi.fn();
      subscriptions.push(service.todos$.subscribe({ error: rejected }));
      vi.advanceTimersByTime(250);
      expect(busy).toBe(true);
      http.expectOne(url).flush(null, { status: 500, statusText: 'Server Error' });
      expect(rejected).toHaveBeenCalledOnce();
      expect(busy).toBe(false);
      expect(vi.getTimerCount()).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('cancels the previous list on refresh and cancels pending HTTP on unsubscribe', () => {
    const subscription = service.todos$.subscribe();
    const first = http.expectOne(url);
    service.refresh();
    expect(first.cancelled).toBe(true);
    const second = http.expectOne(url);
    subscription.unsubscribe();
    expect(second.cancelled).toBe(true);
  });
});
