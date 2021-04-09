import { Component, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IAppRoute } from '../routes';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { TodoRealtimeService } from './todo-realtime.service';

@Component({
  selector: 'app-todo-realtime',
  templateUrl: './todo-realtime.component.html',
  styleUrls: ['./todo-realtime.component.scss'],
})
export class TodoRealtimeComponent implements OnDestroy {
  public todos$ = this.todoService.todos$;
  public addNewForm: FormGroup;

  constructor(private todoService: TodoRealtimeService, fb: FormBuilder) {
    this.addNewForm = fb.group({
      description: ['', Validators.required],
    });
    todoService.startConnection();
  }

  static getRoute(): IAppRoute {
    return {
      path: 'realtime',
      component: TodoRealtimeComponent,
      data: {
        title: 'Realtime',
      },
    };
  }

  public add() {
    if (this.addNewForm.invalid) return;

    this.todoService.add({ description: this.addNewForm.value.description });
    this.addNewForm.setValue({ description: '' });
  }

  public toggle(todoItem: ToDoItemDto) {
    todoItem.isCompleted = !todoItem.isCompleted;
    this.todoService.update(todoItem);
  }

  public delete(todoItemId: string) {
    this.todoService.delete(todoItemId);
  }

  ngOnDestroy(): void {
    this.todoService.stopConnection();
  }
}
