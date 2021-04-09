import { Component } from '@angular/core';
import { Router, NavigationEnd, ActivatedRoute, Route } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { IAppRoute } from '../routes';

@Component({
  selector: 'app-shell',
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.scss'],
})
export class ShellComponent {
  private _navItems$ = new BehaviorSubject<IAppRoute[]>([]);
  public navItems$ = this._navItems$.asObservable();

  public currentRoute$ = this._router.events.pipe(
    filter(
      (event) =>
        event instanceof NavigationEnd && !!this._route.snapshot.firstChild
    ),
    map(() => this._route.snapshot.firstChild)
  );

  constructor(private _router: Router, private _route: ActivatedRoute) {
    _router.config.forEach((r) => {
      if (!!r.children) {
        this._navItems$.next(
          <IAppRoute[]>r.children.filter((c) => !!c.data?.title)
        );
      }
    });
  }
}
