import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { TodoRestComponent } from './todo-rest.component';
import { TodoRestService } from './todo-rest.service';

describe('TodoRestComponent interactions', () => {
  let fixture: ComponentFixture<TodoRestComponent>;
  let root: HTMLElement;
  let items: BehaviorSubject<ToDoItemDto[]>;
  let create: Subject<unknown>;
  let toggle: Subject<unknown>;
  let remove: Subject<unknown>;
  let service: {
    todos$: BehaviorSubject<ToDoItemDto[]>;
    add: ReturnType<typeof vi.fn>;
    toggleComplete: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
    refresh: ReturnType<typeof vi.fn>;
  };
  const item: ToDoItemDto = {
    toDoItemId: 'item-1',
    description: 'Visible task',
    isCompleted: true,
    createdDate: new Date('2026-11-09'),
  };

  beforeEach(async () => {
    items = new BehaviorSubject([item]);
    create = new Subject();
    toggle = new Subject();
    remove = new Subject();
    service = {
      todos$: items,
      add: vi.fn(() => create),
      toggleComplete: vi.fn(() => toggle),
      delete: vi.fn(() => remove),
      refresh: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [TodoRestComponent],
      providers: [provideZonelessChangeDetection(), { provide: TodoRestService, useValue: service }],
    }).compileComponents();
    fixture = TestBed.createComponent(TodoRestComponent);
    root = fixture.nativeElement;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => fixture.destroy());

  function enterDescription(value: string) {
    const input = root.querySelector<HTMLInputElement>('input[formControlName="description"]');
    if (!input) throw new Error('Description input missing');
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  function submit() {
    root.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  }

  it('renders descriptions/completion and updates when the list changes', async () => {
    expect(root.querySelector('li span')?.textContent).toBe('Visible task');
    expect(root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(true);
    items.next([{ ...item, description: 'Changed task', isCompleted: false }]);
    await fixture.whenStable();
    expect(root.querySelector('li span')?.textContent).toBe('Changed task');
    expect(root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(false);
    items.next([]);
    await fixture.whenStable();
    expect(root.querySelectorAll('li')).toHaveLength(0);
  });

  it('blocks an empty description, submits a valid description and clears it only on success', async () => {
    submit();
    expect(service.add).not.toHaveBeenCalled();
    enterDescription('New task');
    submit();
    expect(service.add).toHaveBeenCalledExactlyOnceWith({ description: 'New task' });
    expect(fixture.componentInstance.addNewForm.value.description).toBe('New task');
    create.next(null);
    create.complete();
    await fixture.whenStable();
    expect(root.querySelector<HTMLInputElement>('input[formControlName="description"]')?.value).toBe('');
    expect(fixture.componentInstance.addNewForm.invalid).toBe(true);
  });

  it('keeps a rejected description available for retry without an unhandled error', () => {
    enterDescription('Retry task');
    submit();
    create.error(new Error('Request rejected'));
    expect(fixture.componentInstance.addNewForm.value.description).toBe('Retry task');
    expect(root.querySelector<HTMLInputElement>('input[formControlName="description"]')?.value).toBe('Retry task');
  });

  it('delegates refresh, checkbox changes and delete button clicks with the item ID', () => {
    root.querySelector<HTMLButtonElement>('h1 button')?.click();
    root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.dispatchEvent(new Event('change', { bubbles: true }));
    root.querySelector<HTMLButtonElement>('li button')?.click();
    expect(service.refresh).toHaveBeenCalledOnce();
    expect(service.toggleComplete).toHaveBeenCalledExactlyOnceWith('item-1');
    expect(service.delete).toHaveBeenCalledExactlyOnceWith('item-1');
    toggle.error(new Error('Toggle rejected'));
    remove.error(new Error('Delete rejected'));
  });

  it('unsubscribes the rendered list and every pending mutation on destroy', () => {
    enterDescription('Pending task');
    submit();
    root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.dispatchEvent(new Event('change', { bubbles: true }));
    root.querySelector<HTMLButtonElement>('li button')?.click();
    expect(items.observed).toBe(true);
    expect(create.observed).toBe(true);
    expect(toggle.observed).toBe(true);
    expect(remove.observed).toBe(true);
    fixture.destroy();
    // No consumer remains on either the component's AsyncPipe or mutation streams.
    expect(items.observed).toBe(false);
    expect(create.observed).toBe(false);
    expect(toggle.observed).toBe(false);
    expect(remove.observed).toBe(false);
  });
});
