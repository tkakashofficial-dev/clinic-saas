import {
  Component,
  ElementRef,
  HostListener,
  computed,
  forwardRef,
  inject,
  input,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption {
  value: string;
  label: string;
  sublabel?: string;
}

/**
 * Custom dropdown that follows the design system — native <select> can't be
 * styled and looks foreign to the app. Keyboard accessible: Enter/Space open,
 * arrows navigate, Escape closes.
 */
@Component({
  selector: 'app-select',
  template: `
    <div class="select" [class.open]="open()">
      <button
        type="button"
        class="select-trigger input"
        [class.placeholder]="!selected()"
        [disabled]="disabled()"
        (click)="toggle()"
        (keydown)="onKeydown($event)"
        [attr.aria-expanded]="open()"
        aria-haspopup="listbox">
        <span class="select-value">
          @if (selected(); as current) {
            {{ current.label }}
          } @else {
            {{ placeholder() }}
          }
        </span>
        <svg class="chev" width="14" height="14" viewBox="0 0 24 24" fill="none"
             stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="6 9 12 15 18 9"/>
        </svg>
      </button>

      @if (open()) {
        <div class="select-panel" role="listbox">
          @for (option of options(); track option.value; let i = $index) {
            <button
              type="button"
              role="option"
              class="select-option"
              [class.selected]="option.value === value()"
              [class.focused]="i === focusedIndex()"
              [attr.aria-selected]="option.value === value()"
              (click)="pick(option)"
              (mouseenter)="focusedIndex.set(i)">
              <span class="option-labels">
                <span class="option-label">{{ option.label }}</span>
                @if (option.sublabel) {
                  <span class="option-sublabel">{{ option.sublabel }}</span>
                }
              </span>
              @if (option.value === value()) {
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none"
                     stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round">
                  <polyline points="20 6 9 17 4 12"/>
                </svg>
              }
            </button>
          } @empty {
            <div class="select-empty">No options available</div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .select { position: relative; }

    .select-trigger {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      cursor: pointer;
      text-align: left;

      &.placeholder .select-value { color: #9CB1A9; }

      .chev { color: var(--color-text-muted); transition: transform .18s ease; flex: none; }
    }

    .open .select-trigger {
      border-color: var(--color-primary-500);
      box-shadow: 0 0 0 3px var(--color-primary-100);
      .chev { transform: rotate(180deg); }
    }

    .select-panel {
      position: absolute;
      top: calc(100% + 6px);
      left: 0; right: 0;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 12px;
      box-shadow: 0 8px 30px rgb(12 43 35 / 0.14);
      padding: 5px;
      z-index: 60;
      max-height: 260px;
      overflow-y: auto;
      animation: selectIn .14s ease;
    }

    @keyframes selectIn { from { opacity: 0; transform: translateY(-4px); } }

    .select-option {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
      width: 100%;
      border: none;
      background: transparent;
      padding: 9px 11px;
      border-radius: 8px;
      cursor: pointer;
      font: 500 14px var(--font-body);
      color: var(--color-text);
      text-align: left;

      svg { color: var(--color-primary-600); flex: none; }

      &.focused { background: var(--color-primary-50); }
      &.selected .option-label { color: var(--color-primary-700); font-weight: 600; }
    }

    .option-labels { display: flex; flex-direction: column; min-width: 0; }
    .option-sublabel { font-size: 12px; color: var(--color-text-muted); }

    .select-empty {
      padding: 14px;
      text-align: center;
      font-size: 13px;
      color: var(--color-text-muted);
    }
  `,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => Select), multi: true },
  ],
})
export class Select implements ControlValueAccessor {
  readonly options = input.required<SelectOption[]>();
  readonly placeholder = input('Select…');

  private readonly host = inject(ElementRef<HTMLElement>);

  readonly value = signal<string | null>(null);
  readonly open = signal(false);
  readonly disabled = signal(false);
  readonly focusedIndex = signal(-1);

  readonly selected = computed(
    () => this.options().find((option) => option.value === this.value()) ?? null,
  );

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  toggle(): void {
    if (this.disabled()) return;
    this.open.update((v) => !v);
    if (this.open()) {
      this.focusedIndex.set(
        Math.max(0, this.options().findIndex((option) => option.value === this.value())),
      );
    } else {
      this.onTouched();
    }
  }

  pick(option: SelectOption): void {
    this.value.set(option.value);
    this.onChange(option.value);
    this.onTouched();
    this.open.set(false);
  }

  onKeydown(event: KeyboardEvent): void {
    const options = this.options();
    switch (event.key) {
      case 'Escape':
        this.open.set(false);
        break;
      case 'ArrowDown':
        event.preventDefault();
        if (!this.open()) this.toggle();
        else this.focusedIndex.update((i) => Math.min(i + 1, options.length - 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.focusedIndex.update((i) => Math.max(i - 1, 0));
        break;
      case 'Enter':
      case ' ':
        event.preventDefault();
        if (!this.open()) this.toggle();
        else if (options[this.focusedIndex()]) this.pick(options[this.focusedIndex()]);
        break;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
      this.onTouched();
    }
  }

  writeValue(value: string | null): void {
    this.value.set(value);
  }
  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
