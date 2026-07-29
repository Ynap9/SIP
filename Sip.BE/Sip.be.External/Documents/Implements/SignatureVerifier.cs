using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Verify DẤU THỜI GIAN tin cậy (RFC 3161) của chữ ký số PDF so với TSA Ban Cơ yếu Chính phủ.
    /// Thuần, FAIL-CLOSED: không chứng minh được thời gian tin cậy -> KHÔNG hợp lệ (chặn).
    /// File không phải PDF -> NotApplicable (cảnh báo, không chặn). Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public class SignatureVerifier : ISignatureVerifier
    {
        // /ByteRange [start1 len1 start2 len2] — khối chữ ký nằm trong khoảng hở [start1+len1, start2).
        private static readonly Regex ByteRangeRegex =
            new(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.Compiled);

        // Nạp + kiểm pin Root G2 1 lần (fail-closed nếu thiếu/sai pin).
        private static readonly Lazy<X509Certificate2?> RootG2 =
            new(() => LoadPinnedCert(SignatureConstants.RootG2FileName, SignatureConstants.RootG2Sha256));

        // Nạp + kiểm pin các CA TRUNG GIAN dưới Root G2 (Đảng/Nhà nước G2) 1 lần; bỏ qua cert thiếu/sai pin.
        // Đưa vào ExtraStore khi dựng chain để leaf về được Root G2 kể cả khi token/PDF không nhúng sẵn CA trung gian.
        private static readonly Lazy<X509Certificate2Collection> IntermediateCAs = new(LoadIntermediateCAs);

        // Nạp + kiểm pin nhánh G1 — dùng cho chain của NGƯỜI KÝ (fail-closed nếu thiếu/sai pin).
        // Root G1 = trust anchor (self-signed); Sub-CA G1 (cp.crt) vào ExtraStore vì CMS trong PDF chỉ nhúng leaf.
        private static readonly Lazy<X509Certificate2?> RootG1 =
            new(() => LoadPinnedCert(SignatureConstants.RootG1FileName, SignatureConstants.RootG1Sha256));

        private static readonly Lazy<X509Certificate2?> SubCaG1 =
            new(() => LoadPinnedCert(SignatureConstants.SubCaG1FileName, SignatureConstants.SubCaG1Sha256));

        /// <summary>
        /// Verify 1 file. Non-PDF -> NotApplicable. PDF không có/không hợp lệ dấu thời gian -> verdict chặn.
        /// MỌI chữ ký trong file đều được verify và TẤT CẢ phải đạt; verdict cấp file = verdict XẤU NHẤT.
        /// Mọi lỗi đọc được nuốt và fail-closed (không ném ra ngoài).
        /// </summary>
        public TimestampVerifyDto Verify(string filePath)
        {
            var dto = new TimestampVerifyDto();
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    dto.Verdict = TimestampVerdict.NotApplicable;
                    dto.Reason = "Đang phát triển trình kiểm tra ký số cho file không phải PDF, vui lòng liên hệ admin.";
                    return dto;
                }

                var bytes = File.ReadAllBytes(filePath);
                // Latin1: mỗi byte = 1 ký tự -> chỉ số ký tự == chỉ số byte, tiện định vị /Contents theo ByteRange.
                var latin1 = Encoding.Latin1.GetString(bytes);

                // Lượt 1: gom các khối /ByteRange. ParseBlock trả null CHỈ khi đó không phải chữ ký (regex quét
                // byte thô nên chuỗi "/ByteRange" trong content stream cũng khớp) -> đếm lại để caller biết ta đã
                // bỏ qua thứ gì trông giống chữ ký. Khối ra DER mà CMS hỏng thì KHÔNG bị bỏ qua nữa.
                var blocks = new List<SignatureBlock>();
                var skipped = 0;
                foreach (Match m in ByteRangeRegex.Matches(latin1))
                {
                    var block = ParseBlock(latin1, bytes, m);
                    if (block != null) blocks.Add(block);
                    else skipped++;
                }

                if (blocks.Count == 0)
                {
                    dto.Verdict = TimestampVerdict.NoTimestamp;
                    dto.HasTimestamp = false;
                    dto.SkippedBlocks = skipped;
                    dto.Reason = "PDF không có chữ ký số/dấu thời gian tin cậy.";
                    return dto;
                }

                // Chữ ký NGOÀI CÙNG = khối phủ xa nhất, tính trên các khối ĐỌC ĐƯỢC: khối hỏng đằng nào cũng ra
                // InvalidSignature (rank cao nhất), đưa SignedEnd của nó vào đây chỉ làm nới lỏng check EOF.
                var decodable = blocks.Where(b => b.Cms != null).ToList();
                var outermostEnd = decodable.Count > 0 ? decodable.Max(b => b.SignedEnd) : 0L;

                // Lượt 2: verify TỪNG chữ ký của TỪNG khối (không chỉ SignerInfos[0]); khối hỏng ra thẳng verdict.
                // Giữ kèm SignedEnd của khối: ký nối bản luôn ghi thêm vào CUỐI file, nên khối phủ xa nhất
                // chính là chữ ký áp SAU CÙNG. Không có số này thì không cách nào biết chữ ký nào mới nhất.
                var verified = new List<(TimestampVerifyDto Result, long SignedEnd)>();
                foreach (var block in blocks)
                {
                    if (block.Cms == null)
                    {
                        verified.Add((new TimestampVerifyDto
                        {
                            Verdict = TimestampVerdict.InvalidSignature,
                            Reason = block.CorruptReason
                        }, block.SignedEnd));
                        continue;
                    }
                    foreach (SignerInfo signer in block.Cms.SignerInfos)
                    {
                        verified.Add((VerifySigner(signer, block, bytes.LongLength, outermostEnd), block.SignedEnd));
                    }
                }

                // Xếp theo THỨ TỰ KÝ để danh sách hiển thị ra đúng "chữ ký 1, 2, 3..." và cái cuối là mới nhất.
                verified = verified.OrderBy(v => v.SignedEnd).ToList();
                var results = verified.Select(v => v.Result).ToList();

                if (results.Count == 0)
                {
                    dto.Verdict = TimestampVerdict.NoTimestamp;
                    dto.HasTimestamp = false;
                    dto.SkippedBlocks = skipped;
                    dto.Reason = "PDF không có chữ ký số/dấu thời gian tin cậy.";
                    return dto;
                }

                // Verdict XẤU NHẤT thắng. "Khối đầu tiên thắng" là để kẻ tấn công tự chọn kết luận bằng cách
                // sắp xếp thứ tự byte -> luôn xếp hạng tường minh, fail-closed.
                var worst = results.OrderByDescending(r => Rank(r.Verdict)).First();
                worst.SkippedBlocks = skipped;
                worst.SignatureBlockCount = blocks.Count;
                worst.ValidSignatureCount = results.Count(r => r.Verdict == TimestampVerdict.ValidTrusted);
                worst.Signatures = results.Select(r => new SignatureDetailDto
                {
                    SignedBy = r.SignedBy,
                    Verdict = r.Verdict,
                    Reason = r.Reason,
                    TrustedTime = r.TrustedTime,
                    SigningTime = r.SigningTime
                }).ToList();

                // VERDICT và THỜI GIAN là hai câu hỏi khác nhau, phải trả lời bằng hai chữ ký khác nhau:
                // - verdict cấp file lấy của chữ ký XẤU NHẤT (một chữ ký hỏng là cả file hỏng);
                // - giờ cấp file lấy của chữ ký MỚI NHẤT, vì đó là lần ký vừa xảy ra.
                // Gộp làm một là ra đúng bug cũ: file ký chồng lần 2 vẫn hiện giờ của chữ ký lần 1, do mọi chữ
                // ký đều hợp lệ nên phép xếp hạng ổn định trả về khối đầu tiên.
                var newest = results[^1];
                worst.SigningTime = newest.SigningTime;
                worst.TrustedTime = newest.TrustedTime;
                return worst;
            }
            catch (Exception ex)
            {
                dto.Verdict = TimestampVerdict.NoTimestamp; // fail-closed
                dto.ReadFailed = true; // không đọc nổi file -> KHÔNG được suy ra "file chưa ký" từ Signatures rỗng
                dto.Reason = $"Không đọc được chữ ký/dấu thời gian: {ex.Message}";
                return dto;
            }
        }

        /// <summary>
        /// Đổi một mốc UTC sang GIỜ VIỆT NAM để hiển thị.
        ///
        /// Quy bằng múi giờ VN tường minh, KHÔNG dùng ToLocalTime(): máy người dùng đặt múi giờ khác là mọi
        /// mốc thời gian lệch theo mà không có gì báo. Trả về chuỗi giờ TRẦN, không kèm phần bù: mọi giờ ra
        /// khỏi verifier đều đã là giờ VN nên ghi thêm "+07:00" vào từng ô chỉ làm bảng rối.
        /// </summary>
        private static string FormatVietnam(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(utc, DateTimeConstants.VietnamTimeZone)
                .ToString("dd/MM/yyyy HH:mm:ss");

        /// <summary>Xếp hạng mức độ XẤU của verdict (cao = xấu hơn) để chọn verdict cấp file.</summary>
        private static int Rank(TimestampVerdict v) => v switch
        {
            TimestampVerdict.InvalidSignature => 6,
            TimestampVerdict.UntrustedSigner => 5,
            TimestampVerdict.UntrustedTsa => 4,
            TimestampVerdict.NoTimestamp => 3,
            TimestampVerdict.TimeMismatch => 2,
            TimestampVerdict.ValidTrusted => 1,
            TimestampVerdict.NotApplicable => 0,
            _ => 6 // verdict lạ -> coi là xấu nhất (fail-closed)
        };

        /// <summary>1 khối chữ ký: CMS đã gắn ĐÚNG nội dung 2 khoảng /ByteRange ký, + các mốc để kiểm ByteRange.</summary>
        private sealed class SignatureBlock
        {
            /// <summary>null khi khối HỎNG (không decode được CMS) — khi đó <see cref="CorruptReason"/> có giá trị.</summary>
            public SignedCms? Cms { get; init; }
            /// <summary>Khác null = khối này CÓ chữ ký nhưng đã hỏng/bị phá -> phải ra InvalidSignature, không được bỏ qua.</summary>
            public string? CorruptReason { get; init; }
            /// <summary>start2 + len2 — byte cuối cùng mà chữ ký này phủ.</summary>
            public required long SignedEnd { get; init; }
            /// <summary>ByteRange không nói dối: bắt đầu từ 0 và khoảng hở chứa ĐÚNG /Contents của chính nó.</summary>
            public required bool RangeSane { get; init; }
        }

        /// <summary>
        /// Đọc 1 khối /ByteRange -> SignedCms DETACHED gắn đúng nội dung đã ký (nối 2 khoảng).
        /// Trả null CHỈ khi đây thật sự KHÔNG PHẢI chữ ký (ByteRange vô lý / khoảng hở không chứa hex hợp lệ):
        /// regex quét byte thô nên chuỗi "/ByteRange" trong content stream cũng khớp -> bỏ qua là đúng.
        /// Ra được DER nhưng CMS không decode được -> KHÔNG bỏ qua: đó là chữ ký hỏng/bị phá, trả về khối có
        /// CorruptReason để worst-wins nhặt thành InvalidSignature (xem bug verifier-skips-undecodable-signature).
        /// </summary>
        private static SignatureBlock? ParseBlock(string latin1, byte[] bytes, Match m)
        {
            if (!long.TryParse(m.Groups[1].Value, out var start1) || !long.TryParse(m.Groups[2].Value, out var len1) ||
                !long.TryParse(m.Groups[3].Value, out var start2) || !long.TryParse(m.Groups[4].Value, out var len2))
                return null;
            if (start1 < 0 || len1 < 0 || start2 < 0 || len2 < 0) return null;

            var gapStart = start1 + len1;
            var signedEnd = start2 + len2;
            // Khoảng phải nằm trong file và khoảng hở phải đi trước start2.
            if (gapStart > bytes.LongLength || signedEnd > bytes.LongLength || gapStart >= start2) return null;

            var der = ExtractContents(latin1, m);
            if (der == null) return null;

            // Nội dung THỰC SỰ được ký = nối 2 khoảng; khoảng hở ở giữa là /Contents nên không tự ký chính nó.
            var content = new byte[len1 + len2];
            Array.Copy(bytes, start1, content, 0, len1);
            Array.Copy(bytes, start2, content, len1, len2);

            var rangeSane = start1 == 0 && GapHoldsOnlyContents(latin1, gapStart, start2);

            SignedCms cms;
            try
            {
                cms = new SignedCms(new ContentInfo(content), detached: true);
                cms.Decode(der);
            }
            catch (Exception ex)
            {
                // Có /Contents ra được DER mà vẫn không decode nổi -> chữ ký đã bị phá (vd: file bị ghi đè sau khi
                // ký). Nuốt im lặng ở đây chính là bug cũ: chữ ký bị hủy mà file vẫn ra verdict nhẹ.
                return new SignatureBlock
                {
                    CorruptReason = $"Khối chữ ký trong file đã hỏng, không giải mã được (CMS lỗi: {ex.Message}).",
                    SignedEnd = signedEnd,
                    RangeSane = rangeSane
                };
            }
            // Decode được mà không có signer nào là bất thường (chữ ký rỗng), không phải "không phải chữ ký".
            if (cms.SignerInfos.Count == 0)
            {
                return new SignatureBlock
                {
                    CorruptReason = "Khối chữ ký trong file không chứa người ký nào (SignerInfos rỗng).",
                    SignedEnd = signedEnd,
                    RangeSane = rangeSane
                };
            }

            return new SignatureBlock
            {
                Cms = cms,
                SignedEnd = signedEnd,
                RangeSane = rangeSane
            };
        }

        /// <summary>
        /// Khoảng hở [gapStart, gapEnd) phải chứa ĐÚNG 1 chuỗi hex "&lt;...&gt;" và không gì khác (ngoài khoảng trắng).
        /// Nếu không, kẻ tấn công đã giấu được byte trong khoảng hở mà chữ ký không phủ.
        /// </summary>
        private static bool GapHoldsOnlyContents(string latin1, long gapStart, long gapEnd)
        {
            var gap = latin1.Substring((int)gapStart, (int)(gapEnd - gapStart));
            var lt = gap.IndexOf('<');
            var gt = gap.LastIndexOf('>');
            if (lt < 0 || gt < 0 || gt < lt) return false;
            // Ngoài cặp <...> chỉ được phép là khoảng trắng.
            if (gap[..lt].Trim().Length != 0 || gap[(gt + 1)..].Trim().Length != 0) return false;
            // Bên trong không được có thêm dấu ngoặc (tránh giấu object khác).
            return gap.IndexOf('<', lt + 1) < 0 && gap.IndexOf('>', lt + 1) == gt;
        }

        /// <summary>
        /// Verify 1 SignerInfo: TOÀN VẸN tài liệu trước, rồi mới tới dấu thời gian + danh tính người ký.
        /// Luôn trả verdict (không bao giờ null) — mọi chữ ký đều phải được tính vào kết luận cấp file.
        /// </summary>
        private static TimestampVerifyDto VerifySigner(SignerInfo signer, SignatureBlock block,
            long fileLength, long outermostEnd)
        {
            var dto = new TimestampVerifyDto
            {
                SignedBy = signer.Certificate?.GetNameInfo(X509NameType.SimpleName, false)
            };

            // (1) TOÀN VẸN — chữ ký phải khớp CHÍNH nội dung 2 khoảng /ByteRange đã ký (CMS detached).
            // verifySignatureOnly: chain được kiểm riêng ở dưới tại trustedUtc, không phải DateTime.Now.
            try { signer.CheckSignature(verifySignatureOnly: true); }
            catch
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "Nội dung tài liệu đã bị sửa sau khi ký (chữ ký không khớp nội dung).";
                return dto;
            }

            // (2) /ByteRange do kẻ tấn công viết ra -> không được tin. Chữ ký phải phủ từ byte 0 và khoảng hở
            // phải chứa đúng /Contents của nó, nếu không hắn khai một khoảng bé rồi bịa phần còn lại.
            if (!block.RangeSane)
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "Vùng ký (/ByteRange) không hợp lệ — không phủ từ đầu file hoặc có byte lạ trong khoảng hở.";
                return dto;
            }

            // (3) Chữ ký NGOÀI CÙNG phải chạm EOF -> không còn byte nào nằm ngoài mọi vùng ký.
            // KHÔNG bắt mọi chữ ký chạm EOF: ký thêm = incremental update, nên chữ ký cũ HỢP LỆ mà vẫn kết thúc
            // ở revision của chính nó — bắt tất cả là từ chối oan mọi file nhiều chữ ký.
            if (block.SignedEnd == outermostEnd && outermostEnd != fileLength)
            {
                dto.Verdict = TimestampVerdict.InvalidSignature;
                dto.Reason = "File có byte nằm ngoài mọi vùng ký (nội dung được nối thêm sau khi ký).";
                return dto;
            }

            // signingTime tự khai (nếu có)
            DateTime? signingUtc = null;
            foreach (var a in signer.SignedAttributes)
            {
                if (a.Oid?.Value == SignatureConstants.OidSigningTime && a.Values.Count > 0)
                {
                    try { signingUtc = new Pkcs9SigningTime(a.Values[0].RawData).SigningTime.ToUniversalTime(); }
                    catch { /* bỏ qua nếu không parse được */ }
                }
            }

            // token RFC 3161 trong unsigned attributes (PAdES-T)
            byte[]? tokenBytes = null;
            foreach (var a in signer.UnsignedAttributes)
            {
                if (a.Oid?.Value == SignatureConstants.OidSignatureTimeStampToken && a.Values.Count > 0)
                {
                    tokenBytes = a.Values[0].RawData;
                    break;
                }
            }

            dto.SigningTime = signingUtc == null ? null : FormatVietnam(signingUtc.Value);
            dto.HasTimestamp = tokenBytes != null;

            var cmsCerts = block.Cms.Certificates;

            if (tokenBytes == null)
            {
                dto.Verdict = TimestampVerdict.NoTimestamp;
                dto.Reason = "Chữ ký không có dấu thời gian tin cậy (RFC 3161).";
                return dto;
            }

            if (!Rfc3161TimestampToken.TryDecode(tokenBytes, out var token, out _) || token == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Không giải mã được token dấu thời gian.";
                return dto;
            }

            // Chữ ký TSA hợp lệ + bind ĐÚNG chữ ký này + EKU timeStamping.
            if (!token.VerifySignatureForSignerInfo(signer, out var tsaCert) || tsaCert == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Chữ ký của TSA không hợp lệ với chữ ký này.";
                return dto;
            }
            dto.TimestampAuthority = tsaCert.GetNameInfo(X509NameType.SimpleName, false);

            // trustedTime = genTime THÔ của TSA, KHÔNG cộng offset nào: genTime do TSA ký nên là UTC chuẩn.
            // Lệch múi giờ hay gặp nằm ở signingTime của BÊN KÝ (xem phần đối chiếu bên dưới) — cộng bù vào genTime
            // là chữa sai phía: vừa che lỗi của bên ký, vừa kéo lệch luôn mốc dựng chain ở dưới.
            // Tính TRƯỚC mọi lần dựng chain: cert phải được xét tại THỜI ĐIỂM ĐÓNG DẤU, không phải DateTime.Now.
            // Kind = Utc (DateTimeOffset.UtcDateTime) -> instant không nhập nhằng; đừng để .NET tự suy Kind.
            var trustedUtc = token.TokenInfo.Timestamp.UtcDateTime;
            dto.TrustedTime = FormatVietnam(trustedUtc);

            // Chain TSA -> Root G2 đã ghim (offline, custom trust anchor), xét tại trustedUtc.
            // Cert TSA hết hạn KHÔNG làm dấu thời gian cũ mất hiệu lực: nó hợp lệ tại lúc đóng dấu là đủ.
            // Dùng Now ở đây = mọi file sẽ đồng loạt bị từ chối oan vào ngày cert TSA hết hạn.
            var root = RootG2.Value;
            if (root == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Không nạp được trust anchor Root G2 (kiểm tra file rootcag2.crt / pin).";
                return dto;
            }
            if (!ChainsToRoot(tsaCert, token.AsSignedCms().Certificates, root, trustedUtc))
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Dấu thời gian không thuộc TSA Ban Cơ yếu (chain không về Root G2).";
                return dto;
            }

            // Chain cert NGƯỜI KÝ, cũng xét tại trustedUtc (cert người ký thật hết hạn 25/09/2026 -> dùng Now là
            // từ chối oan mọi file cũ kể từ ngày đó).
            if (signer.Certificate == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedSigner;
                dto.Reason = "Chữ ký không nhúng chứng thư số của người ký.";
                return dto;
            }
            if (!IsTrustedSigner(signer.Certificate, cmsCerts, trustedUtc, root))
            {
                dto.Verdict = TimestampVerdict.UntrustedSigner;
                // Nhánh G1 thiếu material -> nói rõ, tránh đổ lỗi nhầm cho cert người ký khi thực ra là lỗi cài đặt.
                dto.Reason = RootG1.Value != null && SubCaG1.Value != null
                    ? "Chứng thư số người ký không thuộc CA Ban Cơ yếu (chain không về Root G1 lẫn Root G2)."
                    : "Chứng thư số người ký không thuộc CA Ban Cơ yếu (nhánh G1 không kiểm được do thiếu/sai pin rootca.crt hoặc cp.crt; chain cũng không về Root G2).";
                return dto;
            }

            // Không có giờ tự khai -> trustedTime là chuẩn -> hợp lệ.
            if (signingUtc == null)
            {
                dto.Verdict = TimestampVerdict.ValidTrusted;
                return dto;
            }

            // signingTime là giờ TỰ KHAI, KHÔNG được TSA ký. Ta KHÔNG so khoảng cách hai mốc nữa mà chỉ xét THỨ TỰ.
            // Lý do: đồng hồ của TSA không đủ chính xác để làm mốc đo khoảng cách (đo thực: các máy chủ sau load
            // balancer lệch nhau hàng chục phút và độ trôi tăng dần theo thời gian). Mọi ngưỡng khoảng cách vì thế
            // đều đánh trượt oan file thật, và ngưỡng "chạy được lâu nay" chỉ đang che một lỗi đang lớn dần.
            // Thứ tự thì miễn nhiễm: trình tự ký hợp lệ luôn là tính chữ ký -> gửi TSA -> TSA đóng dấu, nên
            // signingTime PHẢI đứng trước genTime. TSA chạy nhanh chỉ nới khoảng cách theo chiều an toàn.
            var delta = (signingUtc.Value - trustedUtc).TotalSeconds;

            // CHUẨN HÓA quirk TRƯỚC khi xét thứ tự: công cụ ký ghi giờ ĐỊA PHƯƠNG vào signingTime nhưng đánh dấu
            // Kind=Utc -> đọc ra cao hơn genTime đúng bằng offset múi giờ. Không trừ đi thì luật thứ tự sẽ đánh
            // trượt toàn bộ file cũ dạng này. Sai chuẩn PKCS#9 (ASN.1 UTCTime = UTC tuyệt đối) nhưng là lỗi ghi
            // nhãn của bên ký, không phải dấu hiệu giả mạo -> chấp nhận theo diện TƯƠNG THÍCH NGƯỢC và NÓI RA
            // trong Reason; im lặng là mất luôn khả năng phát hiện công cụ ký ghi sai giờ.
            var quirkOffset = SignatureConstants.SignerLocalTimeQuirkHours * 3600;
            var isLocalTimeQuirk = Math.Abs(delta - quirkOffset) <= SignatureConstants.SignerLocalTimeQuirkWindowSeconds;
            if (isLocalTimeQuirk)
            {
                delta -= quirkOffset;
                dto.Reason = $"Giờ ký tự khai ghi theo giờ địa phương (UTC+{SignatureConstants.SignerLocalTimeQuirkHours}) " +
                             $"nhưng đánh dấu UTC — sai chuẩn PKCS#9, chấp nhận theo diện tương thích ngược.";

                // Giờ HIỂN THỊ phải dùng luôn mốc đã chuẩn hóa. Chuỗi đặt ở trên coi signingTime là UTC rồi
                // cộng 7 tiếng, mà giá trị đó vốn ĐÃ là giờ VN -> cộng thêm lần nữa thành muộn hơn 7 tiếng và
                // đứng SAU cả dấu thời gian của chính nó, nhìn như ký xong mới đi đóng dấu.
                // Trừ đi phần bù rồi format lại = đúng con số bên ký ghi trong file (vd 11:43:06).
                dto.SigningTime = FormatVietnam(
                    signingUtc.Value.AddHours(-SignatureConstants.SignerLocalTimeQuirkHours));
            }

            // Khai giờ ký SAU thời điểm TSA đóng dấu là bất khả thi trong trình tự ký hợp lệ -> dấu hiệu khai gian
            // (điển hình: ký giả chèn vào file đã có dấu thời gian trước đó). Biên nhỏ chỉ để phòng đồng hồ TSA
            // chạy chậm nhẹ + signingTime mã hóa theo giây.
            if (delta > SignatureConstants.TsaClockSkewGraceSeconds)
            {
                dto.Verdict = TimestampVerdict.TimeMismatch;
                dto.Reason = $"Giờ ký tự khai đứng SAU thời điểm TSA đóng dấu {Math.Round(delta)}s — " +
                             $"không thể xảy ra trong trình tự ký hợp lệ.";
                return dto;
            }

            dto.Verdict = TimestampVerdict.ValidTrusted;
            return dto;
        }

        /// <summary>
        /// Dựng chain với trust anchor tùy biến = CHỈ <paramref name="root"/> đã ghim (Root G2 cho TSA/nhánh G2,
        /// Root G1 cho nhánh người ký G1); không tin bất cứ gì trong store của Windows. Revocation offline.
        /// <paramref name="verificationTime"/> BẮT BUỘC (luôn là trustedUtc): cert phải được xét tại thời điểm ký,
        /// không phải DateTime.Now. Để optional là bẫy — quên truyền = âm thầm quay về Now = từ chối oan file cũ
        /// ngay khi cert hết hạn.
        /// </summary>
        private static bool ChainsToRoot(X509Certificate2 leaf, X509Certificate2Collection extra, X509Certificate2 root,
            DateTime verificationTime)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ExtraStore.AddRange(extra);
            chain.ChainPolicy.VerificationTime = verificationTime;
            // CA trung gian đã ghim (Đảng/Nhà nước G2) — giúp dựng chain khi token/PDF thiếu CA trung gian.
            chain.ChainPolicy.ExtraStore.AddRange(IntermediateCAs.Value);
            if (!chain.Build(leaf)) return false;
            // CustomRootTrust đã bảo đảm kết thúc ở root đã ghim; kiểm lại thumbprint cho chắc.
            var top = chain.ChainElements[^1].Certificate;
            return string.Equals(top.Thumbprint, root.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Bản dùng cho luồng KÝ SỐ: kiểm cert người ký có thuộc CA Ban Cơ yếu không, tại thời điểm chỉ định.
        /// Mở ra internal để StoreSigningKey (cùng assembly) kiểm TRƯỚC khi ký cả lô mà không phải nhân bản
        /// code ghim CA — ký xong mới phát hiện cert không tin cậy là hỏng cả lô.
        /// Không có CMS nên ExtraStore rỗng; cert người ký thật vẫn nối được qua cp.crt/cpg2 đã ghim sẵn.
        /// </summary>
        internal static bool IsTrustedSignerCert(X509Certificate2 signerCert, DateTime verificationTime)
        {
            var root = RootG2.Value;
            if (root == null) return false;
            return IsTrustedSigner(signerCert, new X509Certificate2Collection(), verificationTime, root);
        }

        /// <summary>
        /// Cert NGƯỜI KÝ có thuộc CA Ban Cơ yếu không, xét tại thời điểm <paramref name="trustedUtc"/>.
        /// HAI trường hợp, chỉ cần đạt 1 (KHÔNG phải đạt cả hai): không thuộc G1 thì xét G2 và ngược lại;
        /// không thuộc cả hai mới là lỗi (-> UntrustedSigner). Mỗi nhánh neo tại Root self-signed đã ghim của nó:
        ///  - G1: chain về Root G1 đã ghim (cp.crt = Sub-CA G1 đưa vào ExtraStore vì CMS chỉ nhúng leaf);
        ///  - G2: chain về Root G2 đã ghim (giống hệt đường TSA; cpg2/dcscag2 nằm sẵn ExtraStore).
        /// Xét G1 TRƯỚC vì token production hiện là G1 (đo thực: 13/13 file thật đều G1) -> đỡ dựng chain G2 thừa.
        /// Thứ tự KHÔNG đổi kết luận: hai nhánh độc lập, cert không thể thuộc cả hai.
        /// LƯU Ý: G1/G2 là THẾ HỆ hạ tầng PKI (G1 cấp 2010-2030, G2 cấp 2018-2048), KHÔNG phải phân theo
        /// cơ quan/cá nhân — cả hai thế hệ đều phục vụ cơ quan và cấp cert cho cả tổ chức lẫn cá nhân trong đó.
        /// </summary>
        private static bool IsTrustedSigner(X509Certificate2 signerCert, X509Certificate2Collection cmsCerts,
            DateTime trustedUtc, X509Certificate2 rootG2)
        {
            // G1: neo Root G1; cp.crt phải nằm ExtraStore để nối leaf -> Sub-CA G1 -> Root G1.
            var rootG1 = RootG1.Value;
            var subCaG1 = SubCaG1.Value;
            if (rootG1 != null && subCaG1 != null)
            {
                var extraG1 = new X509Certificate2Collection();
                extraG1.AddRange(cmsCerts);
                extraG1.Add(subCaG1);
                if (ChainsToRoot(signerCert, extraG1, rootG1, trustedUtc)) return true;
            }

            // G2: cpg2/dcscag2 đã nằm sẵn trong ExtraStore của ChainsToRoot.
            return ChainsToRoot(signerCert, cmsCerts, rootG2, trustedUtc);
        }

        /// <summary>Trích khối DER (/Contents) của 1 chữ ký từ ByteRange.</summary>
        private static byte[]? ExtractContents(string latin1, Match m)
        {
            var gapStart = (int)(long.Parse(m.Groups[1].Value) + long.Parse(m.Groups[2].Value));
            var gapEnd = (int)long.Parse(m.Groups[3].Value);
            if (gapStart < 0 || gapEnd > latin1.Length || gapStart >= gapEnd) return null;
            var lt = latin1.IndexOf('<', gapStart);
            if (lt < 0 || lt >= gapEnd) return null;
            var gt = latin1.IndexOf('>', lt + 1);
            if (gt < 0 || gt > gapEnd) return null;
            var hex = Regex.Replace(latin1.Substring(lt + 1, gt - lt - 1), "[^0-9A-Fa-f]", string.Empty);
            if (hex.Length % 2 != 0) hex = hex[..^1];
            if (hex.Length < 2) return null;
            try { return Convert.FromHexString(hex); } catch { return null; }
        }

        /// <summary>Nạp các CA trung gian đã ghim (Đảng/Nhà nước G2) cạnh app; bỏ qua cert thiếu/sai pin.</summary>
        private static X509Certificate2Collection LoadIntermediateCAs()
        {
            var col = new X509Certificate2Collection();
            foreach (var (fileName, pin) in SignatureConstants.IntermediateCaPins)
            {
                var cert = LoadPinnedCert(fileName, pin);
                if (cert != null) col.Add(cert);
            }
            return col;
        }

        /// <summary>Nạp 1 cert từ file (PEM hoặc DER) rồi đối chiếu SHA-256 (DER) với pin; null nếu thiếu/sai.</summary>
        private static X509Certificate2? LoadPinnedCert(string fileName, string expectedSha256)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, fileName);
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                // CreateFromPem = PEM CHỈ chứa cert (CreateFromPemFile là cho cert+private key -> ném lỗi với PEM cert-only).
                var cert = text.Contains("BEGIN CERTIFICATE")
                    ? X509Certificate2.CreateFromPem(text)                      // PEM cert-only (bản tải ca.gov.vn)
                    : X509CertificateLoader.LoadCertificateFromFile(path);      // DER
                var sha = cert.GetCertHashString(HashAlgorithmName.SHA256);
                return string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase)
                    ? cert
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
