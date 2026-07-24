import { Component, effect, input, signal } from '@angular/core';
import QRCode from 'qrcode';

/**
 * Renders any string as a QR code image. Generated fully client-side
 * (no network), so UPI QRs and booking links work offline and never
 * leak data to a third-party QR service.
 */
@Component({
  selector: 'app-qr-img',
  template: `
    @if (dataUrl(); as url) {
      <img [src]="url" [alt]="alt()" [style.width.px]="size()" [style.height.px]="size()" />
    }
  `,
  styles: `
    :host { display: inline-block; line-height: 0; }
    img { border-radius: 8px; image-rendering: pixelated; }
  `,
})
export class QrImg {
  readonly value = input.required<string>();
  readonly size = input(180);
  readonly alt = input('QR code');

  readonly dataUrl = signal<string | null>(null);

  constructor() {
    effect(() => {
      const text = this.value();
      if (!text) {
        this.dataUrl.set(null);
        return;
      }
      QRCode.toDataURL(text, {
        margin: 1,
        width: this.size() * 2,   // 2x for crisp rendering on retina screens
        color: { dark: '#0C2B23', light: '#FFFFFF' },
      })
        .then((url) => this.dataUrl.set(url))
        .catch(() => this.dataUrl.set(null));
    });
  }
}
