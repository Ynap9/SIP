# Skill: Dialog Form + Async Action (Sip.FE)

Reusable pattern for a **PrimeNG dialog that collects form input, fires an async REST action
with a loading button, then shows a success modal whose result paths open a folder on desktop**.
Read `docs/project-structure.md` + `docs/skills/feature-page.skill.md` first (model/service/envelope/spec conventions all apply).

## When to use
Any "fill a form in a popup → call BE → show result/open output" flow (e.g. Đóng gói SIP).
NOT for full pages (use `feature-page.skill.md`) nor SSE progress (use the SSE pattern).

## Structure
- **Model** `models/<feature>.models.ts` — `IXxxRequest` (camelCase, BE DTO) + `IXxxResult`.
- **Service** `services/<feature>.service.ts` — `@Injectable({providedIn:'root'})`, `HttpClient` + `environment.baseUrl`, returns `IBaseResponseWithData<IXxxResult>`.
- **Dialog component** `pages/<feature>/<name>-dialog/<name>-dialog.ts|html|scss` + `.spec.ts`:
  - `extends BaseComponent`, `imports:[...SharedImports]` (Dialog/Button/InputText/InputNumber/Message already in `SharedImports`).
  - Inputs: feature data via `input<T>()`; visibility via `visible = model<boolean>()` (two-way `[(visible)]` from parent).
  - Two dialogs in ONE component: a `showForm`/`visible` form dialog and a `showSuccess` result dialog — avoids a 3rd component, shares the result signal.
- **Parent** opens dialog conditionally (guard) and renders `<app-<name>-dialog [(visible)]="show" ... />`.

## Loading button + signals
```ts
packing = signal(false);
result = signal<IXxxResult | null>(null);
showSuccess = signal(false);

onSubmit(): void {
  if (!this.form.outputPath().trim()) { this.invalid.set(true); return; } // validate required
  this.packing.set(true);
  this.svc.pack(this.buildRequest()).subscribe({
    next: (res) => {
      this.packing.set(false);
      if (this.isResponseSucceed(res)) {            // res.status === 1
        this.result.set(res.data);
        this.visible.set(false);                    // close form
        this.showSuccess.set(true);                 // open success modal
      } else {
        this.messageError(res?.message || 'Thao tác thất bại. Vui lòng thử lại.');
      }
    },
    error: () => { this.packing.set(false); this.messageError('Không kết nối được dịch vụ.'); },
  });
}
```

## Template — p-dialog form + loading footer
```html
<p-dialog header="Đóng gói hồ sơ SIP" [(visible)]="visible" [modal]="true"
          [draggable]="false" [style]="{ width: '32rem' }">
  <div class="flex flex-col gap-3">
    <label>Thư mục xuất <span class="text-red-500">*</span></label>
    <input pInputText [ngModel]="outputPath()" (ngModelChange)="outputPath.set($event)" />
    @if (invalid() && !outputPath().trim()) {
      <p-message severity="error" text="Vui lòng nhập thư mục xuất" />
    }
    <!-- TEMP web-dev: paste path. REMOVE on desktop build (use native picker) -->
  </div>
  <ng-template pTemplate="footer">
    <p-button label="Hủy" [text]="true" (onClick)="visible.set(false)" [disabled]="packing()" />
    <p-button label="Đóng gói" icon="pi pi-box" severity="success"
              [loading]="packing()" [disabled]="packing()" (onClick)="onSubmit()" />
  </ng-template>
</p-dialog>
```

## Success modal + open folder
```html
<p-dialog header="Đóng gói thành công" [(visible)]="showSuccess" [modal]="true" [draggable]="false">
  @if (result(); as r) {
    <p>Tổng tài liệu: {{ r.totalDoc }}</p>
    <button class="text-indigo-600 underline break-all" (click)="openFolder(r.packagePath)">
      {{ r.packagePath }}
    </button>
    @if (r.zipPath) {
      <button class="text-indigo-600 underline break-all" (click)="openFolder(r.zipPath)">{{ r.zipPath }}</button>
    }
    @for (w of r.warnings; track w) { <p class="text-amber-600 text-sm">{{ w }}</p> }
  }
</p-dialog>
```
```ts
// Open output folder. Keep this the SINGLE swap point for desktop build.
openFolder(path: string): void {
  // TODO desktop (Electron): window.electron?.shell.openPath(path)
  // TODO desktop (Tauri):   import { openPath } from '@tauri-apps/plugin-opener'; openPath(path)
  navigator.clipboard?.writeText(path);                 // web-dev fallback
  this.messageSuccess('Đã sao chép đường dẫn');
}
```

## Conventions to keep
- File names NO `.component` suffix; selector `app-`. State via `signal`/`input`/`model`/`computed`; `@if`/`@for`.
- Extend `BaseComponent` → `messageSuccess/Warning/Error`, `isResponseSucceed(res)`. Toast for errors, modal for success.
- Build the request omitting hidden/optional fields (send `undefined`, e.g. `schemaSourceDir`), defaults set in code (`createZip: true`).
- Normalize any pasted folder path with `Utils.normalizeFolderPath()` before sending.
- EVERY component has a `.spec.ts` smoke test. End the build step with `npm run build`.
