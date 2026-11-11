import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ToDoItemDto } from '../_models/toDoItemDto';
import { TodoRealtimeComponent } from './todo-realtime.component';
import { TodoRealtimeService } from './todo-realtime.service';

describe('TodoRealtimeComponent interactions', () => {
  let fixture: ComponentFixture<TodoRealtimeComponent>;
  let root: HTMLElement;
  let items: BehaviorSubject<ToDoItemDto[]>;
  let service: {
    todos$: BehaviorSubject<ToDoItemDto[]>;
    create: ReturnType<typeof vi.fn>;
    toggleComplete: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
    startConnection: ReturnType<typeof vi.fn>;
    stopConnection: ReturnType<typeof vi.fn>;
  };
  const item: ToDoItemDto = {
    toDoItemId: 'item-1', description: 'Realtime item', isCompleted: false,
    createdDate: new Date('2026-11-09'),
  };

  beforeEach(async () => {
    items = new BehaviorSubject([item]);
    service = {
      todos$: items, create: vi.fn(), toggleComplete: vi.fn(), delete: vi.fn(),
      startConnection: vi.fn(), stopConnection: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [TodoRealtimeComponent],
      providers: [provideZonelessChangeDetection(), { provide: TodoRealtimeService, useValue: service }],
    }).compileComponents();
    fixture = TestBed.createComponent(TodoRealtimeComponent);
    root = fixture.nativeElement;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  afterEach(() => fixture.destroy());

  it('starts on creation, renders updates and stops/unsubscribes on destruction', async () => {
    expect(service.startConnection).toHaveBeenCalledOnce();
    expect(root.querySelector('li span')?.textContent).toBe('Realtime item');
    expect(root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(false);
    items.next([{ ...item, description: 'Updated item', isCompleted: true }]);
    await fixture.whenStable();
    expect(root.querySelector('li span')?.textContent).toBe('Updated item');
    expect(root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.checked).toBe(true);
    items.next([]);
    await fixture.whenStable();
    expect(root.querySelectorAll('li')).toHaveLength(0);
    expect(items.observed).toBe(true);
    fixture.destroy();
    expect(service.stopConnection).toHaveBeenCalledOnce();
    expect(items.observed).toBe(false);
  });

  it('blocks empty submission, creates valid input and resets the form', async () => {
    const form = root.querySelector('form');
    form?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(service.create).not.toHaveBeenCalled();
    const input = root.querySelector<HTMLInputElement>('input[formControlName="description"]');
    if (!input) throw new Error('Description input missing');
    input.value = 'Created from the form';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    form?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    expect(service.create).toHaveBeenCalledExactlyOnceWith({ description: 'Created from the form' });
    await fixture.whenStable();
    expect(input.value).toBe('');
    expect(fixture.componentInstance.addNewForm.invalid).toBe(true);
  });

  it('delegates checkbox and delete events with the correct item ID', () => {
    root.querySelector<HTMLInputElement>('input[type="checkbox"]')?.dispatchEvent(new Event('change', { bubbles: true }));
    root.querySelector<HTMLButtonElement>('li button')?.click();
    expect(service.toggleComplete).toHaveBeenCalledExactlyOnceWith('item-1');
    expect(service.delete).toHaveBeenCalledExactlyOnceWith('item-1');
  });
});
