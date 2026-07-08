# Shared (`src/app/shared`)

Reusable building blocks every feature composes.

## Layout
```
shared/
├── import.shared.ts            # SharedImports barrel (Angular + PrimeNG modules)
├── utils.ts                    # class Utils (static helpers)
├── components/
│   ├── base/base-component.ts  # abstract BaseComponent (pages extend this)
│   ├── breadcrumb/             # <app-breadcrumb>
│   └── data-table/             # <app-data-table> (rich PrimeNG table)
├── constants/data-table.constants.ts   # CellViewTypes
├── directives/                 # [appEnterKey], [appShiftEnterKey]
└── models/                     # data-table, environment, request-paging base models
```

## SharedImports (`import.shared.ts`)
A single exported `const SharedImports = [...]` array of Angular + PrimeNG modules. Spread it into a component's `imports`: `imports: [...SharedImports, Breadcrumb, DataTable]`. Add a module here once instead of importing per component.

## BaseComponent (`components/base/base-component.ts`)
Abstract `@Directive()` base that pages extend. Provides:
- Injected `router`, `_activatedRoute`, `_messageService`, `_dialogService`, `_confirmationService`.
- Toast helpers: `messageSuccess/Warning/Error(msg)`.
- Confirm dialogs: `confirmDelete(opt, cb)`, `confirmAction(opt, cb)`.
- Form helpers: `form`, `isFormInvalid()`, `getError(field)` driven by `ValidationMessages`.
- **Response gate**: `isResponseSucceed(res, showError?, successMsg?)` — checks `res.status === 1`, toasts on failure; call before reading `res.data`.
- Lifecycle hook `onRouteActivated()` — auto-called (via `NavigationEnd`) when navigating back to the page; override to reload.
- Paging defaults: `MAX_PAGE_SIZE=10`, `START_PAGE_NUMBER=1`.

## DataTable (`components/data-table`)
`<app-data-table>` — signal-input driven PrimeNG table. Key inputs: `columns` (`IColumn[]`), `data`, `pageSize`, `pageNumber`, `total`, `loading`, `hidePaginator`. Outputs: `onPageChanged`, `customEmit`, `onCustomComp`. Column rendering is chosen via `CellViewTypes` (`INDEX`, `BOOL_CHECK`, custom component outlet…). Persists column widths in `localStorage`.

## Models (`shared/models`)
- `request-paging.base.models.ts` — `IBaseResponse`, `IBaseResponseWithData<T>`, `IBaseResponsePaging<T>`, `IBaseRequestPaging`. Match BE `ApiResponse` (`status`, `code`, `message`, `data`).
- `data-table.models.ts` — `IColumn`, `ICustomEmit`.
- `environment.models.ts` — `IEnvironment`.

## Utils (`utils.ts`)
Static helper class: storage (`get/setLocalStorage`), date formatting via moment (`formatDateView`, `formatDateCallApi`), Vietnamese→ASCII (`convertVietnameseToEng`), money/percent formatting, and `normalizeFolderPath` (cleans a user-typed Windows path before sending to BE).
