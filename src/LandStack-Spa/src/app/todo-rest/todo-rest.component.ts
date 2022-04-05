import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { IAppRoute } from '../routes';
import { TodoRestService } from './todo-rest.service';

@Component({
  selector: 'app-todo-rest',
  templateUrl: './todo-rest.component.html',
  styleUrls: ['./todo-rest.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TodoRestComponent {
  public todos$ = this.todoService.todos$;
  public addNewForm: FormGroup;

  constructor(private todoService: TodoRestService, fb: FormBuilder) {
    this.addNewForm = fb.group({
      description: ['', Validators.required],
    });
  }

  static getRoute(): IAppRoute {
    return {
      path: '',
      component: TodoRestComponent,
      data: {
        title: 'Rest',
      },
    };
  }

  public refresh() {
    this.todoService.refresh();
  }

  public add() {
    if (this.addNewForm.invalid) return;

    this.todoService
      .add({ description: this.addNewForm.value.description })
      .subscribe({
        next: () => this.addNewForm.setValue({ description: '' }),
      });
  }

  public toggleComplete(todoItemId: string) {
    this.todoService.toggleComplete(todoItemId).subscribe();
  }

  public delete(todoItemId: string) {
    this.todoService.delete(todoItemId).subscribe();
  }
}
