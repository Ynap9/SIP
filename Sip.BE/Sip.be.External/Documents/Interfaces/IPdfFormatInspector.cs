using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Soi ĐỊNH DẠNG 1 file trước khi ký: có phải PDF không, có khai báo PDF/A không, có đủ 2 lớp
    /// (ảnh scan + text OCR) không. Đây là bước GATE chạy TRƯỚC ký (chuẩn TT05: tài liệu chữ = two-layer PDF/A).
    /// Impl ở Sip.be.External (đọc/parse PDF cấp thấp). Xem .claude/be/plans/pdfa-precheck-be.plan.md.
    /// </summary>
    public interface IPdfFormatInspector
    {
        /// <summary>
        /// Kiểm 1 file, trả verdict theo thứ tự dừng-ở-lỗi-đầu-tiên: NotPdf -> NotPdfA -> NotTwoLayer -> Valid.
        /// Fail-closed: mọi lỗi đọc được NUỐT và trả ReadFailed (KHÔNG bao giờ ném ra ngoài, KHÔNG suy ra hợp lệ).
        /// </summary>
        PdfFormatInspectResult Inspect(string filePath);
    }
}
