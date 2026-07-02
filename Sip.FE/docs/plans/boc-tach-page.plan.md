# "Bóc tách dữ liệu" (unpack) screen — Build Plan

> New route `/boc-tach` that reverses a SIP package (.zip or extracted folder) back into a hồ sơ folder and shows
> the result as a collapsible **library tree** (PrimeNG `p-tree`). FE-only; the BE endpoint already exists.

## Requirements
- Sidebar menu item **"Bóc tách dữ liệu"** placed ABOVE "Cấu hình" (`src/app/layout/menu.ts`); route `/boc-tach`,
  lazy via a new `boc-tach.router.ts`, registered under `Layout`'s children in `src/app.routes.ts`.
- Page `pages/boc-tach/boc-tach.{ts,html,scss,spec.ts}` (`app-boc-tach`, standalone, `extends BaseComponent`,
  signals), full-width, `app-breadcrumb` header. Controls:
  - Drag-drop **box** for a `.zip` (desktop exposes absolute `file.path`; normalize with `Utils.normalizeFolderPath`).
  - **TEMP source-path input** for web-dev (mark `REMOVE on desktop build`).
  - **Output-directory input** (+ a desktop folder-picker button via `DesktopService.pickFolder()`).
  - **"Bóc tách"** button (right-aligned) + an INDETERMINATE `p-progressbar` shown while `loading()`.
  - Result area = the **library tree** + the reconstructed folder path with an **"Mở"** button.
- Click "Bóc tách" (= the save; no separate Lưu button): validate source + outputPath non-empty →
  `POST api/core/document/unpack/ho-so` (`{ sourcePath, outputPath }`) → on success show the returned `tree`.
  One synchronous REST call (not SSE); errors via toast (`isResponseSucceed`/`messageError`).
- **Library tree via PrimeNG `p-tree`**: map `result.tree` → `TreeNode[]` (folders `expanded:true`, icons per type,
  `data`=absolute path, `leaf` for files). Clicking a FOLDER label expands/collapses it; clicking a FILE label
  opens the file in its default viewer.
- **Result folder path + "Mở"**: after unpack, show `result.folderPath` (the reconstructed hồ sơ folder) in a
  read-only input with an **"Mở"** button next to it → opens that folder. Open (folder or file) via
  `DesktopService.openPath(path)`; web-dev (`openPath` false) → copy path to clipboard + toast (reuse the
  `openFolder` pattern from `package-dialog`).

## Decisions
- **D1 — Use PrimeNG `p-tree` (TreeModule), not a custom component.** Map `IFileNode` → `TreeNode`. A FOLDER-label
  click toggles expansion (matches "ấn vào tên thư mục xổ tree") by flipping `node.expanded` in `onNodeSelect`
  (the built-in chevron toggler also works); a FILE-label click opens the file. Import `TreeModule` in the page.
- **D2 — Open actions live in the page.** A single `openNode(path)` (`openPath` else clipboard+toast) is shared by
  the "Mở" button (parent folder) and file-label clicks. Opening a folder is NOT done by clicking it in the tree
  (folder click = expand) — it's the "Mở" button beside the result folder-path input.
- **D3 — Expand state on the TreeNode.** Seed folders `expanded:true` when mapping so the whole structure is
  visible immediately; `onNodeSelect` on a folder flips `node.expanded` to collapse/expand.
- **D4 — Output path required (no auto-default).** "Bóc tách" writes to the output dir and returns the tree of
  real (openable) paths. Matches BE (`OutputPath` required).
- **D5 — Indeterminate progress.** The REST call is synchronous with no per-step events → `<p-progressbar
  mode="indeterminate" />` while `loading()`.

## Steps
1. **Model** `src/app/models/unpack.models.ts`: `IFileNode { name; path; type: 'folder'|'file'; children?: IFileNode[] }`,
   `IUnpackRequest { sourcePath; outputPath }`, `IUnpackResult { hoSoName; folderPath; totalDoc; tree?: IFileNode;
   warnings: string[]; message }`. `npm run build`.
2. **Service** `src/app/services/unpack.service.ts` (`@Injectable({providedIn:'root'})`, inject `HttpClient` +
   `DesktopService`): `unpack(req)` → `http.post<IBaseResponseWithData<IUnpackResult>>(
   `${desktop.apiBase}/api/core/document/unpack/ho-so`, req)`. `npm run build`.
3. **Page** `src/app/pages/boc-tach/boc-tach.{ts,html,scss,spec.ts}` (`app-boc-tach`, `extends BaseComponent`,
   `imports:[...SharedImports, ProgressBarModule, TreeModule, Breadcrumb]`): signals `sourcePath`, `outputPath`,
   `isDragOver`, `loading`, `result`, `treeNodes`; `onDrop`/`chooseOutput`; `canUnpack()`; `onUnpack()` →
   validate → `loading` → `unpack({sourcePath, outputPath})` → `isResponseSucceed` ? set `result` + map tree to
   `treeNodes` : `messageError`; `toTreeNode(node)` (folders `expanded:true`, icon, `data`=path, `leaf`);
   `onNodeSelect(node)` (folder → flip `expanded`; file → `openNode(node.data)`); `openNode(path)` = openPath else
   clipboard. Template: breadcrumb + drop box + (web) TEMP source input + output input (+ desktop picker) +
   right-aligned "Bóc tách" + `@if (loading()) { indeterminate p-progressbar }` +
   `@if (result(); as r) { header (hoSoName/totalDoc) + read-only folderPath input + "Mở" button +
   <p-tree [value]="treeNodes()" selectionMode="single" (onNodeSelect)="onNodeSelect($event.node)" /> + warnings }`.
   Minimal spec. `npm run build`.
4. **Route** `boc-tach.router.ts` (`bocTachRoutes=[{ path:'', component: BocTach, data:{title:'Bóc tách dữ liệu'} }]`);
   in `src/app.routes.ts` add
   `{ path: 'boc-tach', loadChildren: () => import('./app/pages/boc-tach/boc-tach.router').then(m => m.bocTachRoutes) }`
   under `Layout.children`. `npm run build`.
5. **Menu** `src/app/layout/menu.ts`: insert `{ label: 'Bóc tách dữ liệu', link: '/boc-tach', icon: 'pi pi-sitemap' }`
   BEFORE the `Cấu hình` item. `npm run build`.
6. Manual verify: pick .zip + output dir → "Bóc tách" → indeterminate bar → tree appears; folder-label click
   expands/collapses; file-label click opens the viewer; "Mở" opens the reconstructed folder; web-dev copies paths.

## Expected output
- **New:** `models/unpack.models.ts`, `services/unpack.service.ts`,
  `pages/boc-tach/boc-tach.{ts,html,scss,spec.ts}`, `pages/boc-tach/boc-tach.router.ts`.
- **Modified:** `src/app.routes.ts` (+`boc-tach` route), `src/app/layout/menu.ts` (+menu item above Cấu hình).
- **Untouched:** BE, single/multi check-pack flows, other pages.
- Result: `/boc-tach` reverses a SIP package to `outputDir/{fileCode}/` and shows a collapsible `p-tree`; file
  clicks open viewers and the "Mở" button opens the reconstructed folder.
