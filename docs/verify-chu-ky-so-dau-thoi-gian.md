# Kiểm tra dấu thời gian tin cậy của chữ ký số (TSA Ban Cơ yếu Chính phủ)

> Cách SIPPACK chứng minh thời điểm ký của 1 PDF là thật — gắn với dấu thời gian RFC 3161 do TSA của Ban Cơ yếu
> Chính phủ cấp — thay vì tin vào giờ máy tự khai.
> **Đã hiện thực** (BE ở `Sip.be.External`, FE cột "Dấu thời gian"; đã verify runtime: 13/13 file thật `ValidTrusted`).
> Bản tiếng Anh (cho agents): [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. Vấn đề
`PdfSignatureReader` hiện chỉ đọc `signingTime` (PKCS#9 `1.2.840.113549.1.9.5`) — là giờ **người ký tự khai**,
chỉnh đồng hồ máy là giả được. Giờ đáng tin phải đến từ **token dấu thời gian RFC 3161** được TSA **ký số**. Cả
`signingTime` lẫn token đều nằm **trong cùng 1 file**, nên **chỉ so 2 con số thì không chứng minh được gì** — kẻ
gian đặt chúng bằng nhau là xong. Chỉ có **chữ ký của TSA + chuỗi tin cậy về root đã ghim** mới làm giờ trở nên đáng tin.

## 2. Trust anchor (phải ghim — cả tính năng phụ thuộc vào đây)
Token dấu thời gian nhúng sẵn toàn bộ chuỗi TSA, nên xác minh được **hoàn toàn offline** khi đã ghim root.

| Vai trò | Subject | SHA-256 |
|---|---|---|
| Root (anchor, ghim) | `RootCA chuyên dùng Chính phủ G2` (self-signed, 2018→2048) | `F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8` |
| TSA (ví dụ) | `Máy chủ cấp dấu thời gian 02, Ban Cơ yếu CP` | `EAE46D68…CE954BD6` |

- Lấy root ghim từ URL AIA của chính CA: **`http://ca.gov.vn/pki/pub/crt/rootcag2.crt`** (HTTP, không dính lỗi TLS
  của trang https). **Đã kiểm chứng độc lập:** bản tải chính thức này ≡ bản nhúng trong file thật, cùng `F04BA4B4…`
  → loại vòng lặp tin cậy. Sub-CA G1 (issuer người ký): `http://ca.gov.vn/pki/pub/cert/cp.crt` (`75E969C0…`);
  thu hồi: `http://ca.gov.vn/pki/pub/crl/rootcag2.crl`, `http://ocsp.ca.gov.vn`.
- Windows cert store **không** đáng tin ở đây (máy test chưa cài root Chính phủ).
- Cert người ký cũ chain về Sub-CA `Co quan chung thuc so Chinh phu` (đời G1). Để kiểm **dấu thời gian** chỉ cần
  Root G2 (TSA chain về G2); chuỗi của người ký chỉ cần khi muốn verify **chính chữ ký**.

## 3. Đầu vào
- Đường dẫn 1 PDF, hoặc thư mục `attachment/` của hồ sơ → kiểm mọi `.pdf` (không phải PDF → `N/A`).
- Bộ cert ghim (Root G2) đóng kèm app.
- `toleranceSeconds` (mặc định 300) — độ lệch tối đa cho phép giữa giờ tin cậy và giờ tự khai.

## 4. Các bước (mỗi PDF, đều offline)
1. Quét `/ByteRange` + `/Contents` cho từng chữ ký (tái dùng logic `PdfSignatureReader`).
2. `SignedCms.Decode(contents)` → `SignerInfo`; đọc `signingTime` (giờ tự khai) nếu có.
3. Định vị token dấu thời gian:
   - **PAdES-T**: unsigned attribute OID `1.2.840.113549.1.9.16.2.14` trên SignerInfo, hoặc
   - **PAdES-LTV**: 1 chữ ký riêng `/Type /DocTimeStamp` (`/SubFilter /ETSI.RFC3161`), phần `/Contents` là token.
4. `Rfc3161TimestampToken.TryDecode(tokenBytes, out token, out _)`.
5. `token.VerifySignatureForSignerInfo(signerInfo, out tsaCert)` — kiểm chữ ký TSA, `messageImprint` bao đúng
   **chữ ký này**, và EKU TSA `id-kp-timeStamping` (`1.3.6.1.5.5.7.3.8`).
6. Dựng `X509Chain` cho `tsaCert` với **trust anchor tùy biến = chỉ Root G2 đã ghim** (cert của token bỏ vào
   ExtraStore, revocation offline). Bắt buộc SHA-256 của root chuỗi == Root G2 đã ghim.
7. `trustedTime = token.TokenInfo.Timestamp` (genTime, UTC); kiểm nằm trong hiệu lực cert người ký.
8. So sánh có **hiệu chỉnh +7h của VGCA** (xem §7): `|signingTimeUtc − (genTime + 7h)| ≤ toleranceSeconds`.

## 5. Kết luận (verdict)
| Verdict | Điều kiện | Ý nghĩa |
|---|---|---|
| `VALID_TRUSTED` | có token, bước 5–6 đạt, thời gian trong ngưỡng | dấu thời gian Ban Cơ yếu thật |
| `NO_TIMESTAMP` | không có token RFC 3161 | không chứng minh được → **nghi ngờ/giả** (theo policy) |
| `UNTRUSTED_TSA` | có token nhưng bước 5 hoặc 6 thất bại | TSA giả/lạ → **giả** |
| `TIME_MISMATCH` | 5–6 đạt nhưng lệch > ngưỡng | giờ tự khai không nhất quán → **nghi ngờ** |
| `NotApplicable` | file không phải PDF | trình kiểm tra đang phát triển → **cảnh báo** (liên hệ admin), không chặn; đóng gói nếu xác nhận |

Nếu **không có** `signingTime`, `trustedTime` là chuẩn → `VALID_TRUSTED` khi bước 5–6 đạt.

## 6. Đầu ra mong muốn
Mở rộng `SignatureInfoDto` (hoặc `TimestampVerifyDto` mới) cho mỗi file:
```csharp
bool IsSigned; string? Signer; string? SigningTimeLocal;                    // tự khai
bool HasTrustedTimestamp; string? TimestampAuthority; string? TrustedTime;  // genTime
bool ChainToRootG2; bool TimeMatch; string Verdict; string? Reason;
```
Gộp theo hồ sơ; hiển thị cột "Dấu thời gian" trong bảng check ở FE.
**Chính sách (đã chốt):** **PDF** không `VALID_TRUSTED` → **chặn** đóng gói (`ErrorCodes.TimestampInvalid`); non-PDF →
`NotApplicable` → cảnh báo ("…liên hệ admin"), vẫn đóng gói nếu người dùng xác nhận.

## 7. Quyết định & bẫy
- **Bắt buộc:** giữ bước 5–6 (chữ ký TSA + chuỗi). Bỏ đi thì "khớp ⇒ thật" mất giá trị, qua mặt được ngay.
- **Quirk +7h của VGCA (đo thực, cốt lõi):** trong token VGCA thật, `genTime` bị lưu **lùi 7 giờ so với UTC thật**
  (giờ VN +7 dán nhãn `Z`). Trên 13 file mẫu, `signingTime − genTime ≈ 25200s − delay`; sau khi `genTime + 7h`,
  phần dư chính là **delay gọi TSA 2s–131s** (dấu thời gian ngay sau khi ký). So UTC thô với ngưỡng nhỏ **đánh trượt
  mọi file thật** — luôn +7h; lấy **crypto-validity + chain Root-G2 làm tín hiệu tin cậy chính** (độ lệch thời gian
  chỉ để chặn backdate thô).
- **Công nghệ:** dùng `System.Security.Cryptography.Pkcs` native (`.NET 8`, có `Rfc3161TimestampToken`) — zero-dep,
  đồng bộ `PdfSignatureReader`. Chỉ offline; OCSP/CRL revocation là tùy chọn/ngoài phạm vi.
- **PDF nhiều chữ ký:** verify từng người ký độc lập.
- Liên quan: `Sip.be.Application/Document/Implements/PdfSignatureReader.cs`, [chuan-dong-goi-sip-tt05.md](chuan-dong-goi-sip-tt05.md).
