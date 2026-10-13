import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  template: `
    <div class="layout-statusbar">
      <span class="flex items-center gap-2">
        <i class="pi pi-circle-fill text-green-500" style="font-size: 0.5rem"></i>
        Sẵn sàng
      </span>
      <span>SIPPACK · SaoMaiSolutions</span>
    </div>
  `
})
export class Footer {}
