import { Component, output, signal } from '@angular/core';

interface TourStep {
  emoji: string;
  title: string;
  text: string;
}

const STEPS: TourStep[] = [
  {
    emoji: '👋',
    title: 'Welcome to your clinic!',
    text: 'Everything is set up. This 30-second tour shows you what you’ll do every day — skip it any time.',
  },
  {
    emoji: '🧑‍⚕️',
    title: 'Add your team',
    text: 'Go to Staff and invite your doctors and receptionists by email — they choose their own password. Everyone sees only what their role needs.',
  },
  {
    emoji: '📋',
    title: 'Register patients',
    text: 'Reception adds a patient in 20 seconds — name and phone is enough. Search finds anyone instantly by name or number.',
  },
  {
    emoji: '📅',
    title: 'Book, check in, consult',
    text: 'Book an appointment, press Check in when the patient arrives, and the doctor sees them in the waiting room. One tap starts the consultation — diagnosis, vitals and a PDF prescription.',
  },
  {
    emoji: '💊',
    title: 'Track your pharmacy',
    text: 'Inventory keeps medicines and supplies counted — low stock rises to the top, expiring items get flagged, and prescriptions suggest medicine names from your own shelf.',
  },
];

/**
 * First-visit product tour: one calm card, four steps, always skippable.
 * The parent decides when to show it and stores the "seen" flag.
 */
@Component({
  selector: 'app-onboarding-tour',
  template: `
    <div class="tour-backdrop">
      <div class="tour-card">
        <div class="tour-emoji">{{ current().emoji }}</div>
        <h2 class="tour-title">{{ current().title }}</h2>
        <p class="tour-text">{{ current().text }}</p>

        <div class="tour-dots">
          @for (step of steps; track $index) {
            <span class="tour-dot" [class.active]="$index === index()"></span>
          }
        </div>

        <div class="tour-actions">
          <button type="button" class="btn btn-ghost" (click)="closed.emit()">Skip tour</button>
          <button type="button" class="btn btn-primary" (click)="next()">
            {{ isLast() ? "Let's go 🚀" : 'Next' }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .tour-backdrop {
      position: fixed; inset: 0; z-index: 90;
      display: flex; align-items: center; justify-content: center;
      background: rgb(12 43 35 / .5);
      backdrop-filter: blur(3px);
      animation: tourFade .25s ease;
      padding: 16px;
    }
    @keyframes tourFade { from { opacity: 0; } }

    .tour-card {
      background: var(--color-surface);
      border-radius: 20px;
      box-shadow: 0 24px 70px rgb(12 43 35 / .3);
      max-width: 400px;
      width: 100%;
      padding: 34px 30px 24px;
      text-align: center;
      animation: tourPop .3s cubic-bezier(.3, 1.3, .5, 1);
    }
    @keyframes tourPop { from { transform: scale(.94); opacity: 0; } }

    .tour-emoji { font-size: 44px; margin-bottom: 14px; }

    .tour-title {
      font-size: 20px;
      font-weight: 800;
      margin-bottom: 10px;
    }

    .tour-text {
      font-size: 14px;
      color: var(--color-text-muted);
      line-height: 1.6;
      min-height: 68px;
    }

    .tour-dots {
      display: flex; justify-content: center; gap: 7px;
      margin: 18px 0 20px;
    }

    .tour-dot {
      width: 7px; height: 7px;
      border-radius: 50%;
      background: var(--color-border);
      transition: all .25s ease;

      &.active { background: var(--color-primary-500); width: 20px; border-radius: 999px; }
    }

    .tour-actions {
      display: flex;
      justify-content: space-between;
      gap: 10px;

      .btn-primary { min-width: 120px; }
    }
  `,
})
export class OnboardingTour {
  readonly closed = output<void>();

  readonly steps = STEPS;
  readonly index = signal(0);

  current(): TourStep {
    return this.steps[this.index()];
  }

  isLast(): boolean {
    return this.index() === this.steps.length - 1;
  }

  next(): void {
    if (this.isLast()) this.closed.emit();
    else this.index.update((i) => i + 1);
  }
}
