import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

// Temporary page for menu items whose screen is built in a later step
@Component({
  selector: 'app-coming-soon',
  template: `
    <div class="page">
      <header>
        <p class="eyebrow">{{ group }}</p>
        <h1>{{ heading }}</h1>
      </header>
      <section class="card">
        <h2>Màn hình đang được xây dựng</h2>
        <p class="muted">Màn này làm ở bước tiếp theo của Phase 6, theo mockup Figma.</p>
      </section>
    </div>
  `,
})
export class ComingSoon {
  // Heading and group come from the route's data, so one component serves every unfinished page
  private readonly data = inject(ActivatedRoute).snapshot.data;
  protected readonly heading = this.data['heading'] as string;
  protected readonly group = this.data['group'] as string;
}
