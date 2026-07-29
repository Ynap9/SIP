namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// YÊU CẦU kiểm vị trí chữ ký trước khi ký: cùng bộ tọa độ sẽ gửi khi ký, nhưng không có chứng thư số vì
    /// bước này chỉ đo trên file, không chạm khóa và không sinh ra file nào.
    /// </summary>
    public class SignPositionCheckRequestDto
    {
        /// <summary>Đường dẫn tuyệt đối tới 1 file PDF hoặc 1 thư mục chứa PDF.</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Giống <see cref="SignRequestDto.UseCustomPosition"/>: false = hệ thống tự đặt vị trí.</summary>
        public bool UseCustomPosition { get; set; }

        /// <summary>Vị trí dùng chung, giống <see cref="SignRequestDto.Positions"/>.</summary>
        public List<SignPositionDto>? Positions { get; set; }

        /// <summary>Cấu hình riêng theo file, giống <see cref="SignRequestDto.FileOverrides"/>.</summary>
        public List<SignFileConfigDto>? FileOverrides { get; set; }
    }
}
