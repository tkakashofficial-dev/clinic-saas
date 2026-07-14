import { Component, OnInit, computed, input, output, signal } from '@angular/core';

interface DayCell {
  iso: string;
  day: number;
  inMonth: boolean;
  isToday: boolean;
  disabled: boolean;
}

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/**
 * Klivia's own calendar — replaces the browser's native picker so every date
 * surface matches the brand. Three views with a fast path for far-away dates
 * (date of birth): tap the "July 2026" title → year grid → month → day.
 * The parent anchors it (position: relative) and closes it via (closed).
 */
@Component({
  selector: 'app-calendar-pop',
  template: `
    <div class="cal-backdrop" (click)="closed.emit()"></div>
    <div class="cal-panel" role="dialog" aria-label="Choose a date">
      <div class="cal-head">
        <button type="button" class="cal-nav" (click)="navigate(-1)" aria-label="Previous">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 18 9 12 15 6"/></svg>
        </button>
        <button type="button" class="cal-title" (click)="cycleView()">
          @switch (view()) {
            @case ('days') { {{ monthNames[viewMonth()] }} {{ viewYear() }} }
            @case ('months') { {{ viewYear() }} }
            @case ('years') { {{ yearRange().start }}–{{ yearRange().end }} }
          }
        </button>
        <button type="button" class="cal-nav" (click)="navigate(1)" aria-label="Next">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"/></svg>
        </button>
      </div>

      @switch (view()) {
        @case ('days') {
          <div class="cal-weekdays">
            @for (weekday of weekdays; track weekday) { <span>{{ weekday }}</span> }
          </div>
          <div class="cal-days">
            @for (cell of dayCells(); track cell.iso) {
              <button type="button" class="cal-day"
                      [class.outside]="!cell.inMonth"
                      [class.today]="cell.isToday"
                      [class.selected]="cell.iso === value()"
                      [disabled]="cell.disabled"
                      (click)="pickDay(cell)">{{ cell.day }}</button>
            }
          </div>
        }
        @case ('months') {
          <div class="cal-grid">
            @for (month of monthNames; track month; let i = $index) {
              <button type="button" class="cal-cell"
                      [class.selected]="i === viewMonth() && sameYearSelected()"
                      [disabled]="monthDisabled(i)"
                      (click)="pickMonth(i)">{{ month.slice(0, 3) }}</button>
            }
          </div>
        }
        @case ('years') {
          <div class="cal-grid">
            @for (year of yearCells(); track year) {
              <button type="button" class="cal-cell"
                      [class.selected]="year === selectedYear()"
                      [disabled]="yearDisabled(year)"
                      (click)="pickYear(year)">{{ year }}</button>
            }
          </div>
        }
      }

      <div class="cal-foot">
        <button type="button" class="cal-link" (click)="clear()">Clear</button>
        <button type="button" class="cal-link strong" (click)="goToday()">Today</button>
      </div>
    </div>
  `,
  host: { '[class.align-left]': 'align() === "left"' },
  styles: `
    :host { position: absolute; top: calc(100% + 6px); right: 0; z-index: 60; }
    :host(.align-left) { left: 0; right: auto; }

    .cal-backdrop { position: fixed; inset: 0; z-index: -1; }

    .cal-panel {
      width: 272px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 14px;
      box-shadow: 0 14px 40px rgb(12 43 35 / .18);
      padding: 12px;
      animation: calIn .16s cubic-bezier(.25, .8, .35, 1);
    }
    @keyframes calIn { from { opacity: 0; transform: translateY(-6px) scale(.98); } }

    .cal-head {
      display: flex; align-items: center; justify-content: space-between;
      margin-bottom: 8px;
    }

    .cal-nav {
      width: 30px; height: 30px;
      display: flex; align-items: center; justify-content: center;
      border: none; border-radius: 8px;
      background: transparent; cursor: pointer;
      color: var(--color-text-muted);
      &:hover { background: var(--color-primary-100); color: var(--color-primary-700); }
    }

    .cal-title {
      border: none; background: transparent; cursor: pointer;
      font: 700 13.5px var(--font-heading);
      color: var(--color-text);
      padding: 6px 10px; border-radius: 8px;
      &:hover { background: var(--color-primary-100); color: var(--color-primary-800); }
    }

    .cal-weekdays {
      display: grid; grid-template-columns: repeat(7, 1fr);
      margin-bottom: 2px;
      span {
        text-align: center;
        font-size: 10.5px; font-weight: 700;
        color: var(--color-text-muted);
        padding: 4px 0;
        text-transform: uppercase; letter-spacing: .04em;
      }
    }

    .cal-days { display: grid; grid-template-columns: repeat(7, 1fr); gap: 2px; }

    .cal-day {
      aspect-ratio: 1;
      display: flex; align-items: center; justify-content: center;
      border: none; border-radius: 9px;
      background: transparent; cursor: pointer;
      font: 500 12.5px var(--font-body);
      color: var(--color-text);
      transition: background .12s ease;

      &:hover:not(:disabled) { background: var(--color-primary-100); }
      &.outside { color: var(--color-text-muted); opacity: .45; }
      &.today { box-shadow: inset 0 0 0 1.5px var(--color-primary-400); font-weight: 700; }
      &.selected {
        background: var(--color-primary-600); color: #fff; font-weight: 700;
        &:hover { background: var(--color-primary-700); }
      }
      &:disabled { opacity: .25; cursor: default; }
    }

    .cal-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; }

    .cal-cell {
      padding: 12px 0;
      border: none; border-radius: 10px;
      background: transparent; cursor: pointer;
      font: 600 12.5px var(--font-body);
      color: var(--color-text);
      &:hover:not(:disabled) { background: var(--color-primary-100); }
      &.selected { background: var(--color-primary-600); color: #fff; }
      &:disabled { opacity: .25; cursor: default; }
    }

    .cal-foot {
      display: flex; justify-content: space-between;
      margin-top: 10px; padding-top: 8px;
      border-top: 1px solid var(--color-border);
    }

    .cal-link {
      border: none; background: transparent; cursor: pointer;
      font: 600 12.5px var(--font-body);
      color: var(--color-text-muted);
      padding: 5px 8px; border-radius: 7px;
      &:hover { background: var(--color-primary-100); color: var(--color-primary-700); }
      &.strong { color: var(--color-primary-700); }
    }
  `,
})
export class CalendarPop implements OnInit {
  /** Currently selected date (ISO yyyy-MM-dd) or null. */
  readonly value = input<string | null>(null);
  /** Disable dates after today (for birth dates). */
  readonly maxToday = input(false);
  /** Which edge of the anchor to hug (default: right). */
  readonly align = input<'left' | 'right'>('right');

  readonly picked = output<string>();
  readonly cleared = output<void>();
  readonly closed = output<void>();

  readonly monthNames = MONTHS;
  readonly weekdays = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

  private readonly todayDate = new Date();
  private readonly todayIso = toIso(this.todayDate);

  readonly view = signal<'days' | 'months' | 'years'>('days');
  readonly viewYear = signal(this.todayDate.getFullYear());
  readonly viewMonth = signal(this.todayDate.getMonth());

  ngOnInit(): void {
    // Inputs are bound by now — open on the already-selected month, if any
    const value = this.value();
    if (!value) return;
    const parsed = new Date(value + 'T00:00:00');
    if (Number.isNaN(parsed.getTime())) return;
    this.viewYear.set(parsed.getFullYear());
    this.viewMonth.set(parsed.getMonth());
  }

  readonly selectedYear = computed(() => {
    const value = this.value();
    return value ? Number(value.slice(0, 4)) : null;
  });

  readonly sameYearSelected = computed(() => this.selectedYear() === this.viewYear());

  readonly yearRange = computed(() => {
    const start = Math.floor(this.viewYear() / 12) * 12;
    return { start, end: start + 11 };
  });

  readonly yearCells = computed(() => {
    const { start } = this.yearRange();
    return Array.from({ length: 12 }, (_, i) => start + i);
  });

  readonly dayCells = computed<DayCell[]>(() => {
    const year = this.viewYear();
    const month = this.viewMonth();
    const first = new Date(year, month, 1);
    const gridStart = new Date(year, month, 1 - first.getDay()); // back to Sunday

    return Array.from({ length: 42 }, (_, i) => {
      const date = new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i);
      const iso = toIso(date);
      return {
        iso,
        day: date.getDate(),
        inMonth: date.getMonth() === month,
        isToday: iso === this.todayIso,
        disabled: this.maxToday() && iso > this.todayIso,
      };
    });
  });

  // Title click zooms out to the year grid — the DOB fast path
  // (year → month → day beats paging a month view back 30 years)
  cycleView(): void {
    this.view.set('years');
  }

  navigate(direction: 1 | -1): void {
    switch (this.view()) {
      case 'days': {
        const next = new Date(this.viewYear(), this.viewMonth() + direction, 1);
        this.viewYear.set(next.getFullYear());
        this.viewMonth.set(next.getMonth());
        break;
      }
      case 'months':
        this.viewYear.update((y) => y + direction);
        break;
      case 'years':
        this.viewYear.update((y) => y + direction * 12);
        break;
    }
  }

  pickDay(cell: DayCell): void {
    if (cell.disabled) return;
    this.picked.emit(cell.iso);
    this.closed.emit();
  }

  pickMonth(month: number): void {
    this.viewMonth.set(month);
    this.view.set('days');
  }

  pickYear(year: number): void {
    this.viewYear.set(year);
    this.view.set('months');
  }

  monthDisabled(month: number): boolean {
    if (!this.maxToday()) return false;
    return new Date(this.viewYear(), month, 1) >
      new Date(this.todayDate.getFullYear(), this.todayDate.getMonth(), 1);
  }

  yearDisabled(year: number): boolean {
    return this.maxToday() && year > this.todayDate.getFullYear();
  }

  goToday(): void {
    this.viewYear.set(this.todayDate.getFullYear());
    this.viewMonth.set(this.todayDate.getMonth());
    this.view.set('days');
    this.picked.emit(this.todayIso);
    this.closed.emit();
  }

  clear(): void {
    this.cleared.emit();
    this.closed.emit();
  }
}

function toIso(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}
