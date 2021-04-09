import { Routes, Route } from '@angular/router';
import { ShellComponent } from './shell/shell.component';
import { TodoRestComponent } from './todo-rest/todo-rest.component';
import { TodoRealtimeComponent } from './todo-realtime/todo-realtime.component';

export interface IRouteData {
  title: string;
}

export interface IAppRoute extends Route {
  data: IRouteData;
}

export const appRoutes: Routes = [
  {
    path: '',
    component: ShellComponent,
    runGuardsAndResolvers: 'always',
    children: [TodoRestComponent.getRoute(), TodoRealtimeComponent.getRoute()],
  },
  {
    path: '**',
    redirectTo: '',
    pathMatch: 'full',
  },
];
