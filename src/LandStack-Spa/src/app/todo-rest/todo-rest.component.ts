import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
  ReactiveFormsModule,
} from '@angular/forms';
import { IAppRoute } from '../routes';
import { TodoRestService } from './todo-rest.service';
import { AsyncPipe } from '@angular/common';

@Component({
  selector: 'app-todo-rest',
  templateUrl: './todo-rest.component.html',
  styleUrls: ['./todo-rest.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, AsyncPipe],
})
export class TodoRestComponent {
  private readonly destroyRef = inject(DestroyRef);
  public todos$ = this.todoService.todos$;
  public addNewForm: UntypedFormGroup;

  constructor(
    private todoService: TodoRestService,
    fb: UntypedFormBuilder
  ) {
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
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.addNewForm.setValue({ description: '' }),
        // Keep the entered description available for retry after a request failure.
        error: () => undefined,
      });
  }

  public toggleComplete(todoItemId: string) {
    this.todoService
      .toggleComplete(todoItemId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: () => undefined });
  }

  public delete(todoItemId: string) {
    this.todoService
      .delete(todoItemId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: () => undefined });
  }
}
