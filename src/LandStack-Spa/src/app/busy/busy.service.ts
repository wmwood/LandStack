import { Injectable } from '@angular/core';
import {
  asyncScheduler,
  BehaviorSubject,
  combineLatest,
  defer,
  from,
  Observable,
  ObservableInput,
  Subscription,
} from 'rxjs';
import { finalize } from 'rxjs/operators';

@Injectable({
  providedIn: 'root',
})
export class BusyService {
  private _isBusy = new BehaviorSubject<boolean>(false);
  public isBusy = this._isBusy.asObservable();
  private _thingsBusy = 0;
  private _lastFocus: HTMLElement | null = null;
  private _delay: Subscription | null = null;

  public busyCombine<T>(sources: ObservableInput<T>[]): Observable<T[]> {
    return defer(() => {
      this.busy();
      return combineLatest(sources).pipe(finalize(() => this.idle()));
    });
  }

  public busyStream<T>(stream: ObservableInput<T>): Observable<T> {
    return defer(() => {
      this.busy();
      return from(stream).pipe(finalize(() => this.idle()));
    });
  }

  public busy() {
    this._thingsBusy++;
    if (this._thingsBusy === 1) {
      this._delay = asyncScheduler.schedule(() => {
        this._delay = null;
        this.saveCurrentFocus();
        this._isBusy.next(true);
      }, 250);
    }
  }

  public idle() {
    if (this._thingsBusy === 0) return;
    this._thingsBusy--;

    if (this._thingsBusy === 0) {
      this._delay?.unsubscribe();
      this._delay = null;
      this._isBusy.next(false);
      this.restoreFocus();
    }
  }

  private saveCurrentFocus() {
    if (document.activeElement instanceof HTMLElement) {
      this._lastFocus = document.activeElement;
      this._lastFocus.blur();
    } else {
      this._lastFocus = null;
    }
  }

  private restoreFocus() {
    this._lastFocus?.focus();
    this._lastFocus = null;
  }
}
