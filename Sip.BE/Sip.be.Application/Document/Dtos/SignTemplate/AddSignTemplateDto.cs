namespace Sip.be.Application.Document.Dtos.SignTemplate
{
    /// <summary>
    /// Tham số tạo mới một template cấu hình chữ ký.
    ///
    /// Hai đường dẫn ảnh là đường dẫn TRÊN MÁY NGƯỜI DÙNG (họ chọn bằng hộp thoại chọn file). Service sẽ COPY
    /// ảnh về %AppData%/SipPack/template-assets rồi mới lưu - giữ nguyên đường dẫn gốc thì template hỏng âm thầm
    /// ngay khi người dùng đổi tên hoặc dọn thư mục ảnh.
    /// </summary>
    public class AddSignTemplateDto
    {
        /// <summary>Tên template để nhận ra trong danh sách. Bắt buộc.</summary>
        public string TenTemplate { get; set; } = string.Empty;

        /// <summary>Thumbprint chứng thư số đã chọn. Bắt buộc.</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>Tên chủ thể chứng thư, chỉ để hiển thị lại cho người dùng nhận ra.</summary>
        public string? TenChungThu { get; set; }

        /// <summary>Lý do ký. Rỗng -> lúc ký dùng mặc định của SigningConstants.</summary>
        public string? LyDoKy { get; set; }

        /// <summary>Nơi ký. Rỗng -> lúc ký dùng mặc định của SigningConstants.</summary>
        public string? NoiKy { get; set; }

        /// <summary>Đường dẫn ảnh dấu đỏ người dùng chọn. NULL = template không có dấu đỏ.</summary>
        public string? AnhDauDoPath { get; set; }

        /// <summary>Đường dẫn ảnh chữ ký tươi người dùng chọn. NULL = template không có chữ ký tươi.</summary>
        public string? AnhChuKyTuoiPath { get; set; }

        public string? FontChuKySo { get; set; }

        public double? CoChuKySo { get; set; }

        public string? MauChuKySo { get; set; }

        /// <summary>
        /// Toạ độ mọi khối: chữ ký, dấu đỏ, chữ ký tươi - gộp trong MỘT danh sách, phân biệt bằng
        /// <see cref="SignTemplatePositionDto.Kind"/>. Người dùng thả bao nhiêu khối thì gửi bấy nhiêu phần tử.
        /// </summary>
        public List<SignTemplatePositionDto> Positions { get; set; } = new();
    }
}
