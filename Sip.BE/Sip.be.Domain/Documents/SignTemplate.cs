using Sip.be.Shared.Interfaces;

namespace Sip.be.Domain.Documents
{
    /// <summary>
    /// Một bộ cấu hình chữ ký người dùng dựng sẵn để tái sử dụng: chọn chứng thư số, lý do/nơi ký, ảnh dấu đỏ,
    /// ảnh chữ ký tươi và toạ độ của từng khối trên trang.
    ///
    /// Dữ liệu này CHỈ THUỘC VỀ MỘT MÁY nên nằm ở DB riêng (sign-templates.db trong %AppData%/SipPack), KHÔNG
    /// nằm trong sip.db: sip.db của máy dev được build-desktop.ps1 đóng thành seed rồi copy sang máy người dùng,
    /// nên template lưu ở đó sẽ theo bản cài đi khắp nơi.
    /// </summary>
    public class SignTemplate : ISoftDeleted
    {
        public int Id { get; set; }

        /// <summary>Tên người dùng đặt để nhận ra template trong danh sách.</summary>
        public string TenTemplate { get; set; } = string.Empty;

        /// <summary>Thumbprint chứng thư số đã chọn - khoá tra cứu lại cert trong Windows Cert Store.</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>
        /// Tên chủ thể chứng thư lúc lưu template, chỉ để HIỂN THỊ lại cho người dùng nhận ra.
        /// Không dùng để tra cứu: token có thể chưa cắm nên không phải lúc nào cũng đọc được cert thật.
        /// </summary>
        public string? TenChungThu { get; set; }

        /// <summary>Lý do ký ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? LyDoKy { get; set; }

        /// <summary>Nơi ký ghi vào signature dictionary. Rỗng -> dùng mặc định của SigningConstants.</summary>
        public string? NoiKy { get; set; }

        /// <summary>
        /// Ảnh dấu đỏ ĐÃ COPY vào %AppData%/SipPack/template-assets - đây mới là file template dùng thật.
        /// Không trỏ thẳng vào file người dùng chọn vì họ đổi tên/xoá/di chuyển là template hỏng âm thầm.
        /// </summary>
        public string? AnhDauDoPath { get; set; }

        /// <summary>Đường dẫn người dùng đã chọn lúc tạo, GIỮ LẠI CHỈ ĐỂ HIỂN THỊ cho họ nhớ lấy từ đâu.</summary>
        public string? AnhDauDoNguonPath { get; set; }

        /// <summary>Ảnh chữ ký tươi đã copy vào %AppData% - xem giải thích ở <see cref="AnhDauDoPath"/>.</summary>
        public string? AnhChuKyTuoiPath { get; set; }

        /// <summary>Đường dẫn ảnh chữ ký tươi người dùng đã chọn, chỉ để hiển thị.</summary>
        public string? AnhChuKyTuoiNguonPath { get; set; }

        public string? FontChuKySo { get; set; }

        public double? CoChuKySo { get; set; }

        public string? MauChuKySo { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public DateTime? DeletedDate { get; set; }
        public bool Deleted { get; set; }

        /// <summary>Toạ độ của mọi khối trong template - khối chữ ký, ảnh dấu đỏ và ảnh chữ ký tươi.</summary>
        public List<SignTemplatePosition> Positions { get; set; } = new();
    }
}
