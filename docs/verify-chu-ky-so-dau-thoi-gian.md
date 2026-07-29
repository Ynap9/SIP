# Kiểm tra dấu thời gian tin cậy của chữ ký số (TSA Ban Cơ yếu Chính phủ)

> Cách SIPPACK chứng minh thời điểm ký của 1 PDF là thật — gắn với dấu thời gian RFC 3161 do TSA của Ban Cơ yếu
> Chính phủ cấp — thay vì tin vào giờ máy tự khai.
> **Đã hiện thực** (BE ở `Sip.be.External`, FE cột "Dấu thời gian"; đã verify runtime: 13/13 file thật
> `ValidTrusted`). Đã phủ **toàn vẹn tài liệu**, **dấu thời gian** và **chuỗi chứng thư người ký** — MỌI chữ ký
> trong file đều được verify và TẤT CẢ phải đạt.
> Bản tiếng Anh (cho agents): [verify-signature-timestamp.md](verify-signature-timestamp.md).

## 1. Vấn đề
`signingTime` (PKCS#9 `1.2.840.113549.1.9.5`) là giờ **người ký tự khai**, chỉnh đồng hồ máy là giả được — và nó
nằm **cùng file** với token, nên **chỉ so 2 con số thì không chứng minh được gì**: kẻ gian đặt chúng bằng nhau là
xong. Giờ đáng tin chỉ đến từ token RFC 3161 có **chữ ký TSA + chuỗi về root đã ghim**.

## 2. Trust anchor (phải ghim — cả tính năng phụ thuộc vào đây)
Token dấu thời gian nhúng sẵn toàn bộ chuỗi TSA, nên xác minh được **hoàn toàn offline** khi đã ghim root.
**Hai nhánh, hai nhiệm vụ:** Root G2 neo chuỗi **TSA** (và người ký G2); Root G1 + Sub-CA G1 neo chuỗi **người ký**
— token production là G1, TSA luôn là G2. G1/G2 là **thế hệ** hạ tầng PKI, KHÔNG phải phân theo cơ quan/cá nhân.
Bộ cert, từng pin, path tải, và nguồn gốc mỗi pin nằm ở **[ca-trust-certificates.md](ca-trust-certificates.md)** —
doc chuẩn, KHÔNG nhân bản lại ở đây.
Windows cert store **không** đáng tin ở đây (máy test chưa cài root Chính phủ).

## 3. Đầu vào
Đường dẫn 1 PDF, hoặc thư mục `attachment/` của hồ sơ → kiểm mọi `.pdf` (không phải PDF → `N/A`); bộ cert ghim
(G1 + G2) đóng kèm app; `toleranceSeconds` (mặc định 300) — độ lệch tối đa giữa giờ tin cậy và giờ tự khai.

## 4. Các bước (mỗi PDF, đều offline)
> **MỌI** chữ ký trong file đều được verify và **TẤT CẢ** phải đạt; verdict cấp file = verdict **xấu nhất**. Khối
> không giải mã ra `SignedCms` thì bỏ qua — `/ByteRange` nằm trong content stream không phải chữ ký.

1. Quét `/ByteRange` + `/Contents` cho từng chữ ký (tái dùng logic `PdfSignatureReader`).
2. **Toàn vẹn tài liệu.** Nối 2 khoảng đã ký `[start1,start1+len1) + [start2,start2+len2)`, dựng
   `new SignedCms(new ContentInfo(content), detached: true)`, `Decode(der)`, rồi mỗi signer gọi
   `CheckSignature(verifySignatureOnly: true)` → ném lỗi ⇒ `InvalidSignature`. Sau đó chốt `/ByteRange` không nói
   dối: `start1 == 0`, khoảng hở chứa đúng `/Contents` của nó, và chữ ký **ngoài cùng** (lớn nhất `start2+len2`)
   phải chạm EOF. Đọc `signingTime` (giờ tự khai) nếu có.
3. Định vị token dấu thời gian:
   - **PAdES-T**: unsigned attribute OID `1.2.840.113549.1.9.16.2.14` trên SignerInfo, hoặc
   - **PAdES-LTV**: 1 chữ ký riêng `/Type /DocTimeStamp` (`/SubFilter /ETSI.RFC3161`), phần `/Contents` là token.
4. `Rfc3161TimestampToken.TryDecode(tokenBytes, out token, out _)`.
5. `token.VerifySignatureForSignerInfo(signerInfo, out tsaCert)` — kiểm chữ ký TSA, `messageImprint` bao đúng
   **chữ ký này**, và EKU TSA `id-kp-timeStamping` (`1.3.6.1.5.5.7.3.8`).
6. Dựng `X509Chain` cho `tsaCert` với **trust anchor tùy biến = chỉ Root G2 đã ghim** (cert của token bỏ vào
   ExtraStore, revocation offline), tại `VerificationTime = trustedTime` — **không** phải `DateTime.Now`; bắt buộc
   SHA-256 của root chuỗi == Root G2 đã ghim. Cert TSA hết hạn KHÔNG làm dấu thời gian cũ mất hiệu lực — hợp lệ
   *tại lúc đóng dấu* mới là điều cần (§7).
7. `trustedTime = token.TokenInfo.Timestamp` (genTime) — **UTC thô, KHÔNG cộng offset nào** (§7).
8. **Cổng chuỗi người ký.** Dựng `X509Chain` cho cert người ký với `VerificationTime = trustedTime` — **không** phải
   `DateTime.Now`. Đạt khi **một trong hai** nhánh chain về root đã ghim của nó: G1 (neo Root G1, `cp.crt` vào
   `ExtraStore`) **hoặc** G2 (neo Root G2). Không phải G1 thì xét G2; không thuộc cả hai → `UntrustedSigner`. Xét G1
   trước (production là G1). Bắt buộc neo tại giờ tin cậy: cert người ký thật hết hạn **25/09/2026**, dùng `Now` là
   từ chối oan mọi file cũ kể từ ngày đó.
9. `delta = signingTimeUtc − trustedTime`; nếu `|delta − 7h| ≤ 1h` thì trừ 7h (bên ký ghi nhầm giờ địa phương, §7 —
   ghi rõ trong `Reason`). Sau đó chỉ xét **THỨ TỰ**: `delta > TsaClockSkewGraceSeconds` ⇒ `TimeMismatch`, còn lại đạt.

## 5. Kết luận (verdict)
| Verdict | Điều kiện | Ý nghĩa |
|---|---|---|
| `VALID_TRUSTED` | có token, bước 5–6 đạt, thời gian trong ngưỡng | dấu thời gian Ban Cơ yếu thật |
| `InvalidSignature` | bước 2 thất bại — chữ ký ≠ nội dung, `/ByteRange` nói dối, hoặc có byte ngoài mọi vùng ký | tài liệu bị sửa sau khi ký → **giả** |
| `NO_TIMESTAMP` | không có token RFC 3161 | không chứng minh được → **nghi ngờ/giả** (theo policy) |
| `UNTRUSTED_TSA` | có token nhưng bước 5 hoặc 6 thất bại | TSA giả/lạ → **giả** |
| `UntrustedSigner` | bước 8 thất bại — cert người ký không chain về Root G1 lẫn Root G2 | Ban Cơ yếu không bảo chứng → **giả** |
| `TIME_MISMATCH` | 5–6 đạt nhưng lệch > ngưỡng | giờ tự khai không nhất quán → **nghi ngờ** |
| `NotApplicable` | file không phải PDF | trình kiểm tra đang phát triển → **cảnh báo** (liên hệ admin), không chặn; đóng gói nếu xác nhận |

Nếu **không có** `signingTime`, `trustedTime` là chuẩn → `VALID_TRUSTED` khi bước 5–6 đạt.
Verdict cấp file = verdict **xấu nhất** trong các chữ ký; `TimestampVerifyDto.Signatures[]` giữ từng chữ ký
(`SignedBy`, `Verdict`, `Reason`, `TrustedTime`) — file nhiều chữ ký thì verdict cấp file không đủ để chẩn đoán.

## 6. Đầu ra
`TimestampVerifyDto` mỗi file (`Verdict`, `HasTimestamp`, `TimestampAuthority`, `SignedBy`, `TrustedTime`,
`SigningTime`, `Reason`, `Signatures[]`, kèm `IsValid`/`Blocks` tính sẵn); hiển thị ở cột "Dấu thời gian" của FE.
`Blocks` là whitelist (`!= ValidTrusted && != NotApplicable`) nên thêm verdict chặn mới không phải sửa service.
**Chính sách (đã chốt):** **PDF** không `VALID_TRUSTED` → **chặn** đóng gói (`ErrorCodes.TimestampInvalid`, hoặc
`SignatureInvalid` khi tài liệu bị sửa); non-PDF → `NotApplicable` → cảnh báo ("…liên hệ admin"), vẫn đóng gói nếu
người dùng xác nhận.

## 7. Quyết định & bẫy
- **Bắt buộc:** giữ bước 5–6 (chữ ký TSA + chuỗi). Bỏ đi thì "khớp ⇒ thật" mất giá trị, qua mặt được ngay.
- **`ValidTrusted` nghĩa là gì.** Mọi chữ ký còn nguyên vẹn kể từ lúc ký (bước 2), bằng chứng thư được Ban Cơ yếu
  bảo chứng (bước 8), tại thời điểm TSA Ban Cơ yếu chứng minh (bước 5–7), và không byte nào nằm ngoài vùng ký.
- **Cert luôn xét tại `trustedTime`, KHÔNG bao giờ `Now`** (cả TSA lẫn người ký; chúng chết 2034 và 25/09/2026 —
  `Now` biến mỗi mốc thành ngày mọi file cũ bị từ chối đồng loạt). Đo thực: `VerificationTime` có được tôn trọng và
  `Kind=Utc` được hiểu đúng là UTC — tại `NotBefore`, `+1h` dựng được còn `−1h` thì không; lệch 7h là đảo kết quả.
- **KHÔNG được bắt MỌI chữ ký chạm EOF (đo thực).** Ký thêm chính là incremental update, nên chữ ký cũ hợp lệ mà vẫn
  kết thúc ở revision của nó; bắt tất cả là từ chối oan mọi file nhiều chữ ký. Chỉ bắt chữ ký **ngoài cùng** — vẫn
  chặn được phần nối thêm và vẫn bảo đảm mọi byte có ít nhất 1 chữ ký phủ.
- **Verdict xấu nhất thắng**, xếp hạng tường minh (`InvalidSignature` > `UntrustedSigner` > `UntrustedTsa` >
  `NoTimestamp` > `TimeMismatch` > `ValidTrusted`). "Khối đầu tiên thắng" là để kẻ tấn công tự chọn kết luận bằng
  thứ tự byte.
- **Chain phải `Build()` được, không phải chỉ "có CA đã ghim trong chuỗi".** `ChainElements` là đường chain *định
  dựng*, gồm cả phần tử có chữ ký sai; đọc nó mà bỏ qua kết quả `Build` là nhận cert giả. Xem
  [bug/signer-chain-pinned-ca-presence-is-not-enough.md](bug/signer-chain-pinned-ca-presence-is-not-enough.md).
- **Lệch 7h là của BÊN KÝ, không phải của TSA (đo thực).** `genTime` là UTC chuẩn; một số công cụ ghi **giờ địa
  phương** vào PKCS#9 `signingTime` nhưng dán `Kind=Utc`. ⇒ **Không bao giờ cộng offset vào `genTime`**: sai phía,
  và kéo lệch luôn mốc dựng chain. Các file đó sai RFC 5652, chấp nhận chúng là **tương thích ngược**, ghi rõ trong
  `Reason`. Xem [bug/genTime-offset-compensates-on-the-wrong-side.md](bug/genTime-offset-compensates-on-the-wrong-side.md).
- **Xét THỨ TỰ, không xét KHOẢNG CÁCH (đo thực).** TSA load-balance hai máy chủ có đồng hồ lệch nhau **~14 phút**,
  cả hai chạy nhanh, độ trôi giãn dần (≈0/128s tháng 4 → 237/1087s tháng 7) — mọi ngưỡng khoảng cách đều đánh trượt
  file thật một cách ngẫu nhiên. QCVN 138:2025/BKHCN § 2.2 → ETSI EN 319 421 đòi độ chính xác 1 giây; `vSignPDF`
  của chính VGCA **không có** phép kiểm lệch giờ. Chỉ `signingTime ≤ genTime` là vững — đồng hồ TSA chạy nhanh chỉ
  nới khoảng cách theo chiều an toàn.
  [bug/tsa-load-balanced-clocks-drift-14-minutes.md](bug/tsa-load-balanced-clocks-drift-14-minutes.md)
- Zero-dep: `System.Security.Cryptography.Pkcs`, chỉ offline, không OCSP/CRL.
- Liên quan: `Sip.be.External/Documents/{PdfSignatureReader,SignatureVerifier}.cs`, [ca-trust-certificates.md](ca-trust-certificates.md).
