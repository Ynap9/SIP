namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Khổ của MỌI trang trong 1 file PDF, đọc riêng cho việc kiểm vị trí chữ ký trước khi ký.
    /// Không dùng lại đường của bộ ký: ở đây chỉ cần đo, không đụng gì tới byte của file.
    /// </summary>
    public sealed class PdfPageMetricsDto
    {
        /// <summary>Đọc được file hay không. False = file hỏng/không phải PDF, mọi số bên dưới vô nghĩa.</summary>
        public bool Readable { get; set; }

        /// <summary>Khổ từng trang theo thứ tự đọc (point). Số phần tử = số trang.</summary>
        public List<PdfRectDto> Pages { get; set; } = new();
    }
}
