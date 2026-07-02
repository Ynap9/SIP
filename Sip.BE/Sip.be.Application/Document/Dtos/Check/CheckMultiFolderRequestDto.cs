namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Request kiểm tra NHIỀU hồ sơ cùng lúc.
    /// Đầu vào là 1 thư mục CHA chứa nhiều thư mục hồ sơ con; mỗi hồ sơ con có cấu trúc
    /// metadata/ + attachment/ giống hệt luồng kiểm tra 1 hồ sơ (CheckHoSoFolder).
    /// </summary>
    public class CheckMultiFolderRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới thư mục CHA chứa các thư mục hồ sơ con.</summary>
        public string ParentFolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Số luồng xử lý song song tối đa. Bỏ trống -> mặc định theo số nhân CPU.
        /// Đây là đòn bẩy điều tiết RAM: bộ nhớ đỉnh ~ (số luồng) × (kích thước 1 file Excel mở trong RAM),
        /// vì mỗi hồ sơ chỉ mở đúng 1 workbook rồi giải phóng ngay; file đính kèm chỉ đọc tên (không nạp nội dung).
        /// </summary>
        public int? MaxDegreeOfParallelism { get; set; }
    }
}
