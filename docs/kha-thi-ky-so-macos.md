# Khả thi hay không: SIPPACK ký số trên macOS

Tài liệu nghiên cứu (03/08/2026) — trả lời câu hỏi: **có làm được bản SIPPACK ký số chạy trên macOS không, và
những gì trong stack hiện tại còn dùng được / phải thay?**

Kết luận đặt trước, phần sau là bằng chứng.

---

## 1. Kết luận nhanh

**Khả thi về mặt kỹ thuật — nhưng KHÔNG phải bằng cách port nguyên xi.** Toàn bộ phần "lấy chứng thư + kiểm
token + ký" hiện tại dựa vào **Windows Certificate Store + CNG**, và trên macOS con đường đó **không tồn tại**:
chứng thư nằm trong USB token **không lên keychain**, nên `X509Store` trên mac sẽ trả về 0 chứng thư token.

Phải đổi sang **PKCS#11** — nói chuyện thẳng với thư viện `.dylib` của hãng token. Thư viện .NET cho việc này
(`Pkcs11Interop`, Apache 2.0) có sẵn, chạy được macOS, miễn phí. Các mảnh còn lại của luồng ký (dựng CMS/CAdES,
gọi TSA RFC 3161, ghi chữ ký incremental vào PDF) **giữ được gần như nguyên vẹn** vì chúng là code .NET thuần
hoặc BCL đa nền tảng.

Ba điều kiện quyết định (phải kiểm chứng bằng token thật trước khi cam kết):

| # | Điều kiện | Nếu không đạt |
|---|-----------|---------------|
| 1 | Token Ban Cơ yếu đang dùng **có** thư viện PKCS#11 cho macOS (của hãng hoặc qua OpenSC) | Dừng — không có đường nào khác |
| 2 | Thư viện đó là **arm64 hoặc universal** (máy Mac hiện nay đều Apple Silicon) | Phải build app x64 chạy Rosetta, hoặc dùng OpenSC thay thế |
| 3 | Chấp nhận **Apple Developer Program 99 USD/năm** để ký + notarize | App vẫn chạy được nhưng người dùng phải tự "Open Anyway" mỗi máy |

Ước lượng công sức: **4–6 tuần cho 1 dev**, chưa tính thời gian chờ mua máy Mac + token test.

---

## 2. Hiện tại đang phụ thuộc Windows ở chỗ nào

Rà soát code thật trong `Sip.BE`:

| Thành phần | File | Phụ thuộc Windows | Chạy được trên mac? |
|---|---|---|---|
| Đọc danh sách chứng thư | `CertificateProvider.cs` | `X509Store(My, CurrentUser/LocalMachine)`, `RSACng.Key.Provider`, `RSACryptoServiceProvider.CspKeyContainerInfo` | ❌ Phải viết lại |
| Kiểm token + ký thử + cache PIN | `StoreSigningKey.cs` | như trên + `CngProperty("HWND Handle")` | ❌ Phải viết lại |
| Parent hộp thoại PIN | `SigningUiContext.cs`, `Program.cs` | HWND, `CryptSetProvParam(PP_CLIENT_HWND)` | ❌ Bỏ (mac không có hộp PIN của middleware theo cách này) |
| Chẩn đoán smart card | `Program.cs` | `winscard.dll`, `SCardEstablishContext` | ❌ Thay bằng liệt kê slot PKCS#11 |
| Tự cài driver token | `Program.cs`, `installer/SIPPACK.nsi` | Registry, `msiexec`, UAC, NSIS | ❌ Không có tương đương (xem §6) |
| Dựng CMS/CAdES | `PdfSigner.cs` (`CadesSigner`) | `SignedCms`, `CmsSigner`, `AsnWriter` — BCL đa nền tảng | ⚠️ Chạy được, nhưng khâu **truyền khóa PKCS#11 vào CmsSigner** cần spike |
| Dấu thời gian TSA | `TimestampClient.cs` | `Rfc3161TimestampRequest` (managed) + HttpClient | ✅ Chạy được |
| Ghi chữ ký vào PDF | `PdfIncrementalSigner.cs`, `PdfObjectWriter.cs` | Code tự viết, byte thuần | ✅ Chạy được |
| Vẽ khối chữ ký | `PdfAppearanceBuilder.cs` | PDFsharp + font `"Times New Roman"` hệ thống | ⚠️ Phải nhúng font (xem §5) |
| Verify chữ ký + ghim CA | `SignatureVerifier.cs` | `X509Chain` + `CustomRootTrust` + `VerificationTime` | ⚠️ Rủi ro cao trên macOS (xem §4) |
| Host desktop | `Sip.Desktop` | Photino + WebView2, `RuntimeIdentifier=win-x64` | ⚠️ Photino có bản mac (WKWebView) nhưng phải đóng `.app` |
| Kiểm tra / Đóng gói / Bóc tách | `CheckService`, `PackService`... | Đường dẫn, tên file tiếng Việt | ⚠️ Vướng Unicode NFD (xem §7) |

Tóm lại: **phần mật mã và phần PDF giữ được; phần "chạm phần cứng" và phần "chạm hệ điều hành" phải làm lại.**

---

## 3. Rào cản lớn nhất: lấy chứng thư từ USB token trên macOS

### 3.1 Vì sao `X509Store` vô dụng trên mac

Trên Windows, middleware token (bit4id xPKI / VGCA PKI Manager) **bắc cầu** chứng thư trong token vào
Certificate Store qua minidriver + dịch vụ `CertPropSvc` — nhờ đó SIPPACK chỉ cần đọc store là thấy token,
đúng như comment trong `CertificateProvider.cs`.

macOS **không làm như vậy**. Từ macOS Sierra, Apple thay `tokend` bằng **CryptoTokenKit (CTK)**; từ Catalina
(10.15) trở đi, chứng thư trên smart card/token **không còn hiện trong Keychain Access**, thậm chí lệnh
`security find-identity` cũng không liệt kê. Ứng dụng phải gọi CTK trực tiếp.

Mà .NET trên macOS ánh xạ `CurrentUser\My` = `login.keychain`, `LocalMachine\My` = `System.keychain`
(theo tài liệu Cross-platform cryptography của Microsoft). Nghĩa là:

> `store.Certificates` trên mac chỉ trả về chứng thư **mềm** trong keychain — **không bao giờ thấy token**.

Ngoài ra, các kiểu `RSACng`, `ECDsaCng`, `CngProperty` mà `CertificateProvider`/`StoreSigningKey` đang dùng
để nhận diện "khóa nằm ở phần cứng" **chỉ tồn tại trên Windows** (bảng native interop của Microsoft ghi rõ
`RSACng: ❌` trên macOS).

### 3.2 Đường thay thế: PKCS#11

Cách duy nhất thực tế: nạp thẳng thư viện PKCS#11 (`.dylib`) mà hãng token cung cấp và tự làm
`C_GetSlotList → C_OpenSession → C_Login(PIN) → C_FindObjects(cert) → C_Sign`.

| Nhu cầu | Lib Windows hiện tại | Tương đương trên macOS | Giấy phép |
|---|---|---|---|
| Liệt kê chứng thư | `X509Store` | **Pkcs11Interop** (`C_FindObjects` với `CKO_CERTIFICATE`) | Apache 2.0 (free) |
| Kiểm token đang cắm | `ScanStoreForCertificate` | `C_GetSlotList(tokenPresent: true)` | — |
| Nhập PIN | Hộp thoại của middleware | **Tự làm dialog trong Angular** rồi `C_Login` | — |
| Ký raw | `RSA.SignData` qua CNG | `C_Sign` với `CKM_SHA256_RSA_PKCS` | — |
| Dựng CMS quanh chữ ký raw | `SignedCms`/`CmsSigner` | Giữ BCL nếu spike đạt, **fallback BouncyCastle** `CmsSignedDataGenerator` | MIT (free) |

Điểm cộng: bỏ được toàn bộ trò HWND/PP_CLIENT_HWND — PIN do **app tự hỏi**, nên không còn cảnh "hộp thoại PIN
chìm sau cửa sổ". Điểm trừ: ta phải tự quản phiên đăng nhập token (login 1 lần cho cả lô, logout khi xong),
tự phát hiện rút token, và tự xử lý đếm sai PIN (khóa token sau N lần — **rất dễ gây sự cố thật với khách**).

### 3.3 Thực trạng driver token ở Việt Nam (phần rủi ro nhất)

Đây là chỗ tài liệu không thể kết luận thay cho thực nghiệm:

- **bit4id** — có middleware macOS thật; thư viện PKCS#11 nằm ở
  `/Applications/PKIManager-bit4id.app/Contents/Resources/etc/libbit4xpki.dylib`. Đây là tín hiệu tốt vì token
  Ban Cơ yếu đời mới chính là bit4id (khớp với `installer/drivers/bit4id*.exe` đang bundle).
- **SafeNet/Thales (token VGCA đời cũ)** — có `libeToken.dylib` trong
  `/Library/Frameworks/eToken.framework/`, nhưng bản Apple Silicon native thì tùy phiên bản SAC, phải kiểm.
- **OpenSC** — mã nguồn mở, đã có **installer arm64 cho Apple Silicon**, hỗ trợ nhiều dòng token (kể cả
  eToken 5110). Là phương án dự phòng khi driver hãng thiếu hoặc chỉ có x86_64.
- **Cảnh báo từ hệ sinh thái VN**: hướng dẫn cài token trên macOS của MISA nói rõ phần mềm quản lý token
  của họ **chỉ chạy trên Mac chip Intel**, và **việc kích hoạt token bắt buộc phải làm trên Windows**. Ban Cơ
  yếu cũng chỉ phát hành công cụ ký (VGCA Sign Service) cho Windows. Nói cách khác, **macOS là công dân hạng
  hai trong toàn bộ hệ sinh thái chữ ký số chuyên dùng VN** — phải tính tới việc khách hàng vẫn cần một máy
  Windows cho các việc khác (kích hoạt, đổi PIN, gia hạn).

**Bắt buộc phải đo trước**: cắm đúng token đang dùng vào Mac, chạy
`pkcs11-tool --module <đường-dẫn>.dylib -L -O` xem có liệt kê được slot + chứng thư không, và
`lipo -archs <dylib>` xem có arm64 không. Nếu bước này không qua, mọi thứ phía sau vô nghĩa.

---

## 4. Rào cản thứ hai: dựng chain + ghim CA Ban Cơ yếu

`SignatureVerifier.cs` (dòng ~470) đang dùng đúng bộ ba mà macOS hay có vấn đề:

```csharp
chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
chain.ChainPolicy.TrustMode      = X509ChainTrustMode.CustomRootTrust;   // ghim Root G1/G2
chain.ChainPolicy.VerificationTime = verificationTime;                    // xác thực theo thời điểm ký
```

Những gì đã ghi nhận trên macOS:

- `CustomTrustStore` từng **fail hẳn** trên macOS (dotnet/runtime #1923); và trên Linux/macOS có báo cáo
  `Build()` trả `true` **dù thumbprint root trong CustomTrustStore không khớp** (#127903) — tức là **cơ chế
  ghim CA có thể bị vô hiệu lặng lẽ**, đúng thứ nguy hiểm nhất với nghiệp vụ này.
- `VerificationTime` **không được tôn trọng** trên Linux/macOS trong ngữ cảnh thu hồi (#41460).
- macOS không hỗ trợ `X509RevocationMode.Offline` (bị coi như `Online`) và bỏ qua `UrlRetrievalTimeout`.

**Đề xuất:** không phụ thuộc `X509Chain` của OS nữa mà **tự validate chain bằng BouncyCastle**
(`PkixCertPathBuilder`/`PkixCertPathValidator`) với trust anchor là chính các file `rootca.crt`/`rootcag2.crt`
đang bundle. Lợi ích kép: kết quả **giống hệt nhau trên Windows và macOS** (hiện tại verify là nghiệp vụ
sống–chết của SIPPACK), và bỏ được sự khác biệt engine giữa hai OS. Chi phí: viết lại ~200 dòng + bộ test
đối chiếu với kết quả Windows hiện tại trên bộ file mẫu.

---

## 5. Rào cản thứ ba: PDF và font

- `PdfIncrementalSigner`, `PdfObjectWriter`, `PdfSignatureReader`, `PdfFormatInspector` là **code tự viết trên
  byte thuần** → chạy macOS không sửa gì. Đây là lợi thế lớn của quyết định "tự viết incremental signer"
  (xem `pdf-incremental-signing`).
- **PDFsharp 6 trên macOS không tìm thấy font hệ thống nào cả.** Tài liệu PDFsharp nói thẳng: cơ chế thay thế
  font "luôn chạy trên Windows, đôi khi chạy trên Linux, **không bao giờ chạy trên macOS**". Code hiện tại đặt
  `GlobalFontSettings.UseWindowsFontsUnderWindows = true` và gọi `new XFont("Times New Roman", ...)` → trên mac
  sẽ **ném exception ngay khi vẽ khối chữ ký**.
- Cách xử lý: viết `IFontResolver` + **nhúng file font vào app**. Lưu ý bản quyền: **Times New Roman không được
  phép phân phối kèm**; phải chọn font serif miễn phí có đủ dấu tiếng Việt (Liberation Serif / Noto Serif) —
  và chấp nhận **khối chữ ký trông hơi khác bản Windows**, hoặc dùng luôn font đó cho cả hai nền để đồng nhất.

---

## 6. Rào cản thứ tư: đóng gói và phân phối

| Việc | Windows hiện tại | macOS |
|---|---|---|
| WebView | WebView2 (tự cài ngầm nếu thiếu) | **WKWebView có sẵn trong OS** — bỏ được cả bước cài runtime |
| Host | Photino.NET + `win-x64` | Photino có bản mac, publish `osx-arm64`/`osx-x64`, phải gói thành **`.app` bundle** (từng có lỗi `DllNotFoundException` với `Photino.Native` trên M1 → cần test sớm) |
| Bộ cài | NSIS (free) | **`.dmg`** (script `create-dmg`, free) hoặc **`.pkg`** (`productbuild` trong Xcode CLT, free) |
| Tự cài driver token | Nhúng bit4id + GCA01 + chạy im lặng qua UAC | **Không làm được tương đương**: `.pkg` của hãng cần quyền admin và không nên chạy ngầm; thực tế phải **hướng dẫn người dùng tự cài middleware** — trái với hướng đã chọn trên Windows |
| Chữ ký ứng dụng | Không bắt buộc | **Developer ID + notarization**: cần Apple Developer Program **99 USD/năm**; ký với `-o runtime` (hardened runtime), gửi Apple notarize, staple ticket |
| Nạp `.dylib` PKCS#11 của hãng khác | — | Hardened runtime **chặn nạp dylib ký bởi developer khác** → bắt buộc entitlement **`com.apple.security.cs.disable-library-validation`** |
| Nếu không mua Developer ID | — | App vẫn chạy nhưng macOS chặn lần đầu; người dùng phải vào *Privacy & Security → Open Anyway* trên **từng máy** (macOS Sequoia đã bỏ mẹo chuột phải → Open) |
| Máy build | Windows | **Bắt buộc có máy macOS** để build/ký/notarize (hoặc GitHub Actions macOS runner) |

Lưu ý về ghi chú "chỉ dùng tooling miễn phí": khoản 99 USD/năm của Apple là **không tránh được** nếu muốn giao
sản phẩm cho khách mà không kèm hướng dẫn bấm qua cảnh báo bảo mật.

**Kiến trúc/Rosetta:** nếu `.dylib` PKCS#11 của token chỉ có **x86_64**, thì **cả tiến trình SIPPACK phải là
x86_64** (không trộn kiến trúc trong một process) → publish `osx-x64` và chạy dưới Rosetta 2. Chạy được, nhưng
chậm hơn và phải yêu cầu người dùng cài Rosetta.

---

## 7. Rào cản thứ năm: tên file tiếng Việt (ảnh hưởng cả Kiểm tra/Đóng gói/Bóc tách)

macOS chuẩn hóa tên file theo **NFD (Unicode dạng tách)**, còn Windows/Linux dùng **NFC (dạng gộp)**. Cùng một
tên `Hồ sơ 01`, hai hệ điều hành lưu **hai chuỗi byte khác nhau**.

Hệ quả trực tiếp với SIPPACK:

1. **Checksum/manifest trong METS**: tên file ghi vào METS/EAD trên mac sẽ ở dạng NFD → gói tạo trên mac và gói
   tạo trên Windows khác nhau về byte, và bên kiểm tra so chuỗi sẽ báo lệch.
2. **So khớp với Excel config**: `ConfigHoSo`/`ConfigVanBan` nạp từ Excel luôn ở NFC → so sánh trực tiếp với tên
   file đọc từ đĩa trên mac sẽ **không khớp** dù mắt nhìn giống hệt.
3. **ZIP**: entry name của gói SIP tạo trên mac sẽ mang dạng NFD, giải nén trên Windows có thể ra tên "lạ".

**Cách xử lý:** chuẩn hóa **NFC** ở đúng hai biên — khi đọc tên file từ đĩa và khi ghi tên vào METS/EAD/ZIP
(`s.Normalize(NormalizationForm.FormC)`), và mọi phép so tên đều so trên chuỗi đã chuẩn hóa. Nên làm việc này
**ngay cả khi chưa port mac**, vì nó cũng bảo vệ trường hợp người dùng Windows nhận thư mục hồ sơ do máy Mac
tạo ra.

Cùng nhóm này: bỏ giả định `\` trong đường dẫn (đã thấy vài chỗ ở `SignService`/`PackService`/`CheckService`),
`Process.Start(path)` phải đổi thành `open <path>`, và DB SQLite chuyển từ `%AppData%\SipPack` sang
`~/Library/Application Support/SipPack`.

---

## 8. Bảng đối chiếu thư viện: hiện tại → macOS

| Chức năng | Đang dùng | macOS | Kết luận |
|---|---|---|---|
| Đọc/kiểm chứng thư token | Windows CertStore + CNG | Pkcs11Interop (Apache 2.0) + `.dylib` hãng / OpenSC | **Thay thế được**, cần token thật để xác nhận |
| Ký raw | `RSA.SignData` (CNG) | `C_Sign` qua PKCS#11 | Thay thế được |
| CMS / CAdES | `System.Security.Cryptography.Pkcs` | Chạy được trên mac; nếu `CmsSigner` không nhận khóa PKCS#11 → **BouncyCastle** (MIT) | Có đường dự phòng |
| ESS signingCertificateV2 | `AsnWriter` | Managed, đa nền tảng | Giữ nguyên |
| TSA RFC 3161 | `Rfc3161TimestampRequest` | Managed, đa nền tảng | Giữ nguyên |
| Verify chain + ghim CA | `X509Chain` + CustomRootTrust | **Không đáng tin trên mac** → BouncyCastle PKIX | Phải viết lại |
| Ghi chữ ký vào PDF | Code tự viết | — | Giữ nguyên |
| Vẽ khối chữ ký | PDFsharp + font hệ thống | PDFsharp + `IFontResolver` + font nhúng | Sửa nhỏ |
| Đọc/ghi Excel | ClosedXML | Đa nền tảng | Giữ nguyên |
| DB | EF Core + SQLite | Đa nền tảng | Đổi đường dẫn |
| Host desktop | Photino + WebView2 | Photino + WKWebView, `.app` bundle | Sửa vừa |
| Bộ cài | NSIS | `.dmg`/`.pkg` + notarize | Làm mới |

Không có mảnh nào **không có** phương án thay thế miễn phí. Điểm chết duy nhất nằm ở **driver token của hãng**,
tức là thứ nằm ngoài tầm kiểm soát của dự án.

---

## 9. Các phương án và khuyến nghị

**Phương án A — Port native, ký qua PKCS#11 (khuyến nghị).**
Tách `ICertificateProvider`/`ISigningKey` thành hai implementation: giữ nguyên bản Windows đang chạy tốt, thêm
`Pkcs11CertificateProvider`/`Pkcs11SigningKey`, chọn theo `RuntimeInformation.IsOSPlatform`. Không đụng vào
`PdfSigner`, `PdfIncrementalSigner`, `TimestampClient`, `SignService`. Đây là kiến trúc rẻ nhất về mặt rủi ro
vì **bản Windows không bị ảnh hưởng gì**.
Ưu: trải nghiệm gốc, một codebase. Nhược: phải tự làm UI nhập PIN, tự quản phiên token, cần Mac + 99 USD/năm.

**Phương án B — Bản mac chỉ hỗ trợ chứng thư mềm (.p12/.pfx).**
Rẻ (khoảng 1 tuần), tránh toàn bộ rủi ro driver. Nhưng chữ ký số **chuyên dùng** của Ban Cơ yếu nằm trong
token, nên bản này gần như **không dùng được cho nghiệp vụ thật** — chỉ hợp làm bản demo/thử nghiệm.

**Phương án C — Không port: chạy Windows ảo trên Mac.**
Parallels + Windows on ARM, USB passthrough token. Chi phí phát triển ~0 nhưng UX tệ, license Windows/Parallels
tốn tiền, và passthrough token qua máy ảo là nguồn lỗi kinh điển (đã thấy báo cáo lỗi cài driver token trên
Parallels/M1).

**Phương án D — Ký từ xa (remote signing).**
Máy Mac gửi hash lên một dịch vụ ký chạy Windows/HSM. Giải quyết triệt để vấn đề đa nền tảng và mở đường cho
bản web, nhưng là **thay đổi kiến trúc + pháp lý** (khóa rời khỏi tầm kiểm soát trực tiếp của người ký) — không
phải việc của bản mac đầu tiên.

**Khuyến nghị:** làm **spike ở §10 trước**. Spike đạt → đi phương án A. Spike không đạt (token không có PKCS#11
dùng được trên mac) → **không có phương án A**, khi đó chỉ còn C là thực tế.

---

## 10. Spike bắt buộc trước khi cam kết (2–3 ngày, cần 1 máy Mac + 1 token thật)

| # | Việc | Đạt khi |
|---|---|---|
| 1 | Cài middleware hãng trên mac, tìm `.dylib` PKCS#11 | Có file `.dylib`, `lipo -archs` cho biết kiến trúc |
| 2 | `pkcs11-tool --module <dylib> -L -O` | Liệt kê được slot + object chứng thư |
| 3 | `pkcs11-tool --sign` với PIN thật | Ký được, không khóa token |
| 4 | Console .NET dùng Pkcs11Interop trên `osx-arm64` (hoặc `osx-x64`) | Đọc được cert + `C_Sign` thành công |
| 5 | Dựng CMS: thử `CmsSigner` với RSA tự cài đặt; fail thì BouncyCastle | Ra file CMS mà `SignatureVerifier` bản Windows chấp nhận |
| 6 | Gọi TSA `tsa.ca.gov.vn` từ mac | Lấy được token thời gian |
| 7 | Ký 1 PDF thật đầu-cuối trên mac | Adobe Reader + SIPPACK Windows đều báo hợp lệ |
| 8 | Photino "hello world" + SPA trên mac | Cửa sổ mở, chọn được thư mục |

Điểm dừng: **bước 2 hoặc 4 không qua ⇒ dừng dự án mac native**, báo lại để cân nhắc phương án C.

---

## 11. Ước lượng công sức (sau khi spike đạt)

| Hạng mục | Ngày công |
|---|---|
| Lớp PKCS#11 (provider + signing key + phiên/PIN + giám sát rút token) | 5–8 |
| UI nhập PIN + luồng lỗi (sai PIN, token bị khóa, rút giữa lô) | 2–3 |
| Thay chain validation sang BouncyCastle + test đối chiếu Windows | 3–4 |
| Font resolver + nhúng font + so khớp khối chữ ký | 1–2 |
| Chuẩn hóa NFC + đường dẫn + `open`/AppData | 2–3 |
| Host mac: `.app` bundle, Photino, dialog, SQLite path | 3–4 |
| Đóng gói `.dmg`/`.pkg`, ký, notarize, CI | 3–5 |
| QA đầu-cuối trên mac Intel + Apple Silicon | 5 |
| **Tổng** | **~24–34 ngày công (4–6 tuần)** |

Chi phí ngoài: máy Mac (ưu tiên Apple Silicon), Apple Developer Program 99 USD/năm, ít nhất 1 token thật để test.

---

## 12. Rủi ro còn mở

1. **Driver token macOS của hãng cấp cho Ban Cơ yếu** — chưa xác nhận bằng thiết bị thật. Rủi ro cao nhất.
2. **Apple Silicon**: nếu `.dylib` chỉ x86_64 → phải chạy Rosetta cả app.
3. **Ghim CA trên macOS**: bằng chứng cho thấy `CustomTrustStore` của .NET có thể **không ghim thật** trên
   Linux/macOS → nếu không chuyển sang BouncyCastle thì có nguy cơ **chấp nhận chứng thư không thuộc Ban Cơ yếu**.
4. **Không tự cài được middleware** như trên Windows → phát sinh hỗ trợ khách hàng, ngược với hướng đã chọn.
5. **Hệ sinh thái CKS VN thiên vị Windows**: kích hoạt/đổi PIN/gia hạn token có thể vẫn buộc dùng máy Windows.
6. **Notarization**: pipeline build phải chạy trên macOS; thất bại notarize thường lộ ra muộn, nên làm sớm.

---

## Nguồn tham khảo

- [Cross-platform cryptography — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography) (X509Store/keychain, RSACng ❌ trên mac, X509Chain trên macOS)
- [dotnet/runtime #1923 — X509Chain validation fails on macOS using CustomTrustStore](https://github.com/dotnet/runtime/issues/1923)
- [dotnet/runtime #127903 — CustomRootTrust chấp nhận root sai thumbprint trên Linux/macOS](https://github.com/dotnet/runtime/issues/127903)
- [dotnet/runtime #41460 — VerificationTime không được tôn trọng trên Linux/macOS](https://github.com/dotnet/runtime/issues/41460)
- [A Contemporary Overview of Smart Card Support on macOS](https://blog.leiy.me/post/smartcard-on-macos/) và [OpenSC #1798 — chứng thư token không hiện trong Keychain từ Catalina](https://github.com/OpenSC/OpenSC/issues/1798)
- [Smart card integration in macOS: CryptoTokenKit plugin — Ludovic Rousseau](https://blog.apdu.fr/posts/2018/09/smart-card-integration-in-macos-sierra/)
- [Pkcs11Interop](https://github.com/Pkcs11Interop/Pkcs11Interop) và [issue #206 — build trên macOS ARM64 với universal dylib](https://github.com/Pkcs11Interop/Pkcs11Interop/issues/206)
- [Pkcs7SignatureGenerator — Pkcs11Interop + BouncyCastle](https://github.com/jariq/Pkcs7SignatureGenerator), [SignedCmsKeyVault — SignedCms với khóa từ xa phải dùng BouncyCastle](https://github.com/clairernovotny/SignedCmsKeyVault)
- [Bit4id Universal Middleware for macOS — đường dẫn `libbit4xpki.dylib`](https://suport.aoc.cat/en-US/article/?servei=tcat&id=KA-06988_bit4id-manual-for-a-mac-os)
- [SafeNet eToken trên macOS — `libeToken.dylib`](https://knowledge.digicert.com/solution/safenet-hardware-token-not-detected-in-adobe-reader-on-mac-os), [OpenSC #2163 — universal binary ARM/Intel](https://github.com/OpenSC/OpenSC/issues/2163)
- [MISA — cài USB Token trên macOS (chỉ Intel, kích hoạt phải trên Windows)](https://helpesign.misa.vn/knowledge-base/cai-dat-usb-token-tren-macos-doi-voi-usb-duoc-cap-sau-4-2021/)
- [PDFsharp — Font resolving (macOS không tìm thấy font hệ thống)](https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Font-Resolving-(6.1).html)
- [Photino — Cross-Platform Publishing (WKWebView, `.app` bundle)](https://github.com/tryphotino/photino.Samples/blob/master/Photino.PublishPhotino/PublishPhotino/README.md), [photino.NET #103 — DllNotFoundException trên M1](https://github.com/tryphotino/photino.NET/issues/103)
- [Apple Developer ID (99 USD/năm)](https://developer.apple.com/support/developer-id), [Gatekeeper & notarization in Sequoia](https://eclecticlight.co/2024/08/10/gatekeeper-and-notarization-in-sequoia/), [hardened runtime & library validation](https://eclecticlight.co/2021/01/07/notarization-the-hardened-runtime/)
- [Explainer: Unicode, normalization and APFS](https://eclecticlight.co/2021/05/08/explainer-unicode-normalization-and-apfs/)
