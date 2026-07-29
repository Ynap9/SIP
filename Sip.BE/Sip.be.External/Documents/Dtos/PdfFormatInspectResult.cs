namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Kết quả soi 1 file của <c>IPdfFormatInspector</c> — chỉ chứa SỰ THẬT phát hiện được;
    /// thông điệp hiển thị cho người dùng do tầng service dựng theo verdict.
    /// </summary>
    public class PdfFormatInspectResult
    {
        /// <summary>Kết luận cuối cùng cho file.</summary>
        public PdfFormatVerdict Verdict { get; set; }

        /// <summary>Phần PDF/A (1/2/3) đọc từ pdfaid:part; null nếu không phải PDF/A.</summary>
        public string? PdfAPart { get; set; }

        /// <summary>Mức tuân thủ PDF/A (A/B/U) đọc từ pdfaid:conformance; null nếu không có.</summary>
        public string? PdfAConformance { get; set; }

        /// <summary>Có lớp ảnh raster (image XObject) hay không.</summary>
        public bool HasImageLayer { get; set; }

        /// <summary>Có lớp text (font, thường là text OCR vô hình) hay không.</summary>
        public bool HasTextLayer { get; set; }

        /// <summary>Mô tả kỹ thuật khi ReadFailed (message của exception); không dùng để hiển thị chính.</summary>
        public string? Reason { get; set; }
    }
}
