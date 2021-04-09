import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { AppComponent } from './app.component';
import { HttpClientModule } from '@angular/common/http';
import { TodoRestComponent } from './todo-rest/todo-rest.component';
import { ReactiveFormsModule } from '@angular/forms';
import { BusyComponent } from './busy/busy.component';
import { TodoRealtimeComponent } from './todo-realtime/todo-realtime.component';
import { ShellComponent } from './shell/shell.component';
import { RouterModule } from '@angular/router';
import { appRoutes } from './routes';

@NgModule({
  declarations: [
    AppComponent,
    TodoRestComponent,
    BusyComponent,
    TodoRealtimeComponent,
    ShellComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    ReactiveFormsModule,
    RouterModule.forRoot(appRoutes),
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
