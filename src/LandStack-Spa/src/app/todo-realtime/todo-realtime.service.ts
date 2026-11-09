import { inject, Injectable, OnDestroy } from '@angular/core';
import { asyncScheduler, BehaviorSubject, defer, Subscription } from 'rxjs';
import { BusyService } from '../busy/busy.service';
import { CreateTodoItemDto } from '../_models/createTodoItemDto';
import { NotificationAction } from '../_models/notificationAction';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { TODO_HUB_CONNECTION_FACTORY } from './todo-hub-connection';

@Injectable({
  providedIn: 'root',
})
export class TodoRealtimeService implements OnDestroy {
  private hubConnection = inject(TODO_HUB_CONNECTION_FACTORY)();
  private retry: Subscription | null = null;
  private initialLoad: Subscription | null = null;
  private pendingCommands = new Set<Subscription>();
  private running = false;
  private generation = 0;

  private _isConnected$ = new BehaviorSubject(false);
  public isConnected$ = this._isConnected$.asObservable();

  private _todos$ = new BehaviorSubject<ToDoItemDto[]>([]);
  public todos$ = this._todos$.asObservable();

  constructor(private busy: BusyService) {
    this.hubConnection.onclose(() => this._isConnected$.next(false));
    this.hubConnection.onreconnecting(() => this._isConnected$.next(false));
    this.hubConnection.onreconnected(() => {
      if (this.running) this._isConnected$.next(true);
    });

    this.hubConnection.on(
      NotificationAction[NotificationAction.itemCreated],
      (todoItem: ToDoItemDto) => {
        if (this.running) this._todos$.next([...this._todos$.value, todoItem]);
      }
    );
    this.hubConnection.on(
      NotificationAction[NotificationAction.itemDeleted],
      (todoItemId: string) => {
        if (this.running) {
          this._todos$.next(this._todos$.value.filter(item => item.toDoItemId !== todoItemId));
        }
      }
    );
    this.hubConnection.on(
      NotificationAction[NotificationAction.itemUpdated],
      (updatedItem: ToDoItemDto) => {
        if (this.running) {
          this._todos$.next(this._todos$.value.map(todoItem =>
            todoItem.toDoItemId === updatedItem.toDoItemId ? updatedItem : todoItem
          ));
        }
      }
    );
  }

  public create(dto: CreateTodoItemDto) {
    return this.send('Create', dto);
  }

  public toggleComplete(todoItemId: string) {
    return this.send('ToggleComplete', todoItemId);
  }

  public delete(todoItemId: string) {
    return this.send('Delete', todoItemId);
  }

  private send(method: string, value: CreateTodoItemDto | string): void {
    const subscription = this.busy.busyStream(
      defer(() => this.hubConnection.send(method, value))
    ).subscribe({
      // Commands have no error UI; rejected sends still release the busy state.
      error: () => undefined,
    });
    this.pendingCommands.add(subscription);
    subscription.add(() => this.pendingCommands.delete(subscription));
  }

  public startConnection(): void {
    if (this.running) return;
    this.running = true;
    void this.connect(++this.generation);
  }

  private async connect(generation: number): Promise<void> {
    try {
      await this.hubConnection.start();
    } catch {
      if (this.isCurrent(generation)) {
        this.retry = asyncScheduler.schedule(() => {
          this.retry = null;
          void this.connect(generation);
        }, 10000);
      }
      return;
    }
    if (!this.isCurrent(generation)) return;
    this._isConnected$.next(true);
    this.initialLoad = this.busy.busyStream(
      defer(() => this.hubConnection.invoke<ToDoItemDto[]>('GetAllItems'))
    ).subscribe({
      next: todos => {
        if (this.isCurrent(generation)) this._todos$.next(todos);
      },
      // Keep the connection available for subsequent notifications.
      error: () => undefined,
    });
  }

  private isCurrent(generation: number) {
    return this.running && generation === this.generation;
  }

  public async stopConnection(): Promise<void> {
    this.running = false;
    this.generation++;
    this.retry?.unsubscribe();
    this.retry = null;
    this.initialLoad?.unsubscribe();
    this.initialLoad = null;
    this.pendingCommands.forEach(subscription => subscription.unsubscribe());
    this._isConnected$.next(false);
    try {
      await this.hubConnection.stop();
    } catch {
      // Teardown must settle even when the transport has already failed.
    }
  }

  ngOnDestroy(): void {
    void this.stopConnection();
  }
}
