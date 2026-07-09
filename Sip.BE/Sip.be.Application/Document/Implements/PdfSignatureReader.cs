using Sip.be.Application.Document.Dtos.Check;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.Application.Document.Implements
{
    /// <summary>
    /// Đọc chữ ký số nhúng trong PDF (chuẩn PAdES/PKCS#7) mà KHÔNG cần thư viện ngoài.
    /// Cơ chế: quét các mục /ByteRange trong file, trích chuỗi hex /Contents (khối CMS/PKCS#7),
    /// giải mã bằng System.Security.Cryptography.Pkcs.SignedCms để lấy người ký (CN của certificate)
    /// và thời điểm ký (thuộc tính signingTime của CMS, fallback sang mục /M trong dict chữ ký).
    ///
    /// Chỉ TRÍCH danh tính người ký + thời gian (giống Foxit hiển thị), KHÔNG xác thực toàn vẹn tài liệu
    /// hay chuỗi certificate.
    /// </summary>
    public static class PdfSignatureReader
    {
        // /ByteRange [start1 len1 start2 len2] — khối chữ ký nằm trong khoảng hở [start1+len1, start2).
        private static readonly Regex ByteRangeRegex =
            new(@"/ByteRange\s*\[\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*\]", RegexOptions.Compiled);

        // OID signingTime (PKCS#9) — thời điểm ký ghi trong thuộc tính có ký của CMS.
        private const string SigningTimeOid = "1.2.840.113549.1.9.5";

        // Giới hạn cửa sổ dò ngược tìm /M, /Name... trong cùng dict chữ ký (đứng trước /Contents).
        private const int DictLookBack = 6000;

        /// <summary>
        /// Đọc thông tin ký số của 1 file. File không phải PDF -> trả về IsSigned=false (không đọc nội dung).
        /// Mọi lỗi đọc được nuốt và ghi vào <see cref="SignatureInfoDto.Error"/> (không ném ra ngoài).
        /// </summary>
        public static SignatureInfoDto Read(string filePath)
        {
            var info = new SignatureInfoDto();
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) ||
                    !string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    return info; // không phải PDF -> coi như chưa ký
                }

                var bytes = File.ReadAllBytes(filePath);
                // Latin1: mỗi byte = 1 ký tự -> chỉ số ký tự == chỉ số byte, tiện định vị /Contents theo ByteRange.
                var latin1 = Encoding.Latin1.GetString(bytes);

                foreach (Match m in ByteRangeRegex.Matches(latin1))
                {
                    var start1 = long.Parse(m.Groups[1].Value);
                    var len1 = long.Parse(m.Groups[2].Value);
                    var start2 = long.Parse(m.Groups[3].Value);

                    var gapStart = (int)(start1 + len1);
                    var gapEnd = (int)start2;
                    if (gapStart < 0 || gapEnd > latin1.Length || gapStart >= gapEnd) continue;

                    // /Contents <hex...> nằm trong khoảng hở; lấy phần giữa '<' và '>'.
                    var lt = latin1.IndexOf('<', gapStart);
                    if (lt < 0 || lt >= gapEnd) continue;
                    var gt = latin1.IndexOf('>', lt + 1);
                    if (gt < 0 || gt > gapEnd) continue;

                    var hex = Regex.Replace(latin1.Substring(lt + 1, gt - lt - 1), @"[^0-9A-Fa-f]", string.Empty);
                    if (hex.Length % 2 != 0) hex = hex[..^1];
                    if (hex.Length < 2) continue;

                    byte[] der;
                    try { der = TrimDer(Convert.FromHexString(hex)); }
                    catch { continue; }

                    var signer = ParseCms(der);
                    // Fallback đọc /M (thời gian) + /Name (người ký) từ dict chữ ký nếu CMS thiếu.
                    var dictWindow = latin1.Substring(Math.Max(0, lt - DictLookBack), lt - Math.Max(0, lt - DictLookBack));
                    if (string.IsNullOrWhiteSpace(signer.SignedAt))
                        signer.SignedAt = ParseMDate(dictWindow);
                    if (string.IsNullOrWhiteSpace(signer.SignedBy))
                        signer.SignedBy = ParseDictString(dictWindow, "Name");
                    signer.Reason ??= ParseDictString(dictWindow, "Reason");
                    signer.Location ??= ParseDictString(dictWindow, "Location");

                    info.Signers.Add(signer);
                }

                info.SignatureCount = info.Signers.Count;
                info.IsSigned = info.Signers.Count > 0;
            }
            catch (Exception ex)
            {
                info.Error = ex.Message;
            }
            return info;
        }

        /// <summary>Giải mã khối CMS/PKCS#7 -> người ký (CN certificate) + thời điểm ký (signingTime).</summary>
        private static SignerInfoDto ParseCms(byte[] der)
        {
            var result = new SignerInfoDto();
            try
            {
                var cms = new SignedCms();
                cms.Decode(der);

                foreach (SignerInfo si in cms.SignerInfos)
                {
                    var cert = si.Certificate;
                    if (cert != null)
                    {
                        var cn = cert.GetNameInfo(X509NameType.SimpleName, false);
                        if (!string.IsNullOrWhiteSpace(cn)) result.SignedBy = cn.Trim();
                    }

                    foreach (var attr in si.SignedAttributes)
                    {
                        if (attr.Oid?.Value == SigningTimeOid && attr.Values.Count > 0)
                        {
                            var st = new Pkcs9SigningTime(attr.Values[0].RawData);
                            result.SignedAt = st.SigningTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
                        }
                    }
                    break; // lấy signer đầu tiên của khối chữ ký này
                }
            }
            catch
            {
                // Không giải mã được CMS -> để fallback /M, /Name xử lý.
            }
            return result;
        }

        /// <summary>Cắt mảng DER về đúng độ dài SEQUENCE ngoài cùng (bỏ các byte 0 đệm sau /Contents).</summary>
        private static byte[] TrimDer(byte[] data)
        {
            if (data.Length < 2 || data[0] != 0x30) return data; // không phải SEQUENCE -> giữ nguyên
            int idx;
            int len;
            if ((data[1] & 0x80) == 0)
            {
                len = data[1];
                idx = 2;
            }
            else
            {
                var numBytes = data[1] & 0x7F;
                idx = 2;
                len = 0;
                for (var i = 0; i < numBytes && idx < data.Length; i++)
                {
                    len = (len << 8) | data[idx++];
                }
            }
            var total = idx + len;
            return total > 0 && total <= data.Length ? data[..total] : data;
        }

        /// <summary>Đọc /M (D:yyyyMMddHHmmss...) trong dict chữ ký, format "dd/MM/yyyy HH:mm:ss".</summary>
        private static string? ParseMDate(string dictWindow)
        {
            var m = Regex.Match(dictWindow, @"/M\s*\(\s*D:(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})");
            if (!m.Success) return null;
            try
            {
                var dt = new DateTime(
                    int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value),
                    int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value));
                return dt.ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Đọc 1 chuỗi literal của mục /Key (...) trong dict chữ ký; null nếu không có.</summary>
        private static string? ParseDictString(string dictWindow, string key)
        {
            var m = Regex.Match(dictWindow, "/" + key + @"\s*\(([^)]*)\)");
            if (!m.Success) return null;
            var value = m.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
