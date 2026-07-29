namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Cấu hình vị trí chữ ký RIÊNG cho một file, ghi đè <see cref="SignRequestDto.Positions"/> chung.
    /// Có DTO này để người dùng đặt chữ ký khác nhau cho từng file mà vẫn chỉ gọi MỘT request ký cho cả lô:
    /// file nào có mặt ở đây thì theo cấu hình riêng, file còn lại theo cấu hình chung.
    /// </summary>
    public class SignFileConfigDto
    {
        /// <summary>Đường dẫn TUYỆT ĐỐI của file PDF được cấu hình riêng (khớp đúng file trong thư mục nguồn).</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Các khối chữ ký của riêng file này, thay hoàn toàn cấu hình chung. Rỗng = quay về vị trí MẶC ĐỊNH
        /// (góc trên-phải trang 1) chứ không phải bỏ ký file.
        /// </summary>
        public List<SignPositionDto> Positions { get; set; } = new();
    }
}
