import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import QRCode from 'qrcode';

import { QrCode } from './qr-code';

type QrCodeInternals = {
  imageSrc: () => string | null;
  download(): void;
};

describe('QrCode', () => {
  const url = 'https://www.uuu.com.tw/Course/Show/1/AZ-104';

  let fixture: ComponentFixture<QrCode>;
  let component: QrCodeInternals;

  /**
   * Hands `download()` an anchor whose click can be observed, while every other element the
   * template or PrimeNG creates is still built for real.
   */
  function stubAnchorCreation(anchor: HTMLAnchorElement): jasmine.Spy {
    const create = document.createElement.bind(document);
    return spyOn(document, 'createElement').and.callFake(
      (tag: string) => (tag === 'a' ? anchor : create(tag)) as HTMLElement,
    );
  }

  /**
   * The encoder is asynchronous, so every test waits for the data URL to land before asserting.
   * `whenStable` covers the promise; `detectChanges` then renders the `<img>`.
   */
  async function setup(inputs: { data: string; caption?: string; size?: number }): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [QrCode],
      providers: [provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(QrCode);
    component = fixture.componentInstance as unknown as QrCodeInternals;

    fixture.componentRef.setInput('data', inputs.data);
    if (inputs.caption !== undefined) {
      fixture.componentRef.setInput('caption', inputs.caption);
    }
    if (inputs.size !== undefined) {
      fixture.componentRef.setInput('size', inputs.size);
    }

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('encodes the given URL and renders it as a PNG image', async () => {
    const encode = spyOn(QRCode, 'toDataURL').and.callThrough();

    await setup({ data: url, caption: 'AZ-104' });

    expect(encode).toHaveBeenCalledTimes(1);
    expect(encode.calls.mostRecent().args[0]).toBe(url);

    const img: HTMLImageElement = fixture.nativeElement.querySelector('img.qr-code__image');
    expect(img).toBeTruthy();
    expect(img.getAttribute('src')).toMatch(/^data:image\/png;base64,/);
    expect(component.imageSrc()).toBe(img.getAttribute('src'));
  });

  it('shows the caption and reuses it as the image alt text', async () => {
    await setup({ data: url, caption: 'AZ-104' });

    const caption: HTMLElement = fixture.nativeElement.querySelector('.qr-code__caption');
    expect(caption.textContent?.trim()).toBe('AZ-104');

    const img: HTMLImageElement = fixture.nativeElement.querySelector('img.qr-code__image');
    expect(img.getAttribute('alt')).toBe('AZ-104');
  });

  it('renders at the requested size', async () => {
    await setup({ data: url, caption: 'AZ-104', size: 240 });

    const img: HTMLImageElement = fixture.nativeElement.querySelector('img.qr-code__image');
    expect(img.getAttribute('width')).toBe('240');
    expect(img.getAttribute('height')).toBe('240');
  });

  it('re-encodes when the data changes', async () => {
    const encode = spyOn(QRCode, 'toDataURL').and.callThrough();

    await setup({ data: url, caption: 'AZ-104' });
    const first = component.imageSrc();

    fixture.componentRef.setInput('data', 'https://www.uuu.com.tw/Course/Show/2/MS-900');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(encode.calls.count()).toBe(2);
    expect(encode.calls.mostRecent().args[0]).toBe(
      'https://www.uuu.com.tw/Course/Show/2/MS-900',
    );
    expect(component.imageSrc()).not.toBe(first);
  });

  it('downloads the rendered code as a PNG named after the caption', async () => {
    await setup({ data: url, caption: 'AZ-104' });

    const anchor = document.createElement('a');
    const click = spyOn(anchor, 'click');
    const createElement = stubAnchorCreation(anchor);

    const button: HTMLElement = fixture.nativeElement.querySelector('p-button button');
    button.click();

    expect(createElement).toHaveBeenCalledWith('a');
    expect(click).toHaveBeenCalledTimes(1);
    expect(anchor.download).toBe('AZ-104.png');
    expect(anchor.getAttribute('href')).toMatch(/^data:image\/png;base64,/);
    expect(anchor.getAttribute('href')).toBe(component.imageSrc());
  });

  it('falls back to qrcode.png when there is no caption', async () => {
    await setup({ data: url });

    const anchor = document.createElement('a');
    spyOn(anchor, 'click');
    stubAnchorCreation(anchor);

    component.download();

    expect(anchor.download).toBe('qrcode.png');
  });

  it('shows the placeholder and downloads nothing while there is no code', async () => {
    await setup({ data: '' });

    expect(component.imageSrc()).toBeNull();
    expect(fixture.nativeElement.querySelector('img.qr-code__image')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('產生中…');

    const createElement = spyOn(document, 'createElement').and.callThrough();
    component.download();
    expect(createElement).not.toHaveBeenCalled();
  });
});
