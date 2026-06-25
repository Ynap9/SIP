import { Component } from '@angular/core';

@Component({
  selector: 'app-empty',
  template: `
    <div class="flex min-h-[55vh] flex-col items-center justify-center gap-5 text-center">
      <div
        class="flex h-24 w-24 items-center justify-center rounded-full bg-indigo-50 text-indigo-500 ring-8 ring-indigo-50/40"
      >
        <i class="pi pi-cog text-5xl"></i>
      </div>
      <div class="flex flex-col gap-2">
        <h2 class="text-xl font-semibold text-slate-700">Tính năng đang phát triển</h2>
        <p class="max-w-md text-slate-500">
          Tính năng kiểm tra &amp; đóng gói nhiều hồ sơ đang trong quá trình phát triển.
          Xin vui lòng liên hệ admin để biết thêm chi tiết.
        </p>
      </div>
      <span
        class="inline-flex items-center gap-2 rounded-full bg-amber-50 px-3 py-1 text-sm font-medium text-amber-600"
      >
        <i class="pi pi-clock"></i> Coming soon
      </span>
    </div>
  `
})
export class Empty {}
