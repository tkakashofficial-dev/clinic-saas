import { Component, OnDestroy, OnInit, computed, input, signal } from '@angular/core';

/**
 * Full-screen "your clinic is being built" experience. Clinic provisioning
 * can take up to ~60s on a cold server — this turns dead waiting time into
 * a moment that feels like something important is happening (because it is).
 */
@Component({
  selector: 'app-provisioning-overlay',
  template: `
    <div class="prov-backdrop" role="status" aria-live="polite">
      <div class="prov-card">
        <div class="prov-avatar">{{ initials() }}</div>
        <h2 class="prov-title">Setting up<br>{{ clinicName() }}</h2>

        <div class="prov-steps">
          @for (step of steps; track step; let i = $index) {
            <div class="prov-step"
                 [class.done]="i < activeStep()"
                 [class.active]="i === activeStep()">
              <span class="prov-marker">
                @if (i < activeStep()) {
                  <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
                } @else if (i === activeStep()) {
                  <span class="prov-spinner"></span>
                }
              </span>
              {{ step }}
            </div>
          }
        </div>

        <div class="prov-bar"><div class="prov-bar-fill"></div></div>
        <p class="prov-hint">This can take up to a minute — please don't close the page.</p>
      </div>
    </div>
  `,
  styles: `
    .prov-backdrop {
      position: fixed; inset: 0; z-index: 100;
      display: flex; align-items: center; justify-content: center;
      background:
        radial-gradient(900px 500px at 50% -10%, rgb(0 189 143 / .22), transparent 60%),
        var(--color-ink-900);
      animation: provFade .3s ease;
    }
    @keyframes provFade { from { opacity: 0; } }

    .prov-card { text-align: center; max-width: 380px; padding: 24px; }

    .prov-avatar {
      width: 64px; height: 64px;
      margin: 0 auto 18px;
      border-radius: 18px;
      background: var(--color-primary-500);
      color: #06362B;
      font: 700 22px var(--font-heading);
      display: flex; align-items: center; justify-content: center;
      animation: provPulse 1.6s ease-in-out infinite;
    }
    @keyframes provPulse {
      0%, 100% { box-shadow: 0 0 0 0 rgb(0 189 143 / .45); }
      50% { box-shadow: 0 0 0 16px rgb(0 189 143 / 0); }
    }

    .prov-title {
      color: var(--color-text-invert);
      font-size: 22px; font-weight: 800;
      margin-bottom: 26px;
      overflow-wrap: anywhere;
    }

    .prov-steps {
      display: flex; flex-direction: column; gap: 12px;
      text-align: left;
      margin: 0 auto 26px;
      width: fit-content;
    }

    .prov-step {
      display: flex; align-items: center; gap: 12px;
      font-size: 14px;
      color: rgb(244 250 247 / .38);
      transition: color .3s ease;

      &.active { color: var(--color-text-invert); }
      &.done { color: var(--color-primary-300); }
    }

    .prov-marker {
      width: 20px; height: 20px; flex: none;
      border-radius: 50%;
      border: 1.5px solid rgb(244 250 247 / .25);
      display: flex; align-items: center; justify-content: center;
      color: var(--color-primary-300);

      .done & { border-color: var(--color-primary-400); background: rgb(0 189 143 / .15); }
      .active & { border-color: var(--color-primary-400); }
    }

    .prov-spinner {
      width: 9px; height: 9px;
      border-radius: 50%;
      border: 2px solid rgb(0 189 143 / .3);
      border-top-color: var(--color-primary-400);
      animation: provSpin .7s linear infinite;
    }
    @keyframes provSpin { to { transform: rotate(360deg); } }

    .prov-bar {
      height: 4px;
      border-radius: 999px;
      background: rgb(244 250 247 / .12);
      overflow: hidden;
      margin-bottom: 14px;
    }

    .prov-bar-fill {
      height: 100%;
      width: 40%;
      border-radius: 999px;
      background: linear-gradient(90deg, var(--color-primary-600), var(--color-primary-300));
      animation: provSlide 1.4s ease-in-out infinite;
    }
    @keyframes provSlide {
      0% { transform: translateX(-100%); }
      100% { transform: translateX(350%); }
    }

    .prov-hint { color: rgb(244 250 247 / .45); font-size: 12.5px; }
  `,
})
export class ProvisioningOverlay implements OnInit, OnDestroy {
  readonly clinicName = input.required<string>();

  readonly steps = [
    'Creating your clinic space',
    'Isolating your data securely',
    'Setting up roles — Admin, Doctor, Reception',
    'Preparing your dashboard',
  ];

  readonly activeStep = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;

  readonly initials = computed(() =>
    this.clinicName()
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .slice(0, 2)
      .join('')
      .toUpperCase(),
  );

  ngOnInit(): void {
    // Pacing is theatrical: steps advance on a timer and hold on the last one
    // until the real request finishes (parent removes the overlay).
    this.timer = setInterval(() => {
      this.activeStep.update((step) => Math.min(step + 1, this.steps.length - 1));
    }, 1100);
  }

  ngOnDestroy(): void {
    if (this.timer !== null) clearInterval(this.timer);
  }
}
