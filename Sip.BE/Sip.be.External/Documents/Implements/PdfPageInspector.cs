using PdfSharp.Pdf.IO;
using Sip.be.External.Documents.Dtos;
using Sip.be.External.Documents.Interfaces;

namespace Sip.be.External.Documents.Implements
{
    /// <inheritdoc/>
    public class PdfPageInspector : IPdfPageInspector
    {
        /// <inheritdoc/>
        public PdfPageMetricsDto Read(string filePath)
        {
            var result = new PdfPageMetricsDto();
            try
            {
                // Import: chỉ đọc, KHÔNG cho sửa và không ghi lại gì -> file gốc (kể cả file đã có chữ ký) an
                // toàn tuyệt đối. Không dùng InformationOnly: PDFsharp đánh dấu obsolete vì chế độ đó không
                // đọc nổi cây trang, mở ra là Pages rỗng và mọi file đều bị chấm "không đọc được".
                using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                foreach (var page in document.Pages)
                {
                    // Width/Height của PDFsharp đã tính sẵn theo /MediaBox (kể cả trang thừa kế của node cha)
                    // và đã đổi theo /Rotate, đúng khổ mà người dùng nhìn thấy khi mở file.
                    result.Pages.Add(new PdfRectDto
                    {
                        X = 0,
                        Y = 0,
                        Width = page.Width.Point,
                        Height = page.Height.Point
                    });
                }
                result.Readable = result.Pages.Count > 0;
            }
            catch
            {
                // Nuốt lỗi có chủ đích: hợp đồng của cổng này là "đo được thì trả, không thì báo không đọc
                // được", còn quyết định coi file đó ra sao là việc của tầng gọi.
                result.Readable = false;
                result.Pages.Clear();
            }
            return result;
        }
    }
}
