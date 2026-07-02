namespace Sip.be.Application.Document.Dtos.Unpack
{
    /// <summary>
    /// Request dịch ngược 1 gói SIP (file .zip HOẶC thư mục gói đã giải nén) về cấu trúc thư mục hồ sơ
    /// (metadata/ chứa Excel Ho_so+Van_ban, attachment/ chứa tài liệu) để nạp lại vào Check/Pack.
    /// </summary>
    public class UnpackRequestDto
    {
        /// <summary>Đường dẫn tới file .zip HOẶC thư mục gói SIP đã giải nén (BE tự nhận diện).</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Thư mục gốc để ghi hồ sơ dựng lại (bắt buộc). Kết quả nằm trong OutputPath/{mã hồ sơ}/.</summary>
        public string OutputPath { get; set; } = string.Empty;
    }
}
