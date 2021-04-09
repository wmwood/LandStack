import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { environment } from 'src/environments/environment';
import { switchMap, tap } from 'rxjs/operators';
import { CreateTodoItemDto } from '../_models/createTodoItemDto';
import { BusyService } from '../busy/busy.service';

@Injectable({
  providedIn: 'root',
})
export class TodoRestService {
  private _todosChangedAction = new BehaviorSubject<boolean>(true);
  private _todosChanged = this._todosChangedAction.asObservable();
  private _baseUrl = `${environment.apiUrl}/todoItem`;

  public todos$ = this._todosChanged.pipe(
    switchMap(() =>
      this.busy.busyStream(this.http.get<ToDoItemDto[]>(this._baseUrl))
    )
  );

  constructor(private http: HttpClient, private busy: BusyService) {}

  public refresh() {
    this._todosChangedAction.next(true);
  }

  public add(dto: CreateTodoItemDto) {
    return this.busy.busyStream(
      this.http.post(this._baseUrl, dto).pipe(tap(() => this.refresh()))
    );
  }

  public update(dto: ToDoItemDto) {
    return this.busy.busyStream(
      this.http.put(this._baseUrl, dto).pipe(tap(() => this.refresh()))
    );
  }

  public delete(todoItemId: string) {
    return this.busy.busyStream(
      this.http
        .delete(`${this._baseUrl}/${todoItemId}`)
        .pipe(tap(() => this.refresh()))
    );
  }
}
