import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { Router } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../environments/environment';
import { AppComponent } from './app.component';
import { appConfig } from './app.config';

describe('Standalone application', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        ...appConfig.providers,
        // Vitest uses zoneless testing; application runtime keeps Zone.js.
        provideZonelessChangeDetection(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('renders the shell and initial REST list using the application providers', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/');
    fixture.detectChanges();

    TestBed.inject(HttpTestingController)
      .expectOne(`${environment.apiUrl}/todoItem`)
      .flush([
        {
          toDoItemId: 'initial-item',
          description: 'Loaded through the standalone application',
          isCompleted: false,
          createdDate: '2026-11-09T12:00:00Z',
        },
      ]);
    await fixture.whenStable();
    fixture.detectChanges();

    const root: HTMLElement = fixture.nativeElement;
    expect(root.querySelector('app-todo-rest')?.textContent).toContain(
      'Loaded through the standalone application'
    );
    expect(
      Array.from(root.querySelectorAll('app-shell a'), (a) =>
        a.textContent?.trim()
      )
    ).toEqual(['Rest', 'Realtime']);
    expect(root.querySelector<HTMLElement>('app-busy')?.style.display).toBe(
      'none'
    );
  });
});
