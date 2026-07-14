import { Component, computed, effect, input, output, signal } from '@angular/core';

export interface TourStep {
  /** CSS selector to spotlight; omitted/hidden target → centered card. */
  target?: string;
  emoji: string;
  title: string;
  text: string;
}

const DEFAULT_STEPS: TourStep[] = [
  {
    emoji: '👋',
    title: 'Welcome to your clinic!',
    text: 'Everything is set up. This quick tour points at the real menus — skip it any time.',
  },
  {
    target: '[data-tour="switcher"]',
    emoji: '🏥',
    title: 'Your clinic lives here',
    text: 'Click your clinic’s name to switch between clinics or open a new one. Each clinic’s patients and staff are completely separate.',
  },
  {
    target: '[data-tour="nav-patients"]',
    emoji: '📋',
    title: 'Patients',
    text: 'Register a patient in 20 seconds — name and phone is enough. Click any patient’s name for their full history and printable intake forms (🦷 dental / 🩺 general).',
  },
  {
    target: '[data-tour="nav-appointments"]',
    emoji: '📅',
    title: 'Appointments',
    text: 'Book, press Check in when the patient arrives, and the doctor consults with one tap — diagnosis, vitals and a PDF prescription.',
  },
  {
    target: '[data-tour="nav-inventory"]',
    emoji: '💊',
    title: 'Pharmacy & inventory',
    text: 'Track medicines and supplies. Low stock and expiring items rise to the top, and prescriptions suggest names from your own shelf.',
  },
  {
    target: '[data-tour="nav-staff"]',
    emoji: '🧑‍⚕️',
    title: 'Your team',
    text: 'Invite doctors and receptionists by email — they choose their own password. Everyone sees only what their role needs.',
  },
  {
    target: '[data-tour="bell"]',
    emoji: '🔔',
    title: 'Notifications',
    text: 'Bookings, arrivals and appointment reminders land here — so nothing at the front desk is missed.',
  },
  {
    target: '[data-tour="book-btn"]',
    emoji: '🚀',
    title: 'Ready to go',
    text: 'Book your first appointment right from here. You can replay this tour anytime from your profile menu.',
  },
];

/** One-step variant shown right after opening an additional clinic. */
export const NEW_CLINIC_HINT: TourStep[] = [
  {
    target: '[data-tour="switcher"]',
    emoji: '🎉',
    title: 'This is your new clinic',
    text: 'You’re now inside the new clinic — its patients and staff start fresh. Click here any time to jump back to your other clinics.',
  },
];

/**
 * Spotlight tour: dims the app and cuts a bright hole around the REAL
 * element for each step, with a tooltip beside it — users learn where
 * things actually live instead of reading an abstract card. Falls back
 * to a centered card when a target isn't on screen (e.g. phones).
 */
@Component({
  selector: 'app-onboarding-tour',
  template: `
    <div class="tour-layer" role="dialog" aria-modal="true">
      @if (spot(); as s) {
        <div class="tour-spot"
             [style.top.px]="s.top" [style.left.px]="s.left"
             [style.width.px]="s.width" [style.height.px]="s.height"></div>
      } @else {
        <div class="tour-dim"></div>
      }

      <div class="tour-card" [class.centered]="!spot()" [style]="cardStyle()">
        <div class="tour-emoji">{{ current().emoji }}</div>
        <h2 class="tour-title">{{ current().title }}</h2>
        <p class="tour-text">{{ current().text }}</p>

        <div class="tour-dots">
          @for (step of steps(); track $index) {
            <span class="tour-dot" [class.active]="$index === index()"></span>
          }
        </div>

        <div class="tour-actions">
          <button type="button" class="btn btn-ghost" (click)="closed.emit()">Skip</button>
          <div class="tour-nav">
            @if (index() > 0) {
              <button type="button" class="btn btn-secondary" (click)="back()">Back</button>
            }
            <button type="button" class="btn btn-primary" (click)="next()">
              {{ isLast() ? "Let's go 🚀" : 'Next' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: `
    .tour-layer { position: fixed; inset: 0; z-index: 95; }

    .tour-dim {
      position: absolute; inset: 0;
      background: rgb(12 43 35 / .55);
      backdrop-filter: blur(2px);
      animation: tourFade .25s ease;
    }

    /* The hole: a ring whose massive shadow dims everything else */
    .tour-spot {
      position: absolute;
      border-radius: 14px;
      box-shadow:
        0 0 0 4px rgb(0 189 143 / .55),
        0 0 0 9999px rgb(12 43 35 / .55);
      transition: all .3s cubic-bezier(.25, .8, .35, 1);
      animation: tourFade .25s ease;
      pointer-events: none;
    }
    @keyframes tourFade { from { opacity: 0; } }

    .tour-card {
      position: absolute;
      width: 320px;
      max-width: calc(100vw - 32px);
      background: var(--color-surface);
      border-radius: 18px;
      box-shadow: 0 24px 70px rgb(12 43 35 / .35);
      padding: 22px 22px 16px;
      animation: tourPop .3s cubic-bezier(.3, 1.3, .5, 1);
      transition: top .3s ease, left .3s ease;

      &.centered {
        top: 50%; left: 50%;
        transform: translate(-50%, -50%);
        text-align: center;
      }
    }
    @keyframes tourPop { from { scale: .95; opacity: 0; } }

    .tour-emoji { font-size: 34px; margin-bottom: 10px; }

    .tour-title { font-size: 17px; font-weight: 800; margin-bottom: 8px; }

    .tour-text {
      font-size: 13.5px;
      color: var(--color-text-muted);
      line-height: 1.55;
    }

    .tour-dots { display: flex; gap: 6px; margin: 14px 0 14px; }
    .centered .tour-dots { justify-content: center; }

    .tour-dot {
      width: 6px; height: 6px;
      border-radius: 50%;
      background: var(--color-border);
      transition: all .25s ease;
      &.active { background: var(--color-primary-500); width: 18px; border-radius: 999px; }
    }

    .tour-actions {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 8px;

      .btn { padding: 7px 12px; font-size: 13px; }
    }

    .tour-nav { display: flex; gap: 8px; }
  `,
})
export class OnboardingTour {
  readonly steps = input<TourStep[]>(DEFAULT_STEPS);
  readonly closed = output<void>();

  readonly index = signal(0);
  readonly spot = signal<{ top: number; left: number; width: number; height: number } | null>(null);

  readonly cardStyle = computed(() => {
    const spot = this.spot();
    if (!spot) return {};

    const cardWidth = 320;
    const cardHeight = 240; // generous estimate for clamping
    const gap = 14;
    let top: number;
    let left: number;

    if (spot.left + spot.width + gap + cardWidth < window.innerWidth) {
      // Beside it (sidebar items): card to the right, vertically aligned
      left = spot.left + spot.width + gap;
      top = Math.min(Math.max(12, spot.top - 8), window.innerHeight - cardHeight);
    } else {
      // Below it (topbar items), clamped into the viewport
      left = Math.min(Math.max(12, spot.left + spot.width / 2 - cardWidth / 2),
        window.innerWidth - cardWidth - 12);
      top = spot.top + spot.height + gap;
      if (top + cardHeight > window.innerHeight) top = Math.max(12, spot.top - cardHeight - gap);
    }

    return { top: `${top}px`, left: `${left}px` };
  });

  constructor() {
    effect(() => {
      const step = this.steps()[this.index()];
      this.locate(step);
    });
  }

  private locate(step: TourStep): void {
    if (!step?.target) {
      this.spot.set(null);
      return;
    }
    const el = document.querySelector<HTMLElement>(step.target);
    // offsetParent is null when the element (or the sidebar) is hidden
    if (!el || el.offsetParent === null) {
      this.spot.set(null);
      return;
    }
    el.scrollIntoView({ block: 'nearest' });
    requestAnimationFrame(() => {
      const rect = el.getBoundingClientRect();
      const pad = 6;
      this.spot.set({
        top: rect.top - pad,
        left: rect.left - pad,
        width: rect.width + pad * 2,
        height: rect.height + pad * 2,
      });
    });
  }

  current(): TourStep {
    return this.steps()[this.index()];
  }

  isLast(): boolean {
    return this.index() === this.steps().length - 1;
  }

  next(): void {
    if (this.isLast()) this.closed.emit();
    else this.index.update((i) => i + 1);
  }

  back(): void {
    this.index.update((i) => Math.max(0, i - 1));
  }
}
