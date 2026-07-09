using Sip.be.Application.Document.Dtos.Check;
using Sip.be.Application.Document.Interfaces;
using Sip.be.Shared.Constants.Documents;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents
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
        private static readonly Lazy<X509Certificate2?> RootG2 = new(LoadRootG2);

        /// <summary>
        /// Verify 1 file. Non-PDF -> NotApplicable. PDF không có/không hợp lệ dấu thời gian -> verdict chặn.
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

                foreach (Match m in ByteRangeRegex.Matches(latin1))
                {
                    var der = ExtractContents(latin1, m);
                    if (der == null) continue;

                    SignedCms cms;
                    try { cms = new SignedCms(); cms.Decode(der); } catch { continue; }
                    if (cms.SignerInfos.Count == 0) continue;

                    // Chữ ký tài liệu đầu tiên (có signingTime hoặc timestamp) quyết định verdict.
                    var result = VerifySigner(cms.SignerInfos[0]);
                    if (result != null) return result;
                }

                dto.Verdict = TimestampVerdict.NoTimestamp;
                dto.HasTimestamp = false;
                dto.Reason = "PDF không có chữ ký số/dấu thời gian tin cậy.";
                return dto;
            }
            catch (Exception ex)
            {
                dto.Verdict = TimestampVerdict.NoTimestamp; // fail-closed
                dto.Reason = $"Không đọc được chữ ký/dấu thời gian: {ex.Message}";
                return dto;
            }
        }

        /// <summary>Verify 1 SignerInfo. Trả null nếu khối này không phải chữ ký tài liệu (không có signingTime/timestamp).</summary>
        private static TimestampVerifyDto? VerifySigner(SignerInfo signer)
        {
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

            // Không có cả timestamp lẫn signingTime -> không phải chữ ký tài liệu ta xét -> thử khối khác.
            if (tokenBytes == null && signingUtc == null) return null;

            var dto = new TimestampVerifyDto
            {
                SigningTime = signingUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
                HasTimestamp = tokenBytes != null
            };

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

            // Chain TSA -> Root G2 đã ghim (offline, custom trust anchor).
            var root = RootG2.Value;
            if (root == null)
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Không nạp được trust anchor Root G2 (kiểm tra file rootcag2.crt / pin).";
                return dto;
            }
            if (!ChainsToRoot(tsaCert, token.AsSignedCms().Certificates, root))
            {
                dto.Verdict = TimestampVerdict.UntrustedTsa;
                dto.Reason = "Dấu thời gian không thuộc TSA Ban Cơ yếu (chain không về Root G2).";
                return dto;
            }

            // trustedTime = genTime + 7h (quirk VGCA đo thực).
            var trustedUtc = token.TokenInfo.Timestamp.UtcDateTime.AddHours(SignatureConstants.VgcaGenTimeOffsetHours);
            dto.TrustedTime = trustedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

            // Không có giờ tự khai -> trustedTime là chuẩn -> hợp lệ.
            if (signingUtc == null)
            {
                dto.Verdict = TimestampVerdict.ValidTrusted;
                return dto;
            }

            var delta = Math.Abs((signingUtc.Value - trustedUtc).TotalSeconds);
            if (delta > SignatureConstants.ToleranceSeconds)
            {
                dto.Verdict = TimestampVerdict.TimeMismatch;
                dto.Reason = $"Giờ ký tự khai lệch {Math.Round(delta)}s so với dấu thời gian tin cậy.";
                return dto;
            }

            dto.Verdict = TimestampVerdict.ValidTrusted;
            return dto;
        }

        /// <summary>Dựng chain cho cert TSA với trust anchor tùy biến = CHỈ Root G2 đã ghim; revocation offline.</summary>
        private static bool ChainsToRoot(X509Certificate2 leaf, X509Certificate2Collection extra, X509Certificate2 root)
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ExtraStore.AddRange(extra);
            if (!chain.Build(leaf)) return false;
            // CustomRootTrust đã bảo đảm kết thúc ở Root G2; kiểm lại thumbprint cho chắc.
            var top = chain.ChainElements[^1].Certificate;
            return string.Equals(top.Thumbprint, root.Thumbprint, StringComparison.OrdinalIgnoreCase);
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

        /// <summary>Nạp Root G2 từ file (PEM hoặc DER) rồi đối chiếu SHA-256 (DER) với pin; null nếu thiếu/sai.</summary>
        private static X509Certificate2? LoadRootG2()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, SignatureConstants.RootG2FileName);
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                // CreateFromPem = PEM CHỈ chứa cert (CreateFromPemFile là cho cert+private key -> ném lỗi với PEM cert-only).
                var cert = text.Contains("BEGIN CERTIFICATE")
                    ? X509Certificate2.CreateFromPem(text)                      // PEM cert-only (bản tải ca.gov.vn)
                    : X509CertificateLoader.LoadCertificateFromFile(path);      // DER
                var sha = cert.GetCertHashString(HashAlgorithmName.SHA256);
                return string.Equals(sha, SignatureConstants.RootG2Sha256, StringComparison.OrdinalIgnoreCase)
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
