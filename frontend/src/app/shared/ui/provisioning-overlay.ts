import { Component, OnDestroy, OnInit, computed, input, signal } from '@angular/core';

/**
 * Full-screen "your clinic is being built" experience — light, calm, alive.
 * A paper plane glides diagonally while real progress happens behind it.
 * Steps advance quickly and HOLD on the last one until the server responds
 * (the parent removes the overlay the moment the request finishes) — so a
 * fast server means a fast finish; only a cold start makes it linger.
 */
@Component({
  selector: 'app-provisioning-overlay',
  template: `
    <div class="prov-backdrop" role="status" aria-live="polite">
      <!-- Paper plane on a diagonal glide, dotted trail behind it -->
      <div class="prov-sky" aria-hidden="true">
        <div class="prov-plane">
          <svg width="42" height="42" viewBox="0 0 24 24" fill="none"
               stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
            <path d="M22 2 11 13"/>
            <path d="M22 2 15 22l-4-9-9-4z"/>
          </svg>
        </div>
        <span class="trail t1"></span>
        <span class="trail t2"></span>
        <span class="trail t3"></span>
      </div>

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
        <p class="prov-hint">Almost there — please keep this page open.</p>
      </div>
    </div>
  `,
  styles: `
    .prov-backdrop {
      position: fixed; inset: 0; z-index: 100;
      display: flex; align-items: center; justify-content: center;
      background:
        radial-gradient(900px 520px at 50% -10%, rgb(0 189 143 / .14), transparent 60%),
        var(--color-bg);
      animation: provFade .3s ease;
      overflow: hidden;
    }
    @keyframes provFade { from { opacity: 0; } }

    /* ---- the plane ---- */
    .prov-sky { position: absolute; inset: 0; pointer-events: none; }

    .prov-plane {
      position: absolute;
      left: -60px; bottom: 18%;
      color: var(--color-primary-600);
      animation: provGlide 3.4s cubic-bezier(.45, .1, .5, .9) infinite;
      filter: drop-shadow(0 6px 14px rgb(0 132 101 / .25));
    }
    @keyframes provGlide {
      0%   { transform: translate(0, 0) rotate(12deg); opacity: 0; }
      12%  { opacity: 1; }
      82%  { opacity: 1; }
      100% { transform: translate(calc(100vw + 120px), -46vh) rotate(4deg); opacity: 0; }
    }

    .trail {
      position: absolute;
      left: -60px; bottom: 18%;
      width: 7px; height: 7px;
      border-radius: 50%;
      background: var(--color-primary-300);
      animation: provGlide 3.4s cubic-bezier(.45, .1, .5, .9) infinite;
    }
    .t1 { animation-delay: .12s; opacity: .5; scale: .8; }
    .t2 { animation-delay: .24s; opacity: .35; scale: .6; }
    .t3 { animation-delay: .36s; opacity: .2; scale: .45; }

    /* ---- the card ---- */
    .prov-card {
      position: relative;
      text-align: center;
      max-width: 400px;
      padding: 34px 30px;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 22px;
      box-shadow: 0 24px 60px rgb(12 43 35 / .14);
    }

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
      0%, 100% { box-shadow: 0 0 0 0 rgb(0 189 143 / .35); }
      50% { box-shadow: 0 0 0 14px rgb(0 189 143 / 0); }
    }

    .prov-title {
      color: var(--color-text);
      font-size: 22px; font-weight: 800;
      margin-bottom: 24px;
      overflow-wrap: anywhere;
    }

    .prov-steps {
      display: flex; flex-direction: column; gap: 12px;
      text-align: left;
      margin: 0 auto 24px;
      width: fit-content;
    }

    .prov-step {
      display: flex; align-items: center; gap: 12px;
      font-size: 14px;
      color: var(--color-text-muted);
      opacity: .55;
      transition: color .3s ease, opacity .3s ease;

      &.active { color: var(--color-text); opacity: 1; }
      &.done { color: var(--color-primary-700); opacity: 1; }
    }

    .prov-marker {
      width: 20px; height: 20px; flex: none;
      border-radius: 50%;
      border: 1.5px solid var(--color-border);
      display: flex; align-items: center; justify-content: center;
      color: var(--color-primary-600);

      .done & { border-color: var(--color-primary-400); background: var(--color-primary-100); }
      .active & { border-color: var(--color-primary-400); }
    }

    .prov-spinner {
      width: 9px; height: 9px;
      border-radius: 50%;
      border: 2px solid rgb(0 189 143 / .25);
      border-top-color: var(--color-primary-600);
      animation: provSpin .7s linear infinite;
    }
    @keyframes provSpin { to { transform: rotate(360deg); } }

    .prov-bar {
      height: 4px;
      border-radius: 999px;
      background: var(--color-primary-100);
      overflow: hidden;
      margin-bottom: 12px;
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

    .prov-hint { color: var(--color-text-muted); font-size: 12.5px; }
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
    // No artificial waiting: steps tick quickly and hold on the last one —
    // the PARENT removes this overlay the instant the server responds.
    this.timer = setInterval(() => {
      this.activeStep.update((step) => Math.min(step + 1, this.steps.length - 1));
    }, 700);
  }

  ngOnDestroy(): void {
    if (this.timer !== null) clearInterval(this.timer);
  }
}
