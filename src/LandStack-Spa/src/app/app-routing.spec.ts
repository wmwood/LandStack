import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import { ToDoItemDto } from './_models/toDoItemDto';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { appRoutes } from './routes';
import { TodoRealtimeService } from './todo-realtime/todo-realtime.service';
import { TodoRestService } from './todo-rest/todo-rest.service';

describe('Application routes and shell navigation', () => {
  let realtime: { todos$: Observable<ToDoItemDto[]>; startConnection: ReturnType<typeof vi.fn>; stopConnection: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    realtime = { todos$: of([]), startConnection: vi.fn(), stopConnection: vi.fn() };
    TestBed.configureTestingModule({ providers: [
      provideZonelessChangeDetection(), provideRouter(appRoutes),
      { provide: TodoRestService, useValue: { todos$: of([]) } },
      { provide: TodoRealtimeService, useValue: realtime },
    ] });
  });

  function title() {
    return TestBed.inject(Router).routerState.snapshot.root.firstChild?.firstChild?.data['title'];
  }

  it('renders the REST route and titled shell links for both views', async () => {
    const harness = await RouterTestingHarness.create('/');
    expect(harness.routeNativeElement?.querySelector('app-todo-rest')).not.toBeNull();
    expect(title()).toBe('Rest');
    const links = Array.from(harness.routeNativeElement?.querySelectorAll('a') ?? []);
    expect(links.map(link => link.textContent?.trim())).toEqual(['Rest', 'Realtime']);
    expect(links.map(link => link.getAttribute('href'))).toEqual(['/', '/realtime']);
    expect(links[0].parentElement?.classList.contains('is-active')).toBe(true);
    expect(realtime.startConnection).not.toHaveBeenCalled();
  });

  it('follows shell links to realtime and back, preserving titles/active tabs and stopping the connection', async () => {
    const harness = await RouterTestingHarness.create('/');
    const root = harness.routeNativeElement;
    root?.querySelector<HTMLAnchorElement>('a[href="/realtime"]')?.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/realtime');
    expect(root?.querySelector('app-todo-realtime')).not.toBeNull();
    expect(title()).toBe('Realtime');
    expect(root?.querySelector('a[href="/realtime"]')?.parentElement?.classList.contains('is-active')).toBe(true);
    expect(realtime.startConnection).toHaveBeenCalledOnce();
    root?.querySelector<HTMLAnchorElement>('a[href="/"]')?.click();
    await harness.fixture.whenStable();
    harness.detectChanges();
    expect(TestBed.inject(Router).url).toBe('/');
    expect(root?.querySelector('app-todo-rest')).not.toBeNull();
    expect(title()).toBe('Rest');
    expect(realtime.stopConnection).toHaveBeenCalledOnce();
  });

  it('supports direct realtime navigation and redirects unknown deep links to REST', async () => {
    const harness = await RouterTestingHarness.create('/realtime');
    expect(harness.routeNativeElement?.querySelector('app-todo-realtime')).not.toBeNull();
    expect(title()).toBe('Realtime');
    await harness.navigateByUrl('/unknown/deep/link');
    expect(TestBed.inject(Router).url).toBe('/');
    expect(harness.routeNativeElement?.querySelector('app-todo-rest')).not.toBeNull();
    expect(title()).toBe('Rest');
    expect(realtime.stopConnection).toHaveBeenCalledOnce();
  });
});
