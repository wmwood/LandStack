import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { asyncScheduler, BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';
import { BusyService } from '../busy/busy.service';
import { CreateTodoItemDto } from '../_models/createTodoItemDto';
import { ToDoItemDto } from '../_models/toDoItemDto';

@Injectable({
  providedIn: 'root',
})
export class TodoRealtimeService {
  private hubConnection: signalR.HubConnection = new signalR.HubConnectionBuilder()
    .withUrl(environment.hubUrl)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.previousRetryCount < 6) return 10000;
        if (retryContext.previousRetryCount < 10) return 30000;
        return 60000;
      },
    })
    .build();

  private _isConnected$ = new BehaviorSubject(false);
  public isConnected$ = this._isConnected$.asObservable();

  private _todos$ = new BehaviorSubject<ToDoItemDto[]>([]);
  public todos$ = this._todos$.asObservable();

  constructor(private busy: BusyService) {
    this.hubConnection.onclose(() => this._isConnected$.next(false));
    this.hubConnection.onreconnecting(() => this._isConnected$.next(false));
    this.hubConnection.onreconnected(() => this._isConnected$.next(true));

    this.hubConnection.on('LoadTodoItems', (todos: ToDoItemDto[]) => {
      this._todos$.next(todos);
    });

    this.hubConnection.on('AddTodoItem', (todoItem: ToDoItemDto) => {
      this._todos$.next([...this._todos$.value, todoItem]);
    });

    this.hubConnection.on('DeleteTodoItem', (todoItemId: string) => {
      this._todos$.next(
        (<ToDoItemDto[]>this._todos$.value).filter(
          (item) => item.toDoItemId != todoItemId
        )
      );
    });

    this.hubConnection.on('UpdateTodoItem', (updatedItem: ToDoItemDto) => {
      this._todos$.next(
        (<ToDoItemDto[]>this._todos$.value).map((todoItem) =>
          todoItem.toDoItemId == updatedItem.toDoItemId ? updatedItem : todoItem
        )
      );
    });
  }

  public add(dto: CreateTodoItemDto) {
    this.busy.busy();
    this.hubConnection.send('Add', dto).finally(() => this.busy.idle());
  }

  public update(dto: ToDoItemDto) {
    this.busy.busy();
    this.hubConnection.send('Update', dto).finally(() => this.busy.idle());
  }

  public delete(todoItemId: string) {
    this.busy.busy();
    this.hubConnection
      .send('Delete', todoItemId)
      .finally(() => this.busy.idle());
  }

  public startConnection(): void {
    this.hubConnection
      .start()
      .then(() => this._isConnected$.next(true))
      .catch(() =>
        asyncScheduler.schedule(() => this.startConnection(), 10000)
      );
  }

  public stopConnection() {
    this.hubConnection.stop();
  }
}
