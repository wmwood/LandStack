import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { Subject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { BusyComponent } from './busy.component';
import { BusyService } from './busy.service';

describe('BusyComponent', () => {
  const state = new Subject<boolean>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BusyComponent],
      providers: [provideZonelessChangeDetection(), { provide: BusyService, useValue: { isBusy: state } }],
    });
  });

  it('shows and hides the overlay and unsubscribes when destroyed', () => {
    const fixture = TestBed.createComponent(BusyComponent);
    fixture.detectChanges();
    const host: HTMLElement = fixture.nativeElement;
    expect(host.style.display).toBe('none');
    state.next(true);
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(host.style.display).toBe('');
    state.next(false);
    fixture.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(host.style.display).toBe('none');
    fixture.destroy();
    state.next(true);
    expect(fixture.componentInstance.display).toBe('none');
  });
});
