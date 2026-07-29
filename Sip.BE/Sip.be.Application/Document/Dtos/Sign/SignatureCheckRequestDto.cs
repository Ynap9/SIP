namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Yêu cầu kiểm tra chữ ký số + dấu thời gian của MỘT file (không cần cấu trúc hồ sơ).
    /// </summary>
    public class SignatureCheckRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới file cần kiểm tra (không nhận thư mục).</summary>
        public string FilePath { get; set; } = string.Empty;
    }
}
