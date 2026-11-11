import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { EMPTY, Observable, of, Subject, throwError } from 'rxjs';
import { BusyService } from './busy.service';

describe('BusyService', () => {
  let service: BusyService;
  let visible: boolean;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.runOnlyPendingTimers();
    service = new BusyService();
    visible = false;
    service.isBusy.subscribe(value => visible = value);
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    expect(vi.getTimerCount()).toBe(0);
    vi.useRealTimers();
    document.body.replaceChildren();
  });

  it('waits 250ms before showing busy state and restores the focused input', () => {
    const input = document.createElement('input');
    document.body.append(input);
    input.focus();
    service.busy();
    vi.advanceTimersByTime(249);
    expect(visible).toBe(false);
    expect(document.activeElement).toBe(input);
    vi.advanceTimersByTime(1);
    expect(visible).toBe(true);
    expect(document.activeElement).not.toBe(input);
    service.idle();
    expect(visible).toBe(false);
    expect(document.activeElement).toBe(input);
  });

  it('does not flash for fast work or reuse its timer for a later operation', () => {
    service.busy();
    vi.advanceTimersByTime(200);
    service.idle();
    service.busy();
    vi.advanceTimersByTime(50);
    expect(visible).toBe(false);
    vi.advanceTimersByTime(199);
    expect(visible).toBe(false);
    vi.advanceTimersByTime(1);
    expect(visible).toBe(true);
    service.idle();
  });

  it('preserves the original focus across overlapping delayed operations', () => {
    const input = document.createElement('input');
    document.body.append(input);
    input.focus();
    service.busy();
    vi.advanceTimersByTime(100);
    service.busy();
    vi.advanceTimersByTime(250);
    expect(visible).toBe(true);
    service.idle();
    expect(visible).toBe(true);
    expect(document.activeElement).not.toBe(input);
    service.idle();
    expect(visible).toBe(false);
    expect(document.activeElement).toBe(input);
  });

  it('counts subscriptions, not observable construction or intermediate emissions', () => {
    const source = new Subject<number>();
    const wrapped = service.busyStream(source);
    vi.advanceTimersByTime(300);
    expect(visible).toBe(false);
    const values: number[] = [];
    const first = wrapped.subscribe(value => values.push(value));
    const second = wrapped.subscribe();
    vi.advanceTimersByTime(250);
    source.next(1);
    source.next(2);
    expect(values).toEqual([1, 2]);
    expect(visible).toBe(true);
    first.unsubscribe();
    expect(visible).toBe(true);
    second.unsubscribe();
    expect(visible).toBe(false);
  });

  it('releases work on error and keeps other operations busy', () => {
    const first = new Subject<number>();
    const second = new Subject<number>();
    const rejected = vi.fn();
    service.busyStream(first).subscribe({ error: rejected });
    service.busyStream(second).subscribe();
    vi.advanceTimersByTime(250);
    const failure = new Error('failed request');
    first.error(failure);
    expect(rejected).toHaveBeenCalledWith(failure);
    expect(visible).toBe(true);
    second.complete();
    expect(visible).toBe(false);
  });

  it('cleans up an empty completion and a cancelled source before the delay', () => {
    service.busyStream(EMPTY).subscribe();
    const teardown = vi.fn();
    const subscription = service.busyStream(new Observable(() => teardown)).subscribe();
    subscription.unsubscribe();
    expect(teardown).toHaveBeenCalledOnce();
    expect(vi.getTimerCount()).toBe(0);
    vi.advanceTimersByTime(300);
    expect(visible).toBe(false);
  });

  it('does not leave work pending after synchronous values or errors', () => {
    const values: number[] = [];
    service.busyStream(of(42)).subscribe(value => values.push(value));
    const failure = new Error('synchronous failure');
    const rejected = vi.fn();
    service.busyStream(throwError(() => failure)).subscribe({ error: rejected });
    expect(values).toEqual([42]);
    expect(rejected).toHaveBeenCalledWith(failure);
    expect(visible).toBe(false);
  });

  it('holds combined streams busy until completion and forwards their latest values', () => {
    const first = new Subject<number>();
    const second = new Subject<number>();
    const values: number[][] = [];
    service.busyCombine([first, second]).subscribe(value => values.push(value));
    vi.advanceTimersByTime(250);
    first.next(1);
    second.next(2);
    first.next(3);
    expect(values).toEqual([[1, 2], [3, 2]]);
    expect(visible).toBe(true);
    first.complete();
    expect(visible).toBe(true);
    second.complete();
    expect(visible).toBe(false);
  });

  it('releases combined work when a source errors and tears down the others', () => {
    const pending = new Subject<number>();
    const teardown = vi.fn();
    const failure = new Error('combined failure');
    const rejected = vi.fn();
    const source = new Observable<number>(() => teardown);
    service.busyCombine([source, pending]).subscribe({ error: rejected });
    vi.advanceTimersByTime(250);
    pending.error(failure);
    expect(rejected).toHaveBeenCalledWith(failure);
    expect(teardown).toHaveBeenCalledOnce();
    expect(visible).toBe(false);
  });

  it('ignores unmatched idle calls without hiding subsequent work early', () => {
    service.idle();
    service.busy();
    vi.advanceTimersByTime(250);
    expect(visible).toBe(true);
    service.idle();
    service.idle();
    expect(visible).toBe(false);
  });
});
