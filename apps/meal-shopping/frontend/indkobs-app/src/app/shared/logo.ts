import { Component, input } from '@angular/core';

/**
 * Brand-mærke: en indkøbspose med et flueben (indkøb + liste) i den grønne
 * brand-farve. Ren inline-SVG (ingen eksterne afhængigheder), skalerbar.
 */
@Component({
  selector: 'app-logo',
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 32 32"
         xmlns="http://www.w3.org/2000/svg" role="img" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id="mlLogoGrad" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stop-color="#2fbf7e" />
          <stop offset="1" stop-color="#0c5638" />
        </linearGradient>
      </defs>
      <rect x="1" y="1" width="30" height="30" rx="9" fill="url(#mlLogoGrad)" />
      <path d="M10.2 12.6h11.6l-.86 10.9a2.2 2.2 0 0 1-2.2 2.05H13.26a2.2 2.2 0 0 1-2.2-2.05Z" fill="#fff" />
      <path d="M12.95 12.6v-1.1a3.05 3.05 0 0 1 6.1 0v1.1" fill="none" stroke="#fff"
            stroke-width="1.9" stroke-linecap="round" />
      <path d="M13.5 18.4l1.95 1.95 3.35-3.7" fill="none" stroke="#12724c"
            stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
    </svg>
  `,
  styles: [':host { display: inline-flex; line-height: 0; }'],
})
export class LogoMark {
  size = input<number>(28);
}
