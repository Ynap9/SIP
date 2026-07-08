<div align="center">

# 📦 SIPPACK

### Hệ thống kiểm tra & đóng gói tài liệu lưu trữ chuẩn SIP

**Kiểm tra hồ sơ và đóng gói tài liệu nộp lưu thành gói `SIP_tailieu` theo Thông tư 05/2025/TT-BNV (Phụ lục IV) — mở rộng từ chuẩn E-ARK CSIP.**

_SIPPACK by **SaoMaiSolutions**_

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Angular 21](https://img.shields.io/badge/Angular-21-DD0031?logo=angular&logoColor=white)](https://angular.dev/)
[![PrimeNG 21](https://img.shields.io/badge/PrimeNG-21-007AD9)](https://primeng.org/)
[![SQLite](https://img.shields.io/badge/SQLite-DB-003B57?logo=sqlite&logoColor=white)](https://www.sqlite.org/)
[![Desktop: Photino](https://img.shields.io/badge/Desktop-Photino%20%2B%20WebView2-2C2C2C)](https://www.tryphotino.io/)
[![TT05/2025/TT--BNV](https://img.shields.io/badge/Chu%E1%BA%A9n-TT05%2F2025%2FTT--BNV-2E7D32)](./docs/chuan-dong-goi-sip-tt05.md)

</div>

---

## 📑 Mục lục

- [Giới thiệu](#-giới-thiệu)
- [Tính năng chính](#-tính-năng-chính)
- [Công nghệ](#-công-nghệ)
- [Kiến trúc](#-kiến-trúc)
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [API](#-api)
- [Backend (Sip.BE)](#-backend-sipbe)
- [Frontend (Sip.FE)](#-frontend-sipfe)
- [Cơ sở dữ liệu](#-cơ-sở-dữ-liệu)
- [Bắt đầu nhanh](#-bắt-đầu-nhanh)
- [Tài liệu](#-tài-liệu)
- [Đóng góp](#-đóng-góp)
- [Contributors](#-contributors)
- [License](#-license)

---

## 📌 Giới thiệu

Khi nộp lưu tài liệu số, cơ quan/tổ chức cần đóng gói tài liệu theo **cấu trúc SIP chuẩn** quy định tại **Thông tư 05/2025/TT-BNV** (Phụ lục IV — _Cấu trúc dữ liệu tài liệu nộp / Submission Information Package — `SIP_tailieu`_). Chuẩn này được xây dựng mở rộng từ **E-ARK CSIP**: mỗi gói gồm `METS.xml` mô tả gói, các tệp **EAD** mô tả hồ sơ/tài liệu, và toàn bộ được nén thành tệp **ZIP** đúng cấu trúc.

**SIPPACK** giúp toàn bộ quy trình này trở nên đơn giản, chính xác và lặp lại được:

> 🎯 **Cấu hình** danh mục trường (import từ Excel) → ✅ **Kiểm tra** thư mục hồ sơ theo thời gian thực → 📦 **Đóng gói** thành gói `SIP_tailieu` (METS/EAD) và nén ZIP.

Ứng dụng vận hành ở hai chế độ từ **cùng một mã nguồn lõi**:

- 🌐 **Web-dev** — backend ASP.NET Core + frontend Angular chạy độc lập để phát triển.
- 🖥️ **Desktop Windows** — đóng gói thành **một file cài đặt duy nhất** (`SIPPACK-Setup.exe`), người dùng **không cần cài thêm .NET hay Node.js**.

---

## ✨ Tính năng chính

| Nhóm | Mô tả |
|---|---|
| ⚙️ **Cấu hình từ điển trường** | Import cấu hình **hồ sơ** & **văn bản** từ file Excel (sheet `Mô tả_*`) làm chuẩn đối chiếu: tên trường, mô tả, ràng buộc bắt buộc, giá trị enum. |
| ✅ **Kiểm tra hồ sơ realtime** | Quét một thư mục hồ sơ qua **3 giai đoạn**: cấu trúc thư mục → khớp mã định danh → nội dung Excel. Kết quả đẩy về **theo thời gian thực (SSE)**, tick xanh từng bước, báo lỗi ngay. |
| 📦 **Đóng gói SIP** | Sinh `METS.xml` + các tệp **EAD**, sắp xếp tài liệu đúng cấu trúc `SIP_tailieu` và **nén thành ZIP** theo TT05. |
| 🖥️ **Chạy như app Desktop** | Một tiến trình `.NET` tự host web server + mở cửa sổ WebView2. Cài bằng **1 installer**, chạy **offline**, không phụ thuộc runtime ngoài. |
| 🗂️ **Cấu hình đi kèm sẵn** | Bản desktop seed sẵn cơ sở dữ liệu cấu hình TT05 ngay lần chạy đầu tiên. |

---

## 🧰 Công nghệ

| Tầng | Công nghệ |
|---|---|
| **Backend** | `.NET 9` · ASP.NET Core Web API · **EF Core 9 + SQLite** · ClosedXML (đọc/ghi Excel) · AutoMapper · NLog · Swagger |
| **Frontend** | `Angular 21` (standalone + **signals**) · **PrimeNG 21** · Tailwind CSS v4 · @ng-icons/heroicons · moment |
| **Desktop** | **Photino.NET** (WebView2) · publish single-file self-contained `win-x64` · installer **Inno Setup** |
| **Chuẩn nghiệp vụ** | Thông tư **05/2025/TT-BNV** (Phụ lục IV) · **E-ARK CSIP** (METS, EAD) |

---

## 🏗️ Kiến trúc

**Web-dev** — frontend và backend chạy độc lập, giao tiếp qua HTTP/SSE (CORS):

```
┌─────────────────────────┐        HTTP / SSE        ┌──────────────────────────────┐
│   Angular 21 (Sip.FE)    │ ───────────────────────▶ │  ASP.NET Core API (Sip.BE)   │
│   http://localhost:4200  │ ◀─────────────────────── │     http://localhost:5145    │
│   PrimeNG · signals      │   {status,code,message,  │   Config · Check · Pack      │
└─────────────────────────┘            data}          └──────────────┬───────────────┘
                                                                      │  EF Core 9
                                                                      ▼
                                                              ┌───────────────┐
                                                              │  SQLite  📁   │
                                                              └───────────────┘
```

**Desktop** — **một tiến trình `.NET` duy nhất** (Photino host) vừa tự host Kestrel (phục vụ **API + SPA Angular cùng origin**) vừa mở cửa sổ WebView2:

```
┌──────────────────────────────────────────────────────────────┐
│                  SIPPACK.exe  (1 process .NET)                │
│  ┌────────────────────┐      ┌────────────────────────────┐  │
│  │  Photino + WebView2│◀────▶│  Kestrel (same-origin)     │  │
│  │  (cửa sổ UI)       │ HTTP │  ├─ /            → SPA SPA  │  │
│  │  Angular static    │  SSE │  └─ /api/core/.. → Web API  │  │
│  └────────────────────┘      └─────────────┬──────────────┘  │
│                                            │  EF Core 9       │
│                                            ▼                  │
│                            %AppData%\SipPack\sip.db  (SQLite) │
└──────────────────────────────────────────────────────────────┘
```

> 💡 `Program.BuildApp()` dựng `WebApplication` (services + pipeline) **dùng chung** cho cả bản web (`Program.Main`) và bản desktop (`Sip.Desktop` tự host trong process). Bản desktop dùng `MapFallbackToFile("index.html")` để SPA hoạt động cùng origin với API.

---

## 📂 Cấu trúc thư mục

```
Sip/
├── docs/                       # Tài liệu chuẩn TT05, thiết kế DB, plan đóng gói desktop
├── Sip.BE/                     # Backend .NET (solution)
│   ├── Sip.be.Api/             # Web API: Program / BuildApp + Controllers
│   │   └── Controllers/Documents/  # ConfigController · CheckController · PackController
│   ├── Sip.be.Application/     # Services (Config / Check / Pack), DTOs, BaseService, MappingProfile
│   ├── Sip.be.Domain/          # Entity: ConfigHoSo, ConfigVanBan
│   ├── Sip.be.Infratructure/   # SipDbContext (EF Core) + Migrations
│   ├── Sip.be.Shared/          # SipPackConstants, EadFieldMapping, schema .xsd template, ApiResponse
│   ├── Sip.Desktop/            # Host Photino (entrypoint bản desktop)
│   ├── build-desktop.ps1       # Script đóng gói desktop (build FE + publish + bundle seed)
│   └── installer/SIPPACK.iss   # Script Inno Setup → SIPPACK-Setup.exe
├── Sip.FE/                     # Frontend Angular (standalone + signals + PrimeNG)
├── publish/                    # (sinh ra) output desktop: SIPPACK.exe + wwwroot + seed
└── LICENSE                     # MIT
```

---

## 🔌 API

Tất cả endpoint thuộc nhánh `api/core/document/`. Mọi REST trả về **HTTP 200** với envelope chuẩn — đọc `status` trong body.

| Chức năng | Method | Endpoint |
|---|---|---|
| Import cấu hình hồ sơ | `POST` | `api/core/document/config/ho-so` |
| Xem cấu hình hồ sơ | `GET` | `api/core/document/config/ho-so` |
| Import cấu hình văn bản | `POST` | `api/core/document/config/van-ban` |
| Xem cấu hình văn bản | `GET` | `api/core/document/config/van-ban` |
| Kiểm tra thư mục hồ sơ (**SSE realtime**) | `POST` | `api/core/document/check/ho-so-folder` |
| Đóng gói SIP | `POST` | `api/core/document/pack/ho-so-folder` |

**Envelope chung:**

```jsonc
{
  "status": 1,          // 1 = thành công, 0 = lỗi
  "code": 0,
  "message": "...",
  "data": { /* payload */ }
}
```

> ✅ Phía client kiểm tra thành công bằng `status === 1` (`BaseComponent.isResponseSucceed(res)`).
>
> 📡 Endpoint **check** trả `text/event-stream`: mỗi bước kiểm tra (kể cả từng file) đẩy một event `CheckProgressDto` (`stage`, `targetType`, `target`, `status`, `code`, `message`, `completed`). Vì là `POST`, client đọc qua **`fetch` + `ReadableStream`**, parse từng dòng `data: {json}`.

---

## 🧩 Backend (Sip.BE)

Kiến trúc phân tầng (Clean Architecture rút gọn):

- **`Sip.be.Api`** — Controller mỏng, gọi service và trả `ApiResponse`. `Program.BuildApp()` dựng pipeline dùng chung cho web (`Main`) và desktop (`Sip.Desktop`). Có CORS, Swagger (chỉ Development), `UseDefaultFiles`/`UseStaticFiles` + SPA fallback, tự `Database.Migrate()` khi khởi động.
- **`Sip.be.Application`** — Nghiệp vụ lõi:
  - `ConfigService` — import Excel → 2 bảng config (hồ sơ / văn bản).
  - `CheckService` — stream **SSE** kiểm tra **3 giai đoạn**: cấu trúc → khớp mã → nội dung.
  - `PackService` — sinh **METS/EAD** + nén **ZIP**.
  - Tất cả kế thừa `BaseService` (DbContext + logger + mapper).
- **`Sip.be.Domain`** — Entity `ConfigHoSo`, `ConfigVanBan` (từ điển trường + mô tả/enum + cờ bắt buộc).
- **`Sip.be.Infratructure`** — `SipDbContext` (SQLite) + Migrations.
- **`Sip.be.Shared`** — Hằng số chuẩn SIP (`SipPackConstants`), ánh xạ trường ↔ EAD (`EadFieldMapping`), bộ schema `.xsd` template, `ApiResponse`.
- **`Sip.Desktop`** — Host Photino, tự host Kestrel trong process cho bản desktop.

> 📝 Dữ liệu gói (HoSo / TaiLieu) **không lưu DB** — được đọc trực tiếp từ Excel tại thời điểm đóng gói rồi sinh ra gói SIP. DB chỉ lưu **cấu hình từ điển trường**.

---

## 🎨 Frontend (Sip.FE)

- **Standalone components + signals** (không NgModule): template `@if` / `@for` / `@switch`, `inject()`, `signal()` / `input()` / `computed()`.
- **Layout kiểu Sakai**: topbar (nút hamburger + logo), sidebar/menu thẻ trắng **bo góc co giãn mượt**, footer chung; `LayoutService` quản trạng thái menu.
- **Các trang**:
  | Route | Trang | Mô tả |
  |---|---|---|
  | `/` | 🏠 Trang chủ | Giới thiệu + quy trình. |
  | `/dong-goi` | ✅ Kiểm tra & đóng gói | Chọn thư mục → kiểm tra realtime (progress + bảng) → nút **Đóng gói** mở popup nhập thông tin (mã phông, người tạo, thư mục xuất) → kết quả + mở thư mục. |
  | `/cau-hinh` | ⚙️ Cấu hình | Import/xem cấu hình hồ sơ & văn bản. |
- **Service**: `check` (SSE qua `fetch`), `config` & `pack` (REST), `desktop` (cầu nối native + `baseUrl` **same-origin** khi build desktop).
- **Nền tảng dùng lại**: `BaseComponent` cung cấp toast / confirm / validate / kiểm tra envelope; `app-data-table` (PrimeNG table giàu tính năng).

---

## 🗄️ Cơ sở dữ liệu

- **SQLite**, migration **chạy tự động** khi khởi động (`Database.Migrate()`).
- **Web-dev**: DB nằm tại `Sip.BE/Sip.be.Api/sip.db`.
- **Desktop**: DB nằm tại `%AppData%\SipPack\sip.db`.
  - 🌱 Lần chạy đầu tiên: copy từ `seed\sip.db` đi kèm app ⇒ **có sẵn cấu hình TT05**.
  - 🔒 Các lần sau: **giữ nguyên** dữ liệu người dùng.

---

## 🚀 Bắt đầu nhanh

### Yêu cầu

| Thành phần | Phiên bản | Bắt buộc cho |
|---|---|---|
| **.NET SDK** | `9.0+` | Backend / Desktop |
| **Node.js** + npm | `20+` | Frontend |
| **WebView2 Runtime** | (Win10/11 thường có sẵn) | Desktop |
| **Inno Setup** | mới nhất | (tuỳ chọn) tạo installer |

### 1️⃣ Chế độ phát triển (web-dev)

```bash
# Backend API → http://localhost:5145  (Swagger: /swagger)
cd Sip.BE/Sip.be.Api
dotnet run
```

```bash
# Frontend (cửa sổ khác) → http://localhost:4200
cd Sip.FE
npm install
npm start
```

### 2️⃣ Chế độ Desktop (đóng gói + chạy)

```powershell
# Build FE (config "desktop", same-origin) + publish .NET single-file + bundle DB seed
powershell -ExecutionPolicy Bypass -File Sip.BE/build-desktop.ps1

# Chạy app
.\publish\SIPPACK.exe
```

> 📦 Script `build-desktop.ps1` sẽ: build Angular (`--configuration desktop`) → `dotnet publish` **single-file self-contained win-x64** → copy `dist` Angular sang `publish/wwwroot` → bundle `seed/sip.db`. Output nằm ở **`publish/`** (gốc repo).

### 3️⃣ Tạo bộ cài đặt (installer)

Cài [Inno Setup](https://jrsoftware.org/isdl.php) → mở `Sip.BE/installer/SIPPACK.iss` → **Compile** → ra **`SIPPACK-Setup.exe`** (kèm shortcut Desktop/Start Menu, kiểm tra WebView2 Runtime).

---

## 📚 Tài liệu

| Tài liệu | Nội dung |
|---|---|
| [`docs/chuan-dong-goi-sip-tt05.md`](./docs/chuan-dong-goi-sip-tt05.md) | Chuẩn đóng gói `SIP_tailieu` (Phụ lục IV — TT05/2025/TT-BNV) + đối chiếu gói mẫu thực tế. |
| [`docs/thiet-ke-db-dong-goi-sip.md`](./docs/thiet-ke-db-dong-goi-sip.md) | Thiết kế dữ liệu phục vụ kiểm tra & đóng gói SIP. |
| [`docs/desktop-packaging.plan.md`](./docs/desktop-packaging.plan.md) | Phương án đóng gói desktop (Hướng C — .NET host WebView2 / Photino). |
| [`.claude/fe/docs/`](./.claude/fe/docs/) | `project-structure`, `ui-guidelines`, `sakai-layout`… của frontend. |

---

## 🤝 Đóng góp

Rất hoan nghênh đóng góp! Quy trình đề xuất:

1. **Fork** repo và tạo nhánh từ `main`: `git checkout -b feature/ten-tinh-nang`.
2. **Commit** theo chuẩn, message rõ ràng (mô tả thay đổi).
3. Đảm bảo backend build (`dotnet build`) và frontend build (`npm run build`) **không lỗi**.
4. Mở **Pull Request** kèm mô tả thay đổi (và ảnh chụp UI nếu có).
5. 📝 Khi đổi nghiệp vụ/chuẩn, **cập nhật tài liệu** tương ứng trong `docs/`.

---

## 👥 Contributors

| Thành viên | GitHub | Email | Tổ chức |
|---|---|---|---|
| **Dư Niệm An** | [@kiinp9](https://github.com/kiinp9) | vietcuong23122k2@gmail.com | **SaoMaiSolutions** |

---

## 📄 License

Dự án được phát hành theo giấy phép **MIT** — xem chi tiết tại [`LICENSE`](./LICENSE).

> © 2026 **SaoMaiSolutions**. _SIPPACK — đóng gói tài liệu lưu trữ chuẩn SIP._

<div align="center">

**[⬆ Về đầu trang](#-sippack)**

</div>
