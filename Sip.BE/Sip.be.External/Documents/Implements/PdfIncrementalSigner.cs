using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Ký số PDF bằng cách NỐI BẢN (incremental update): giữ nguyên từng byte của file gốc rồi ghi thêm một
    /// bản sửa đổi mới vào cuối.
    ///
    /// Vì sao BẮT BUỘC phải làm thế: chữ ký PDF phủ OFFSET BYTE TUYỆT ĐỐI qua /ByteRange. Ghi lại cả tài liệu
    /// (điều PDFsharp document.Save() làm) khiến mọi byte dịch chỗ, /ByteRange của chữ ký CŨ trỏ sang byte
    /// khác và chữ ký cũ chết. Đo thực trên file thật: 99,6% byte trong vùng ký cũ bị đổi.
    /// Xem docs/bug/pdfsharp-rewrites-document-destroying-existing-signature.md.
    ///
    /// Bản mới gồm: ghi đè Catalog (thêm field chữ ký vào /AcroForm /Fields) và trang 1 (thêm widget vào
    /// /Annots) — DÙNG LẠI ĐÚNG SỐ HIỆU CŨ; các object mới cho field/widget, khối chữ ký hiển thị và dict
    /// giá trị chữ ký; cuối cùng là xref mới có /Prev trỏ về xref cũ.
    /// Xem .claude/be/plans/pdf-incremental-signing.plan.md step 4.
    /// </summary>
    public static class PdfIncrementalSigner
    {
        // Bề rộng cố định cho mỗi số trong /ByteRange. Giá trị thật được vá ĐÈ LÊN chỗ này sau khi đã biết
        // offset, nên độ dài PHẢI không đổi — dài ra một byte là mọi offset vừa tính đã sai.
        // 10 chữ số đủ cho file tới ~10 GB.
        private const int ByteRangeFieldWidth = 10;

        /// <summary>
        /// Ghi bản ký nối vào <paramref name="original"/> và trả về byte của file đã ký.
        /// <paramref name="computeSignature"/> nhận đúng dãy byte cần ký (2 vùng của /ByteRange nối lại) và
        /// trả về khối CMS/PKCS#7 — phần mật mã giữ nguyên ở <c>CadesSigner</c>, ở đây chỉ lo dựng container.
        /// <paramref name="placements"/> null/rỗng = một khối ở vị trí mặc định (góc trên-phải, trang 1);
        /// nhiều phần tử = người dùng kéo thả nhiều khối, mỗi khối một widget đặt ĐÚNG trang của khối đó.
        /// Trang vượt quá số trang của file thì kẹp về trang cuối (không báo lỗi, không bỏ qua file). Ô chữ ký
        /// cuối cùng do <paramref name="placementResolver"/> tính lại theo khổ trang THẬT của trang đó.
        /// Dù đóng dấu mấy trang, mấy khối thì vẫn CHỈ MỘT chữ ký.
        /// </summary>
        public static async Task<byte[]> SignAsync(
            byte[] original,
            string signerName,
            string reason,
            string location,
            DateTime signedAt,
            int signatureSizeBytes,
            IReadOnlyList<PdfSignPlacementDto>? placements,
            ISignaturePlacementResolver placementResolver,
            Func<byte[], CancellationToken, Task<byte[]>> computeSignature,
            CancellationToken cancellationToken = default)
        {
            var info = PdfRevisionInfo.Load(original);

            // ===== 1. Xác định các object cần ghi đè =====
            var catalogNo = info.RootObjectNumber;
            var catalogRaw = info.GetObjectBody(catalogNo)
                ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không đọc được Catalog của file PDF.");

            var pagesM = Regex.Match(catalogRaw, @"/Pages\s+(\d+)\s+\d+\s+R");
            if (!pagesM.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Catalog của file PDF không có /Pages.");
            }
            var pagesNo = int.Parse(pagesM.Groups[1].Value);

            // Duyệt cây trang lấy DANH SÁCH trang theo đúng thứ tự đọc. Không lấy thẳng kid đầu tiên như bản
            // trước nữa: người dùng chọn được trang bất kỳ, mà /Kids của node gốc có thể lại là node /Pages
            // trung gian chứ không phải trang. Duyệt bằng stack (đẩy ngược để pop ra đúng thứ tự) thay vì đệ quy.
            var pageOrder = new List<int>();
            var pageStack = new Stack<int>();
            var visitedNodes = new HashSet<int>();
            pageStack.Push(pagesNo);
            while (pageStack.Count > 0)
            {
                var node = pageStack.Pop();
                // Cây trang hỏng có thể trỏ vòng lại chính nó -> duyệt mãi không dừng.
                if (!visitedNodes.Add(node)) continue;

                var nodeRaw = info.GetObjectBody(node);
                if (nodeRaw == null) continue;

                // Có /Kids = node trung gian; không có = trang thật (lá).
                var kidsM = Regex.Match(nodeRaw, @"/Kids\s*\[(.*?)\]", RegexOptions.Singleline);
                if (!kidsM.Success)
                {
                    pageOrder.Add(node);
                    continue;
                }

                var kids = Regex.Matches(kidsM.Groups[1].Value, @"(\d+)\s+\d+\s+R")
                    .Select(m => int.Parse(m.Groups[1].Value)).ToList();
                for (var i = kids.Count - 1; i >= 0; i--) pageStack.Push(kids[i]);
            }

            if (pageOrder.Count == 0)
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "Không tìm được trang nào trong file PDF.");
            }

            // ===== 2. Ô chữ ký cho TỪNG khối người dùng đã đặt =====
            var nextNumber = info.Size;
            var sigFieldNo = nextNumber++;   // 1 widget: field gộp luôn widget; nhiều widget: field CHA của chúng
            var sigValueNo = nextNumber++;   // dict giá trị chữ ký (/ByteRange + /Contents) - luôn CHỈ MỘT

            // Người dùng kéo thả bao nhiêu khối thì đóng bấy nhiêu, mỗi khối ở ĐÚNG trang nó được thả vào.
            // Không có vị trí nào -> đúng MỘT khối ở chỗ mặc định (resolver nhận null thì tự đặt góc trên-phải).
            var placementList = placements is { Count: > 0 }
                ? placements.Select(p => (PdfSignPlacementDto?)p).ToList()
                : new List<PdfSignPlacementDto?> { null };

            // Mỗi trang tính lại khổ riêng: trong cùng một file vẫn có thể có trang ngang xen trang dọc, nên
            // tỉ lệ phải quy đổi theo /MediaBox của CHÍNH trang đó thì chữ ký mới nằm đúng chỗ trên mọi trang.
            var widgets = new List<(int PageNo, string PageRaw, PdfRectDto Rect, AppearanceResult Appearance, int WidgetNo)>();
            // Khối chữ ký KHÔNG ghi phần bù múi giờ: giờ vẽ ra luôn là giờ VN (signedAt lấy từ VietnamNow), nên
            // "+07:00" chỉ làm khối dài thêm mà không nói thêm được gì cho người đọc trong nước.
            var signedAtText = signedAt.ToString(SigningConstants.AppearanceTimeFormat, CultureInfo.InvariantCulture);
            foreach (var placement in placementList)
            {
                // Trang của chính khối này. File ÍT TRANG HƠN -> kẹp về trang cuối, TUYỆT ĐỐI không báo lỗi:
                // người dùng đặt vị trí một lần cho cả thư mục, đánh trượt một file chỉ vì nó ngắn hơn là hỏng
                // cả lô vì một con số không liên quan tới file đó.
                var pageIndex = Math.Clamp((placement?.PageNumber ?? 1) - 1, 0, pageOrder.Count - 1);
                var no = pageOrder[pageIndex];
                var raw = info.GetObjectBody(no)
                    ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                        $"Không đọc được trang {pageIndex + 1} của file PDF.");

                // /MediaBox có thể có gốc khác 0 và thứ tự tọa độ đảo, nên chuẩn hóa về (trái, dưới, rộng, cao).
                // Trang không tự khai /MediaBox thì THỪA KẾ của node /Pages cha — bỏ qua chỗ thừa kế này là
                // tính nhầm khổ A4 cho file khổ khác và chữ ký lệch hẳn ra ngoài vùng nhìn thấy.
                var boxM = Regex.Match(raw, @"/MediaBox\s*\[([^\]]*)\]");
                if (!boxM.Success)
                {
                    boxM = Regex.Match(info.GetObjectBody(pagesNo) ?? "", @"/MediaBox\s*\[([^\]]*)\]");
                }
                var box = boxM.Success
                    ? boxM.Groups[1].Value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()
                    : new[] { 0d, 0d, 595d, 842d };
                if (box.Length != 4) box = new[] { 0d, 0d, 595d, 842d };

                var pageBox = new PdfRectDto
                {
                    X = Math.Min(box[0], box[2]),
                    Y = Math.Min(box[1], box[3]),
                    Width = Math.Abs(box[2] - box[0]),
                    Height = Math.Abs(box[3] - box[1])
                };

                // Toàn bộ phép quy đổi tỉ lệ -> point và co giãn khối chữ ký nằm ở resolver, chạy LẠI cho
                // từng trang của từng file: một lựa chọn của người dùng ra ô đúng khổ trên mọi khổ giấy.
                var rect = placementResolver.Resolve(pageBox, placement);

                // Khối chữ ký hiển thị + cây font đi kèm, vẽ ĐÚNG khổ ô vừa tính (chữ co giãn theo, không bị
                // hộp một đằng chữ một nẻo). Mỗi ô một khối riêng vì BBox phải khớp đúng ô đó.
                var appearance = PdfAppearanceBuilder.Build(signerName, signedAtText, nextNumber,
                    rect.Width, rect.Height);
                nextNumber = appearance.Objects.Max(o => o.Number) + 1;

                // Đúng MỘT ô trong cả file -> giữ NGUYÊN hình dạng của file production (field gộp widget,
                // đúng một object). Nhiều ô -> mỗi ô một widget con của cùng một field.
                var widgetNo = placementList.Count == 1 ? sigFieldNo : nextNumber++;
                widgets.Add((no, raw, rect, appearance, widgetNo));
            }

            var xrefNo = nextNumber++;

            // ===== 3. Dựng các object ghi đè =====
            var newCatalog = BuildCatalogOverride(catalogRaw, sigFieldNo);

            // Gom theo TRANG: một trang có bao nhiêu ô thì /Annots của nó nhận bấy nhiêu widget, và phải nối
            // dồn vào cùng một bản dict — ghi đè trang hai lần thì lần sau xoá mất widget của lần trước.
            var newPages = widgets.GroupBy(w => w.PageNo).Select(g =>
            {
                var body = g.First().PageRaw;
                foreach (var w in g)
                {
                    body = PdfObjectWriter.AppendToArray(body, "Annots", w.WidgetNo)
                        ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                            "/Annots của trang là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");
                }
                return (PageNo: g.Key, Body: body);
            }).ToList();

            // ===== 5. Xếp bản mới ra byte =====
            var sb = new StringBuilder();
            var offsets = new Dictionary<int, long>();

            // File gốc phải kết thúc bằng xuống dòng trước khi nối, nếu không "%%EOF" cũ dính vào object mới.
            var baseLength = original.Length;
            var prefixNewline = !(original[^1] == '\n' || original[^1] == '\r');
            if (prefixNewline) sb.Append('\n');

            // Offset tính từ đầu FILE, không phải đầu bản mới. sb.Length dùng thẳng làm số byte được vì mọi
            // ký tự nối vào đều < 256 và cuối cùng ghi ra bằng Latin1 (1 ký tự = đúng 1 byte).
            long CurrentOffset() => baseLength + sb.Length;

            void WriteRaw(int number, string body, byte[]? stream)
            {
                offsets[number] = CurrentOffset();
                sb.Append(number.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n").Append(body);
                if (stream != null)
                {
                    sb.Append("\nstream\n").Append(Encoding.Latin1.GetString(stream)).Append("\nendstream");
                }
                sb.Append("\nendobj\n");
            }

            WriteRaw(catalogNo, newCatalog, null);
            foreach (var p in newPages) WriteRaw(p.PageNo, p.Body, null);

            var fieldSb = new StringBuilder();
            if (widgets.Count == 1)
            {
                // Field chữ ký gộp widget: /FT/Sig + /Subtype/Widget trên cùng một object là cách chuẩn và
                // cũng là cách file production thật làm (chỉ có MỘT object 44 nằm trong cả /Fields lẫn /Annots).
                var only = widgets[0];
                PdfObjectWriter.WriteValue(fieldSb, new PdfObjectWriter.Dict()
                    .Add("Type", new PdfObjectWriter.Name("Annot"))
                    .Add("Subtype", new PdfObjectWriter.Name("Widget"))
                    .Add("FT", new PdfObjectWriter.Name("Sig"))
                    .Add("Ff", 0)
                    // Tên field phải DUY NHẤT trong tài liệu, nếu trùng field cũ thì trình đọc coi là cùng một
                    // ô và chữ ký mới đè chữ ký cũ. Gắn số hiệu object vào là chắc chắn không trùng.
                    .Add("T", new PdfObjectWriter.Str("SIPPACK_Signature_" + sigFieldNo))
                    .Add("V", new PdfObjectWriter.Ref(sigValueNo))
                    .Add("P", new PdfObjectWriter.Ref(only.PageNo))
                    .Add("F", 4) // cờ Print: chữ ký phải hiện khi in
                    .Add("Rect", new object[] { only.Rect.X, only.Rect.Y, only.Rect.Right, only.Rect.Top })
                    .Add("AP", new PdfObjectWriter.Dict()
                        .Add("N", new PdfObjectWriter.Ref(only.Appearance.FormObjectNumber))));
                WriteRaw(sigFieldNo, fieldSb.ToString(), null);
            }
            else
            {
                // Nhiều ô: MỘT field chữ ký có nhiều widget con (/Kids) - chuẩn PDF 12.7.3.1 cho phép.
                // Vẫn là MỘT chữ ký duy nhất (một /V, một /ByteRange phủ cả file), chỉ hiển thị ở nhiều chỗ;
                // tách thành nhiều field = nhiều chữ ký = nhiều lần gọi TSA và nhiều lần chạm khóa token.
                PdfObjectWriter.WriteValue(fieldSb, new PdfObjectWriter.Dict()
                    .Add("FT", new PdfObjectWriter.Name("Sig"))
                    .Add("Ff", 0)
                    .Add("T", new PdfObjectWriter.Str("SIPPACK_Signature_" + sigFieldNo))
                    .Add("V", new PdfObjectWriter.Ref(sigValueNo))
                    .Add("Kids", widgets.Select(w => new PdfObjectWriter.Ref(w.WidgetNo)).ToArray()));
                WriteRaw(sigFieldNo, fieldSb.ToString(), null);

                foreach (var p in widgets)
                {
                    var widgetSb = new StringBuilder();
                    PdfObjectWriter.WriteValue(widgetSb, new PdfObjectWriter.Dict()
                        .Add("Type", new PdfObjectWriter.Name("Annot"))
                        .Add("Subtype", new PdfObjectWriter.Name("Widget"))
                        .Add("Parent", new PdfObjectWriter.Ref(sigFieldNo))
                        .Add("P", new PdfObjectWriter.Ref(p.PageNo))
                        .Add("F", 4)
                        .Add("Rect", new object[] { p.Rect.X, p.Rect.Y, p.Rect.Right, p.Rect.Top })
                        .Add("AP", new PdfObjectWriter.Dict()
                            .Add("N", new PdfObjectWriter.Ref(p.Appearance.FormObjectNumber))));
                    WriteRaw(p.WidgetNo, widgetSb.ToString(), null);
                }
            }

            foreach (var p in widgets)
            {
                foreach (var o in p.Appearance.Objects) WriteRaw(o.Number, o.DictText, o.StreamData);
            }

            // Dict giá trị chữ ký. /ByteRange và /Contents ghi bằng CHỖ TRỐNG có bề rộng CỐ ĐỊNH rồi vá đè
            // sau — vá xong độ dài không đổi nên offset đã tính vẫn đúng.
            var contentsHexLength = signatureSizeBytes * 2;
            var byteRangePlaceholder = "/ByteRange [" +
                string.Join(" ", Enumerable.Repeat(new string(' ', ByteRangeFieldWidth), 4)) + "]";

            var sigSb = new StringBuilder();
            sigSb.Append("<</Type /Sig/Filter /Adobe.PPKLite/SubFilter /")
                .Append(SigningConstants.PdfSubFilter);
            sigSb.Append("/Name ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(signerName));
            sigSb.Append("/Reason ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(reason));
            sigSb.Append("/Location ");
            PdfObjectWriter.WriteValue(sigSb, new PdfObjectWriter.Str(location));
            // /M = giờ ký theo định dạng ngày tháng của PDF: D:YYYYMMDDHHmmSS+HH'mm'.
            // signedAt là giờ Việt Nam (DateTimeConstants.VietnamNow, Kind=Unspecified) nên múi giờ phải lấy
            // TƯỜNG MINH từ múi giờ VN — dùng offset của MÁY sẽ ghi sai khi máy đặt múi giờ khác.
            var vnOffset = DateTimeConstants.VietnamTimeZone.BaseUtcOffset;
            sigSb.Append("/M (D:").Append(signedAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture))
                .Append(vnOffset >= TimeSpan.Zero ? '+' : '-')
                .Append(Math.Abs(vnOffset.Hours).ToString("00", CultureInfo.InvariantCulture)).Append('\'')
                .Append(Math.Abs(vnOffset.Minutes).ToString("00", CultureInfo.InvariantCulture)).Append("')");
            sigSb.Append(byteRangePlaceholder);
            sigSb.Append("/Contents <").Append(new string('0', contentsHexLength)).Append('>');
            sigSb.Append(">>");
            WriteRaw(sigValueNo, sigSb.ToString(), null);

            // ===== 6. Xref mới =====
            var xrefOffset = CurrentOffset();
            if (info.LastXrefIsStream)
            {
                // Luồng xref LÀ một object nên phải tự khai chính mình trong bảng.
                offsets[xrefNo] = xrefOffset;
                sb.Append(BuildXrefStream(xrefNo, offsets, Math.Max(info.Size, offsets.Keys.Max() + 1), info));
            }
            else
            {
                // Bảng xref cổ điển KHÔNG phải object -> không có mục cho chính nó.
                sb.Append(BuildXrefTable(offsets, Math.Max(info.Size, offsets.Keys.Max() + 1), xrefOffset, info));
            }

            // ===== 7. Ghép byte, vá /ByteRange, ký, vá /Contents =====
            var appended = Encoding.Latin1.GetBytes(sb.ToString());
            var output = new byte[baseLength + appended.Length];
            Buffer.BlockCopy(original, 0, output, 0, baseLength);
            Buffer.BlockCopy(appended, 0, output, baseLength, appended.Length);

            var text = Encoding.Latin1.GetString(output);

            // Định vị /Contents CỦA BẢN MỚI: tìm từ cuối file lùi lại, không bao giờ vớ phải chữ ký cũ.
            var contentsIdx = text.LastIndexOf("/Contents <", StringComparison.Ordinal);
            var brIdx = text.LastIndexOf("/ByteRange [", StringComparison.Ordinal);
            if (contentsIdx < baseLength || brIdx < baseLength)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không định vị được chỗ trống chữ ký trong bản ký nối vừa dựng.");
            }

            var gapStart = contentsIdx + "/Contents ".Length;      // vị trí dấu '<'
            var gapEnd = gapStart + 1 + contentsHexLength + 1;     // ngay sau dấu '>'

            // Ước lệ /ByteRange: vùng 1 kết thúc TẠI dấu '<', vùng 2 bắt đầu NGAY SAU dấu '>' — đo thực trên
            // file production (byte tại offset thứ 2 đúng là '<', byte trước offset thứ 3 đúng là '>').
            var ranges = new long[] { 0, gapStart, gapEnd, output.Length - gapEnd };
            var realByteRange = "/ByteRange [" + string.Join(" ", ranges.Select(v =>
                v.ToString(CultureInfo.InvariantCulture).PadRight(ByteRangeFieldWidth))) + "]";
            var placeholderLength = byteRangePlaceholder.Length;
            if (realByteRange.Length != placeholderLength)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    $"/ByteRange thật dài {realByteRange.Length} byte, khác chỗ trống {placeholderLength} byte.");
            }
            Encoding.Latin1.GetBytes(realByteRange).CopyTo(output, brIdx);

            // Dãy byte cần ký = 2 vùng nối lại (bỏ đúng chỗ trống /Contents).
            var signed = new byte[ranges[1] + ranges[3]];
            Buffer.BlockCopy(output, 0, signed, 0, (int)ranges[1]);
            Buffer.BlockCopy(output, (int)ranges[2], signed, (int)ranges[1], (int)ranges[3]);

            var cms = await computeSignature(signed, cancellationToken);

            if (cms.Length > signatureSizeBytes)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    $"Khối chữ ký {cms.Length} byte vượt chỗ trống {signatureSizeBytes} byte đã chừa.");
            }

            // Vá hex vào giữa '<' và '>'; phần dư giữ nguyên số '0' đệm (chuẩn cho phép đệm sau khối DER).
            var hex = Convert.ToHexString(cms);
            Encoding.Latin1.GetBytes(hex).CopyTo(output, gapStart + 1);

            return output;
        }

        /// <summary>
        /// Ghi đè Catalog: thêm field chữ ký vào /AcroForm /Fields và bật /SigFlags 3.
        /// Xử lý cả 3 dạng gặp thực tế: /AcroForm là dict nằm thẳng (100% file mẫu), là tham chiếu gián tiếp,
        /// hoặc KHÔNG CÓ (file chưa từng ký — lúc đó phải tạo mới cả /AcroForm).
        /// </summary>
        private static string BuildCatalogOverride(string catalogRaw, int sigFieldNo)
        {
            var idx = catalogRaw.IndexOf("/AcroForm", StringComparison.Ordinal);
            if (idx < 0)
            {
                // Chưa có AcroForm -> dựng mới tối thiểu. /SigFlags 3 = tài liệu có chữ ký + chỉ được sửa
                // bằng cách nối bản; thiếu cờ này trình đọc có thể lưu đè và giết chữ ký.
                var close = catalogRaw.LastIndexOf(">>", StringComparison.Ordinal);
                if (close < 0)
                {
                    throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                        "Catalog của file PDF không hợp lệ.");
                }
                return catalogRaw[..close] +
                       "/AcroForm<</Fields[" + sigFieldNo + " 0 R]/SigFlags 3/DA(/Helv 0 Tf 0 g )>>" +
                       catalogRaw[close..];
            }

            var p = idx + "/AcroForm".Length;
            while (p < catalogRaw.Length && char.IsWhiteSpace(catalogRaw[p])) p++;
            if (p + 1 >= catalogRaw.Length || catalogRaw[p] != '<' || catalogRaw[p + 1] != '<')
            {
                throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "/AcroForm là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");
            }

            var acro = PdfRevisionInfo.ExtractDictionary(catalogRaw, p);
            var newAcro = PdfObjectWriter.AppendToArray(acro, "Fields", sigFieldNo)
                ?? throw new UserFriendlyException(ErrorCodes.SignPdfStructureUnsupported,
                    "/Fields của AcroForm là tham chiếu gián tiếp — dạng này chưa hỗ trợ ký nối bản.");

            if (!Regex.IsMatch(newAcro, @"/SigFlags\s+\d"))
            {
                var close = newAcro.LastIndexOf(">>", StringComparison.Ordinal);
                newAcro = newAcro[..close] + "/SigFlags 3" + newAcro[close..];
            }

            return catalogRaw[..p] + newAcro + catalogRaw[(p + acro.Length)..];
        }

        /// <summary>
        /// Dựng LUỒNG xref mới (/Type/XRef), KHÔNG nén và KHÔNG predictor — chuẩn cho phép, và giữ được bộ
        /// nén ra khỏi đường đi chính. /Prev trỏ về xref cũ, /ID giữ nguyên của tài liệu.
        /// </summary>
        private static string BuildXrefStream(int xrefNo, Dictionary<int, long> offsets, int newSize,
            PdfRevisionInfo info)
        {
            var numbers = offsets.Keys.OrderBy(n => n).ToList();

            // /Index gom các số hiệu liên tiếp thành từng đoạn [số đầu, số lượng].
            var index = new List<int>();
            var data = new List<byte>();
            var i = 0;
            while (i < numbers.Count)
            {
                var start = numbers[i];
                var count = 1;
                while (i + count < numbers.Count && numbers[i + count] == start + count) count++;
                index.Add(start);
                index.Add(count);
                for (var k = 0; k < count; k++)
                {
                    var off = offsets[start + k];
                    data.Add(1);                                  // type 1 = nằm thẳng trong file
                    data.Add((byte)((off >> 24) & 0xFF));         // offset, 4 byte big-endian (W[1] = 4)
                    data.Add((byte)((off >> 16) & 0xFF));
                    data.Add((byte)((off >> 8) & 0xFF));
                    data.Add((byte)(off & 0xFF));
                    data.Add(0);                                  // gen, 2 byte (W[2] = 2)
                    data.Add(0);
                }
                i += count;
            }

            var sb = new StringBuilder();
            sb.Append(xrefNo.ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n");
            sb.Append("<</Type /XRef/Size ").Append(newSize);
            sb.Append("/Index [").Append(string.Join(" ", index)).Append(']');
            sb.Append("/W [1 4 2]");
            sb.Append("/Root ").Append(info.RootObjectNumber).Append(" 0 R");
            if (info.InfoRaw != null) sb.Append("/Info ").Append(info.InfoRaw);
            if (info.IdRaw != null) sb.Append("/ID ").Append(info.IdRaw);
            sb.Append("/Prev ").Append(info.LastXrefOffset);
            sb.Append("/Length ").Append(data.Count).Append(">>\n");
            sb.Append("stream\n").Append(Encoding.Latin1.GetString(data.ToArray())).Append("\nendstream\nendobj\n");
            sb.Append("startxref\n").Append(offsets[xrefNo]).Append("\n%%EOF\n");
            return sb.ToString();
        }

        /// <summary>
        /// Dựng BẢNG xref cổ điển + trailer. Không file mẫu nào dùng dạng này (cả 14 file đều là luồng), nhưng
        /// PDF đời cũ trong kho lưu trữ thì có — và xref mới BẮT BUỘC cùng dạng với xref cũ nó nối vào.
        /// </summary>
        private static string BuildXrefTable(Dictionary<int, long> offsets, int newSize, long xrefStart,
            PdfRevisionInfo info)
        {
            var numbers = offsets.Keys.OrderBy(n => n).ToList();
            var sb = new StringBuilder();
            sb.Append("xref\n");
            var i = 0;
            while (i < numbers.Count)
            {
                var start = numbers[i];
                var count = 1;
                while (i + count < numbers.Count && numbers[i + count] == start + count) count++;
                sb.Append(start).Append(' ').Append(count).Append('\n');
                for (var k = 0; k < count; k++)
                {
                    // Mỗi dòng đúng 20 byte: "nnnnnnnnnn ggggg n\r\n".
                    sb.Append(offsets[start + k].ToString("D10", CultureInfo.InvariantCulture))
                        .Append(" 00000 n\r\n");
                }
                i += count;
            }

            sb.Append("trailer\n<</Size ").Append(newSize);
            sb.Append("/Root ").Append(info.RootObjectNumber).Append(" 0 R");
            if (info.InfoRaw != null) sb.Append("/Info ").Append(info.InfoRaw);
            if (info.IdRaw != null) sb.Append("/ID ").Append(info.IdRaw);
            sb.Append("/Prev ").Append(info.LastXrefOffset).Append(">>\n");
            sb.Append("startxref\n").Append(xrefStart).Append("\n%%EOF\n");
            return sb.ToString();
        }
    }
}
