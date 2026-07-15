import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

/**
 * Terms of Service + Privacy Policy on one page. Public (no auth). Linked
 * from the landing footer, the signup screen and the billing page. Plain,
 * clinic-friendly language — and the price-change clause the founder needs.
 *
 * NOTE: sensible defaults, not a lawyer's draft — have it reviewed before
 * signing paying customers.
 */
@Component({
  selector: 'app-legal',
  imports: [RouterLink],
  templateUrl: './legal.html',
  styleUrl: './legal.scss',
})
export class Legal {
  private readonly auth = inject(AuthService);

  readonly lastUpdated = '15 July 2026';
  readonly company = 'Klivia';
  readonly contactEmail = 'taveperz@gmail.com';
  readonly whatsapp = '+91 62384 56205';

  /** Signed-in users go back to the app; visitors to the landing page. */
  get backLink(): string {
    return this.auth.isLoggedIn() ? '/dashboard' : '/welcome';
  }
}
