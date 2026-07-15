import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Segmented control — the right pattern when there are 2–4 options:
 * all choices visible at once, one tap to pick, no dropdown needed.
 */
@Component({
  selector: 'app-segmented',
  template: `
    <div class="segmented" role="radiogroup">
      @for (option of options(); track option) {
        <button
          type="button"
          role="radio"
          [attr.aria-checked]="value() === option"
          class="segment"
          [class.active]="value() === option"
          [disabled]="disabled()"
          (click)="pick(option)">
          @if (value() === option) {
            <svg class="check" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
          }
          {{ option }}
        </button>
      }
    </div>
  `,
  styles: `
    .segmented {
      display: flex;
      background: var(--color-bg);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-input);
      padding: 3px;
      gap: 3px;
    }
    .segment {
      flex: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      border: none;
      background: transparent;
      font: 500 13.5px var(--font-body);
      color: var(--color-text-muted);
      padding: 8px 10px;
      border-radius: 8px;
      cursor: pointer;
      transition: all .15s ease;

      &:hover:not(.active):not(:disabled) { color: var(--color-text); background: rgb(12 43 35 / .04); }

      /* Selected state must be UNMISSABLE — white-on-white was invisible
         inside drawers, and a black fill felt heavy in light forms. Brand
         teal + white text + check: obvious, accessible, on-palette. */
      &.active {
        background: var(--color-primary-600);
        color: #fff;
        font-weight: 600;
        box-shadow: 0 1px 4px rgb(0 132 101 / .35);
      }

      .check { flex: none; color: #fff; }

      &:disabled { opacity: .5; cursor: not-allowed; }
    }
  `,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => Segmented), multi: true },
  ],
})
export class Segmented implements ControlValueAccessor {
  readonly options = input.required<string[]>();

  readonly value = signal<string | null>(null);
  readonly disabled = signal(false);

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  pick(option: string): void {
    this.value.set(option);
    this.onChange(option);
    this.onTouched();
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
