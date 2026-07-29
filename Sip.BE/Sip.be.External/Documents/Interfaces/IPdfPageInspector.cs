using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Đo khổ các trang của 1 file PDF. Có riêng cổng này để tầng Application kiểm được vị trí chữ ký TRƯỚC
    /// khi ký (khối có tràn ra ngoài trang không, trang người dùng chọn có tồn tại không) mà không phải mở
    /// đường ký hay tự đọc cấu trúc PDF.
    /// </summary>
    public interface IPdfPageInspector
    {
        /// <summary>
        /// Đọc khổ mọi trang. KHÔNG ném lỗi: file hỏng/không phải PDF -> trả Readable = false, vì đây là bước
        /// kiểm tra, một file hỏng không được làm sập cả lượt kiểm của thư mục.
        /// </summary>
        PdfPageMetricsDto Read(string filePath);
    }
}
