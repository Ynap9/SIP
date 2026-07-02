using Sip.be.Shared.Request;

namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>
    /// Kết quả kiểm tra 1 FILE đính kèm trong hồ sơ — dùng cho bảng con per-file ở màn nhiều hồ sơ.
    /// </summary>
    public class FileCheckResultDto
    {
        /// <summary>Tên file đính kèm.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Trạng thái: 1 = Success, 0 = Error.</summary>
        public StatusCodeE Status { get; set; }

        /// <summary>Nguyên nhân lỗi (chỉ có khi file lỗi).</summary>
        public string? Reason { get; set; }
    }
}
