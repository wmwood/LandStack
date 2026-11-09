import { Component, ChangeDetectionStrategy } from '@angular/core';
import { BusyComponent } from './busy/busy.component';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [BusyComponent, RouterOutlet],
})
export class AppComponent {}
