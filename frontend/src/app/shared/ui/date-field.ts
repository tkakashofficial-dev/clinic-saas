import { Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Date-of-birth style input: the user TYPES the date (DD/MM/YYYY) with
 * automatic slash insertion — far faster than paging a calendar back
 * 30 years. Emits ISO (yyyy-MM-dd) or null; shows inline validity.
 */
@Component({
  selector: 'app-date-field',
  template: `
    <div class="date-field">
      <input
        type="text"
        class="input"
        inputmode="numeric"
        [placeholder]="placeholder()"
        [value]="display()"
        [disabled]="disabled()"
        [class.invalid]="invalid()"
        (input)="onInput($any($event.target))"
        (blur)="onBlur()"
        maxlength="10">
      <svg class="cal-icon" width="15" height="15" viewBox="0 0 24 24" fill="none"
           stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <rect x="3" y="4" width="18" height="18" rx="2"/>
        <line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/>
        <line x1="3" y1="10" x2="21" y2="10"/>
      </svg>
    </div>
    @if (invalid()) {
      <span class="field-error">{{ errorText() }}</span>
    }
  `,
  styles: `
    .date-field {
      position: relative;

      .input { padding-right: 38px; }

      .cal-icon {
        position: absolute;
        right: 12px; top: 50%;
        transform: translateY(-50%);
        color: var(--color-text-muted);
        pointer-events: none;
      }
    }
    .field-error { margin-top: 6px; display: inline-flex; }
  `,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => DateField), multi: true },
  ],
})
export class DateField implements ControlValueAccessor {
  readonly placeholder = input('DD/MM/YYYY');
  /** Reject future dates (for birth dates). */
  readonly maxToday = input(false);

  readonly display = signal('');
  readonly disabled = signal(false);
  readonly invalid = signal(false);
  readonly errorText = signal('');

  private onChange: (value: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  onInput(inputElement: HTMLInputElement): void {
    // Keep digits only, re-insert slashes at fixed positions
    const digits = inputElement.value.replace(/\D/g, '').slice(0, 8);
    let formatted = digits;
    if (digits.length > 4) formatted = `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
    else if (digits.length > 2) formatted = `${digits.slice(0, 2)}/${digits.slice(2)}`;

    this.display.set(formatted);
    inputElement.value = formatted;
    this.validateAndEmit(digits, false);
  }

  onBlur(): void {
    this.validateAndEmit(this.display().replace(/\D/g, ''), true);
    this.onTouched();
  }

  private validateAndEmit(digits: string, strict: boolean): void {
    this.invalid.set(false);

    if (digits.length === 0) {
      this.onChange(null);
      return;
    }

    if (digits.length < 8) {
      // Incomplete: only complain once the user leaves the field
      if (strict) {
        this.invalid.set(true);
        this.errorText.set('Enter a full date: DD/MM/YYYY.');
      }
      this.onChange(null);
      return;
    }

    const day = Number(digits.slice(0, 2));
    const month = Number(digits.slice(2, 4));
    const year = Number(digits.slice(4));
    const date = new Date(Date.UTC(year, month - 1, day));

    const isReal =
      date.getUTCFullYear() === year &&
      date.getUTCMonth() === month - 1 &&
      date.getUTCDate() === day &&
      year > 1900;

    if (!isReal) {
      this.invalid.set(true);
      this.errorText.set('That date doesn’t exist — check day and month.');
      this.onChange(null);
      return;
    }

    if (this.maxToday() && date.getTime() > Date.now()) {
      this.invalid.set(true);
      this.errorText.set('Date of birth cannot be in the future.');
      this.onChange(null);
      return;
    }

    const iso = `${year.toString().padStart(4, '0')}-${month.toString().padStart(2, '0')}-${day.toString().padStart(2, '0')}`;
    this.onChange(iso);
  }

  writeValue(value: string | null): void {
    if (!value) {
      this.display.set('');
      return;
    }
    const [year, month, day] = value.split('-');
    this.display.set(`${day}/${month}/${year}`);
  }
  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }
}
