import { Component, HostBinding, OnDestroy } from '@angular/core';
import { BusyService } from './busy.service';

@Component({
  selector: 'app-busy',
  templateUrl: './busy.component.html',
  styleUrls: ['./busy.component.scss'],
})
export class BusyComponent implements OnDestroy {
  @HostBinding('style.display') display: string | null = 'none';

  private _subscription = this.busy.isBusy.subscribe((isBusy) => {
    if (isBusy) this.show();
    else this.hide();
  });

  constructor(private busy: BusyService) {}

  private hide() {
    this.display = 'none';
  }

  private show() {
    this.display = null;
  }

  ngOnDestroy(): void {
    this._subscription?.unsubscribe();
  }
}
