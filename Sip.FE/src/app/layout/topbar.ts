import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

import { Menu } from './menu';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink, Menu],
  template: `
    <div class="layout-topbar">
      <a class="layout-topbar-logo" routerLink="/" draggable="false">
        <img src="favicon.ico" alt="" class="h-5 w-5" />
        <span>SIPPACK</span>
      </a>

      <span class="layout-topbar-divider"></span>

      <app-menu />
    </div>
  `
})
export class Topbar {}
