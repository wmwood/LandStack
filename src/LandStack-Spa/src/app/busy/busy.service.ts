import { Injectable } from '@angular/core';
import {
  asyncScheduler,
  BehaviorSubject,
  combineLatest,
  Observable,
  ObservableInput,
  ObservedValueOf,
  throwError,
} from 'rxjs';
import { catchError, switchMap, tap } from 'rxjs/operators';

@Injectable({
  providedIn: 'root',
})
export class BusyService {
  private _isBusy = new BehaviorSubject<boolean>(false);
  public isBusy = this._isBusy.asObservable();
  private _thingsBusy = 0;
  private _lastFocus: HTMLElement | null = null;

  public busyCombine<O extends ObservableInput<any>>(
    sources: O[]
  ): Observable<ObservedValueOf<O>[]> {
    this.busy();
    return combineLatest(sources).pipe(
      tap(() => this.idle()),
      catchError((err) => {
        this.idle();
        return throwError(err);
      })
    );
  }

  public busyStream<O extends ObservableInput<any>>(
    stream: O
  ): Observable<ObservedValueOf<O>> {
    return this.busyCombine([stream]).pipe(switchMap((v) => v));
  }

  public busy() {
    if (!this._isBusy.value)
      asyncScheduler.schedule(() => {
        if (this._thingsBusy > 0) {
          this.saveCurrentFocus();
          this._isBusy.next(true);
        }
      }, 250);
    else this._isBusy.next(true);
    this._thingsBusy++;
  }

  public idle() {
    this._thingsBusy--;

    if (this._thingsBusy <= 0) {
      this._thingsBusy = 0;
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
    if (this._lastFocus) this._lastFocus.focus();
  }
}
