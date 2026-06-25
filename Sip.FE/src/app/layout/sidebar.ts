import { Component } from '@angular/core';

import { Menu } from './menu';

@Component({
  selector: 'app-sidebar',
  imports: [Menu],
  template: `
    <div class="layout-sidebar">
      <app-menu />
    </div>
  `
})
export class Sidebar {}
