import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Password field with a show/hide (eye) toggle — the expected pattern
 * everywhere passwords are typed, so people can confirm what they entered
 * before submitting. Works as a form control (formControlName).
 */
@Component({
  selector: 'app-password-input',
  template: `
    <div class="pw-wrap">
      <input
        [id]="inputId()"
        [type]="visible() ? 'text' : 'password'"
        class="input"
        [class.invalid]="invalid()"
        [placeholder]="placeholder()"
        [attr.autocomplete]="autocomplete()"
        [value]="value()"
        [disabled]="disabled()"
        (input)="onInput($any($event.target).value)"
        (blur)="onTouched()">
      <button type="button" class="pw-toggle"
              (click)="visible.set(!visible())"
              [attr.aria-label]="visible() ? 'Hide password' : 'Show password'"
              [attr.aria-pressed]="visible()"
              tabindex="-1">
        @if (visible()) {
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
        } @else {
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
        }
      </button>
    </div>
  `,
  styles: `
    .pw-wrap { position: relative; }
    .pw-wrap .input { padding-right: 42px; width: 100%; }
    .pw-toggle {
      position: absolute;
      right: 6px; top: 50%;
      transform: translateY(-50%);
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 30px; height: 30px;
      border: 0;
      border-radius: 7px;
      background: none;
      color: var(--color-text-muted);
      cursor: pointer;
      transition: color .15s ease, background .15s ease;

      &:hover { color: var(--color-primary-700); background: var(--color-primary-50); }
    }
  `,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PasswordInput), multi: true },
  ],
})
export class PasswordInput implements ControlValueAccessor {
  readonly inputId = input('password');
  readonly placeholder = input('');
  readonly autocomplete = input('current-password');
  readonly invalid = input(false);

  readonly visible = signal(false);
  readonly value = signal('');
  readonly disabled = signal(false);

  private onChange: (value: string) => void = () => {};
  onTouched: () => void = () => {};

  onInput(value: string): void {
    this.value.set(value);
    this.onChange(value);
  }

  writeValue(value: string | null): void {
    this.value.set(value ?? '');
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
