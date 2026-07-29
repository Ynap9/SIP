using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Soi định dạng PDF cấp thấp mà KHÔNG kéo thư viện nặng: tái dùng <see cref="PdfRevisionInfo"/> (parser
    /// xref/object đã kiểm chứng trên gói mẫu thật) để đọc Catalog, luồng /Metadata (XMP) và quét object.
    ///
    /// 3 tầng phát hiện, dừng ở lỗi đầu tiên:
    /// 1) NotPdf   - không có header %PDF- trong 1024 byte đầu.
    /// 2) NotPdfA  - đọc XMP ở /Metadata, thiếu pdfaid:part (namespace http://www.aiim.org/pdfa/ns/id/).
    /// 3) NotTwoLayer - không đủ CẢ lớp ảnh raster (/Subtype /Image) LẪN lớp text (font: /Type /Font | /BaseFont).
    /// Đủ 2 lớp + là PDF/A -> Valid. Mọi exception -> ReadFailed (fail-closed).
    /// Xem .claude/be/plans/pdfa-precheck-be.plan.md và docs/chuan-dong-goi-sip-tt05.md (§ two-layer PDF/A).
    /// </summary>
    public class PdfFormatInspector : IPdfFormatInspector
    {
        // Namespace định danh PDF/A trong XMP: có pdfaid:part trong namespace này = tài liệu tự khai là PDF/A.
        private const string PdfAIdNamespace = "http://www.aiim.org/pdfa/ns/id/";

        // Số byte đầu file dò header %PDF-. Chuẩn cho phép header không nằm ngay byte 0 nên dò cả cửa sổ đầu.
        private const int HeaderProbeBytes = 1024;

        /// <inheritdoc/>
        public PdfFormatInspectResult Inspect(string filePath)
        {
            var result = new PdfFormatInspectResult();
            try
            {
                var bytes = File.ReadAllBytes(filePath);

                // ===== 1. NotPdf: header %PDF- =====
                var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(HeaderProbeBytes, bytes.Length));
                if (!head.Contains("%PDF-", StringComparison.Ordinal))
                {
                    result.Verdict = PdfFormatVerdict.NotPdf;
                    return result;
                }

                // Parser cấp thấp: ném UserFriendlyException với file mã hóa / cấu trúc hỏng -> rơi xuống
                // catch -> ReadFailed (không soi nổi thì fail-closed, không đoán bừa là PDF/A).
                var info = PdfRevisionInfo.Load(bytes);

                // ===== 2. NotPdfA: đọc pdfaid:part trong XMP ở /Metadata =====
                var catalog = info.GetObjectBody(info.RootObjectNumber);
                string? part = null;
                string? conformance = null;
                var metaM = catalog != null
                    ? Regex.Match(catalog, @"/Metadata\s+(\d+)\s+\d+\s+R")
                    : Match.Empty;
                if (metaM.Success)
                {
                    var metaNo = int.Parse(metaM.Groups[1].Value);
                    var xmpBytes = info.GetRawStreamBytes(metaNo);
                    // PDF/A CẤM nén luồng XMP tài liệu, nhưng vài bộ sinh vẫn nén — giải Flate nếu có để không
                    // báo nhầm PDF/A thật thành NotPdfA (chặn oan).
                    var metaDict = info.GetObjectBody(metaNo);
                    if (xmpBytes != null && metaDict != null &&
                        metaDict.Contains("/FlateDecode", StringComparison.Ordinal))
                    {
                        try
                        {
                            using var input = new MemoryStream(xmpBytes);
                            using var z = new ZLibStream(input, CompressionMode.Decompress);
                            using var outMs = new MemoryStream();
                            z.CopyTo(outMs);
                            xmpBytes = outMs.ToArray();
                        }
                        catch { /* giải nén hỏng -> giữ nguyên byte thô, coi như không đọc được pdfaid */ }
                    }

                    if (xmpBytes != null)
                    {
                        var xmp = Encoding.UTF8.GetString(xmpBytes);
                        // Chỉ tin pdfaid khi đúng namespace PDF/A để không vớ phải thuộc tính trùng tên của schema khác.
                        if (xmp.Contains(PdfAIdNamespace, StringComparison.Ordinal))
                        {
                            // Bắt cả 2 dạng XMP: phần tử <pdfaid:part>2</...> và thuộc tính pdfaid:part="2".
                            var pm = Regex.Match(xmp, "pdfaid:part\\s*[>=]\\s*[\"']?\\s*(\\d+)");
                            if (pm.Success) part = pm.Groups[1].Value;
                            var cm = Regex.Match(xmp, "pdfaid:conformance\\s*[>=]\\s*[\"']?\\s*([A-Za-z])");
                            if (cm.Success) conformance = cm.Groups[1].Value.ToUpperInvariant();
                        }
                    }
                }

                if (string.IsNullOrEmpty(part))
                {
                    result.Verdict = PdfFormatVerdict.NotPdfA;
                    return result;
                }
                result.PdfAPart = part;
                result.PdfAConformance = conformance;

                // ===== 3. NotTwoLayer: cần CẢ lớp ảnh (/Subtype /Image) LẪN lớp text (font) =====
                // Quét dict của mọi object. Image XObject là stream object nằm thẳng nên GetObjectBody trả đúng
                // dict của nó; font có thể nằm trong object stream nhưng GetObjectBody đã giải nén giúp.
                // /Type /Font (có \b để không dính /FontDescriptor) hoặc /BaseFont = có font -> có lớp text OCR.
                var hasImage = false;
                var hasText = false;
                foreach (var num in info.Objects.Keys)
                {
                    if (hasImage && hasText) break;
                    var body = info.GetObjectBody(num);
                    if (body == null) continue;
                    if (!hasImage && Regex.IsMatch(body, @"/Subtype\s*/Image\b")) hasImage = true;
                    if (!hasText && (Regex.IsMatch(body, @"/Type\s*/Font\b") ||
                        body.Contains("/BaseFont", StringComparison.Ordinal))) hasText = true;
                }

                result.HasImageLayer = hasImage;
                result.HasTextLayer = hasText;
                if (!hasImage || !hasText)
                {
                    result.Verdict = PdfFormatVerdict.NotTwoLayer;
                    return result;
                }

                result.Verdict = PdfFormatVerdict.Valid;
                return result;
            }
            catch (Exception ex)
            {
                // Fail-closed: đọc không nổi thì coi như KHÔNG ký được, tuyệt đối không suy ra hợp lệ.
                result.Verdict = PdfFormatVerdict.ReadFailed;
                result.Reason = ex.Message;
                return result;
            }
        }
    }
}
