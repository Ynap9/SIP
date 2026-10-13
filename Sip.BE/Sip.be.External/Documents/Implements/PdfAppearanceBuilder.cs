using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Sip.be.External.Documents.Dtos;
using Sip.be.Shared.Constants.Documents;
using Sip.be.Shared.Request.AppException;
using Sip.be.Shared.Request.ErrorRequest;
using System.Text;
using System.Text.RegularExpressions;

namespace Sip.be.External.Documents.Implements
{
    /// <summary>
    /// Dựng khối chữ ký hiển thị (2 dòng: tên người ký + giờ ký) rồi CẤY sang tài liệu đích.
    ///
    /// Vì sao vẫn giữ PDFsharp ở đây trong khi phần ghi file đã tự làm: tên người ký có DẤU TIẾNG VIỆT
    /// ("Phạm Ngọc Chính"). Font base-14 Times-Roman với bảng mã WinAnsi KHÔNG mã hóa được "ạ ọ í", nên bắt
    /// buộc phải nhúng một font Unicode đã rút gọn (subset) — mà PDFsharp làm việc đó đúng sẵn rồi. Tự viết
    /// bộ nhúng font là công việc lớn và dễ sai, không có lý do gì làm lại.
    ///
    /// Cách làm: vẽ vào một tài liệu PDF TẠM trong bộ nhớ có khổ trang đúng bằng khối chữ ký, rồi bê luồng
    /// nội dung của trang đó thành Form XObject, kéo theo toàn bộ cây tài nguyên (font, font descriptor,
    /// file font nhúng) và đánh lại số hiệu cho khớp tài liệu đích.
    /// Xem .claude/be/plans/pdf-incremental-signing.plan.md step 3.
    /// </summary>
    public static class PdfAppearanceBuilder
    {
        public static (double Width, double Height) MeasureText(string line1, string line2, string fontFamily,
            double fontSize)
        {
            var font = new XFont(fontFamily, fontSize);
            using var gfx = XGraphics.CreateMeasureContext(new XSize(1, 1), XGraphicsUnit.Point,
                XPageDirection.Downwards);

            return (Math.Max(gfx.MeasureString(line1, font).Width, gfx.MeasureString(line2, font).Width),
                font.GetHeight() * 2);
        }

        public static AppearanceResult Build(string line1, string line2, int firstObjectNumber,
            double width, double height, string fontFamily, double fontSize, string textColor)
        {
            var hex = (textColor ?? string.Empty).TrimStart('#');
            var color = hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb)
                ? XColor.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF)
                : XColors.Black;

            return DrawAndTransplant(firstObjectNumber, width, height, gfx =>
            {
                var font = new XFont(fontFamily, fontSize);
                var brush = new XSolidBrush(color);
                var lineHeight = font.GetHeight();

                gfx.DrawString(line1, font, brush, new XRect(0, 0, width, lineHeight), XStringFormats.TopLeft);
                gfx.DrawString(line2, font, brush, new XRect(0, lineHeight, width, lineHeight),
                    XStringFormats.TopLeft);
            });
        }

        public static AppearanceResult BuildImage(byte[] image, int firstObjectNumber, double width, double height)
        {
            using var stream = new MemoryStream(image);
            var xImage = XImage.FromStream(stream);

            return DrawAndTransplant(firstObjectNumber, width, height, gfx =>
            {
                var scale = Math.Min(width / xImage.PointWidth, height / xImage.PointHeight);
                var drawWidth = xImage.PointWidth * scale;
                var drawHeight = xImage.PointHeight * scale;
                gfx.DrawImage(xImage, new XRect((width - drawWidth) / 2, (height - drawHeight) / 2,
                    drawWidth, drawHeight));
            });
        }

        private static AppearanceResult DrawAndTransplant(int firstObjectNumber, double width, double height,
            Action<XGraphics> draw)
        {
            byte[] tempPdf;
            using (var document = new PdfDocument())
            {
                // Không nén: /Length thành số trực tiếp và không sinh object stream -> khâu đọc lại bên dưới
                // đơn giản và chắc chắn hơn. Tài liệu này chỉ sống trong bộ nhớ vài mili giây nên kích thước
                // không đáng quan tâm. Riêng file font nhúng vẫn tự nén theo cách của nó, ta chép nguyên byte.
                document.Options.CompressContentStreams = false;

                var page = document.AddPage();
                page.Width = XUnit.FromPoint(width);
                page.Height = XUnit.FromPoint(height);

                using (var gfx = XGraphics.FromPdfPage(page))
                {
                    draw(gfx);
                }

                using var ms = new MemoryStream();
                document.Save(ms, closeStream: false);
                tempPdf = ms.ToArray();
            }

            var temp = PdfRevisionInfo.Load(tempPdf);

            // Lần từ Catalog -> Pages -> trang đầu để lấy luồng nội dung và tài nguyên đã vẽ.
            var catalog = temp.GetObjectBody(temp.RootObjectNumber)
                ?? throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không đọc được Catalog của tài liệu khối chữ ký tạm.");
            var pagesNo = Regex.Match(catalog, @"/Pages\s+(\d+)\s+\d+\s+R");
            var pagesBody = pagesNo.Success ? temp.GetObjectBody(int.Parse(pagesNo.Groups[1].Value)) : null;
            var kid = Regex.Match(pagesBody ?? "", @"/Kids\s*\[\s*(\d+)");
            var pageBody = kid.Success ? temp.GetObjectBody(int.Parse(kid.Groups[1].Value)) : null;
            if (pageBody == null)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Không đọc được trang của tài liệu khối chữ ký tạm.");
            }

            var contentsM = Regex.Match(pageBody, @"/Contents\s+(\d+)\s+\d+\s+R");
            if (!contentsM.Success)
            {
                throw new UserFriendlyException(ErrorCodes.SignFailed,
                    "Trang khối chữ ký tạm không có luồng nội dung.");
            }
            var contentNo = int.Parse(contentsM.Groups[1].Value);
            var contentDict = temp.GetObjectBody(contentNo) ?? "<<>>";
            var contentData = temp.GetRawStreamBytes(contentNo) ?? Array.Empty<byte>();

            // /Resources của trang: có thể là dict nằm thẳng hoặc tham chiếu gián tiếp.
            var resIdx = pageBody.IndexOf("/Resources", StringComparison.Ordinal);
            string resourcesRaw;
            var sourceRefs = new List<int>();
            if (resIdx < 0)
            {
                resourcesRaw = "<<>>";
            }
            else
            {
                var p = resIdx + "/Resources".Length;
                while (p < pageBody.Length && char.IsWhiteSpace(pageBody[p])) p++;
                if (p < pageBody.Length && pageBody[p] == '<')
                {
                    resourcesRaw = PdfRevisionInfo.ExtractDictionary(pageBody, p);
                }
                else
                {
                    var rm = Regex.Match(pageBody[p..], @"^(\d+)\s+\d+\s+R");
                    resourcesRaw = rm.Success
                        ? temp.GetObjectBody(int.Parse(rm.Groups[1].Value)) ?? "<<>>"
                        : "<<>>";
                }
            }

            // Gom toàn bộ object mà cây tài nguyên đụng tới: font -> font descriptor -> file font nhúng.
            // Duyệt theo chiều rộng, mỗi object chỉ lấy 1 lần (cùng 1 font dùng cho cả 2 dòng chữ).
            var order = new List<int>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (var r in FindRefs(resourcesRaw)) queue.Enqueue(r);
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                if (!visited.Add(n)) continue;
                var body = temp.GetObjectBody(n);
                if (body == null) continue;
                order.Add(n);
                foreach (var r in FindRefs(body)) if (!visited.Contains(r)) queue.Enqueue(r);
            }

            // Đánh số lại: Form XObject lấy số đầu, các object tài nguyên lấy các số kế tiếp.
            var formNumber = firstObjectNumber;
            var map = new Dictionary<int, int>();
            var next = firstObjectNumber + 1;
            foreach (var n in order) map[n] = next++;

            var objects = new List<TransplantedObject>();

            // Form XObject = luồng nội dung của trang tạm khoác thêm cái vỏ XObject.
            // BBox đúng bằng khổ khối chữ ký nên nội dung nằm khít, không cần /Matrix.
            var formDict = new StringBuilder();
            formDict.Append("<</Type /XObject/Subtype /Form/FormType 1/BBox [0 0 ")
                .Append(width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(height.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
                .Append("]/Resources ").Append(Renumber(resourcesRaw, map));

            // Giữ nguyên /Filter của luồng nội dung gốc vì ta chép nguyên byte đã mã hóa.
            var filterM = Regex.Match(contentDict, @"/Filter\s*(/\w+|\[[^\]]*\])");
            if (filterM.Success) formDict.Append("/Filter ").Append(filterM.Groups[1].Value);
            formDict.Append("/Length ").Append(contentData.Length).Append(">>");

            objects.Add(new TransplantedObject
            {
                Number = formNumber,
                DictText = formDict.ToString(),
                StreamData = contentData,
            });

            foreach (var n in order)
            {
                var body = temp.GetObjectBody(n)!;
                var data = temp.GetRawStreamBytes(n);
                var dict = Renumber(body, map);

                // /Length phải khớp đúng số byte thực chép sang, không tin số cũ.
                if (data != null)
                {
                    dict = Regex.IsMatch(dict, @"/Length\s+\d+")
                        ? Regex.Replace(dict, @"/Length\s+\d+", "/Length " + data.Length)
                        : dict[..dict.LastIndexOf(">>", StringComparison.Ordinal)] + "/Length " + data.Length + ">>";
                }

                objects.Add(new TransplantedObject { Number = map[n], DictText = dict, StreamData = data });
            }

            return new AppearanceResult { Objects = objects, FormObjectNumber = formNumber };
        }

        /// <summary>Liệt kê số hiệu của mọi tham chiếu gián tiếp "N G R" trong nguyên văn một dict.</summary>
        private static IEnumerable<int> FindRefs(string dictRaw)
        {
            foreach (Match m in Regex.Matches(dictRaw, @"(\d+)\s+\d+\s+R\b"))
            {
                yield return int.Parse(m.Groups[1].Value);
            }
        }

        /// <summary>
        /// Đổi mọi "N G R" trong nguyên văn dict sang số hiệu đích theo <paramref name="map"/>, MỘT LƯỢT
        /// (Regex.Replace đi từ trái sang và không xét lại phần đã thay) — thay nhiều lượt thì số vừa đổi
        /// có thể bị đổi tiếp thành số khác.
        /// Chỉ áp dụng cho phần DICT, không bao giờ cho thân stream: dữ liệu nội dung có thể chứa dãy số
        /// trông y hệt một tham chiếu.
        /// </summary>
        private static string Renumber(string dictRaw, IReadOnlyDictionary<int, int> map)
        {
            return Regex.Replace(dictRaw, @"(\d+)(\s+\d+\s+R\b)", m =>
            {
                var n = int.Parse(m.Groups[1].Value);
                return (map.TryGetValue(n, out var t) ? t : n) + m.Groups[2].Value;
            });
        }
    }
}
