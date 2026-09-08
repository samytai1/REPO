import { DOCUMENT } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import QRCode from 'qrcode';

/** Default on-screen edge length, in CSS pixels. */
const DEFAULT_SIZE = 160;

/**
 * A QR code for an arbitrary URL, with a caption and a PNG download.
 *
 * The encoder returns a `data:image/png;base64,…` URL, which doubles as the `<img>` source and as
 * the href of the throw-away anchor the 下載 button clicks — so what the user saves is exactly the
 * image on screen. Encoding is asynchronous: `imageSrc` stays null until the first code is ready.
 */
@Component({
  selector: 'app-qr-code',
  imports: [ButtonModule],
  templateUrl: './qr-code.html',
  styleUrl: './qr-code.scss',
})
export class QrCode {
  private readonly document = inject(DOCUMENT);

  /** The value encoded into the code — for a course, its public 課程介紹 URL. */
  readonly data = input.required<string>();
  /** Shown above the code and used as the image's alt text; also the download's file name. */
  readonly caption = input('');
  /** Edge length of the rendered PNG, in pixels. */
  readonly size = input(DEFAULT_SIZE);

  protected readonly imageSrc = signal<string | null>(null);

  constructor() {
    effect(() => {
      const data = this.data();
      const width = this.size();

      if (!data) {
        this.imageSrc.set(null);
        return;
      }

      QRCode.toDataURL(data, { width, margin: 1, errorCorrectionLevel: 'M' })
        .then((url) => this.imageSrc.set(url))
        .catch(() => this.imageSrc.set(null));
    });
  }

  /** Saves the rendered code as `{caption}.png` (`qrcode.png` when there is no caption). */
  protected download(): void {
    const src = this.imageSrc();
    if (!src) {
      return;
    }

    const link = this.document.createElement('a');
    link.href = src;
    link.download = `${this.caption().trim() || 'qrcode'}.png`;
    link.click();
  }
}
