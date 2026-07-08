# Pages & Routing

## Routing model
- Root routes in `src/app.routes.ts`. Path `''` = the `Layout` shell; feature pages are its `children`.
- The **home** page is eager; every other feature is **lazy-loaded** via a per-feature router file:
  ```ts
  { path: 'cau-hinh',  loadChildren: () => import('./app/pages/config/config.router').then(m => m.configRoutes) },
  { path: 'dong-goi',  loadChildren: () => import('./app/pages/dong-goi/dong-goi.router').then(m => m.dongGoiRoutes) },
  ```
- Route paths are **kebab-case Vietnamese** (`cau-hinh`, `dong-goi`).
- Attach a `data: { title: '...' }` to each route for display.

## Per-feature router file (`<feature>.router.ts`)
Each feature folder owns a `Routes` export consumed by `loadChildren`:
```ts
export const configRoutes: Routes = [
  { path: '', component: Config, data: { title: 'Cấu hình hồ sơ/loại tài liệu' } }
];
```

## Page folder layout
```
pages/config/
├── config.ts          # component class `Config`
├── config.html        # template
├── config.scss        # styles (.scss for pages/shared components)
├── config.router.ts   # configRoutes
└── config.spec.ts     # test
```
Nested features nest folders, e.g. `pages/dong-goi/check/` and `pages/dong-goi/check/package-dialog/`.

## Page component pattern
- Standalone component; `imports: [...SharedImports, <extra components>]`.
- **Extend `BaseComponent`** (from `shared/components/base/base-component.ts`) for messages, confirms, form helpers, response checks.
- Override `ngOnInit()` (call nothing on super — it's empty) to load initial data.
- Override `onRouteActivated()` to reload when navigating back to the page.
- Use **signals** for view state: `rows = signal<IConfigItem[]>([])`, `tableLoading = signal(false)`.
- Breadcrumb: expose `home: MenuItem` and `breadcrumbItems: MenuItem[]`, render `<app-breadcrumb>`.
- Consume services via `inject(SomeService)`; subscribe and gate on `this.isResponseSucceed(res)` before using `res.data`.

## Skills / patterns
Reusable page recipes live in [`../docs/skills/`](../docs/skills/) (`feature-page.skill.md`, `dialog-action.skill.md`, `sakai-layout.skill.md`) and plans in [`../docs/plans/`](../docs/plans/). Read the matching skill before building a new page/dialog.
