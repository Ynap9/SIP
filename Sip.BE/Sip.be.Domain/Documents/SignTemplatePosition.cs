namespace Sip.be.Domain.Documents
{
    /// <summary>
    /// Loại khối được đặt lên trang. Đánh số TƯỜNG MINH vì giá trị này đi thẳng ra JSON cho FE
    /// (enum serialize thành SỐ) - chèn thêm phần tử vào giữa là đổi nghĩa dữ liệu đã lưu trên máy người dùng.
    /// </summary>
    public enum SignTemplatePositionKind
    {
        /// <summary>Khối chữ ký số (2 dòng chữ: tên người ký + giờ ký).</summary>
        ChuKy = 0,

        /// <summary>Ảnh dấu đỏ của cơ quan.</summary>
        DauDo = 1,

        /// <summary>Ảnh chữ ký tươi (chữ ký tay đã quét).</summary>
        ChuKyTuoi = 2,
    }

    /// <summary>
    /// Toạ độ MỘT khối trên trang, lưu theo TỈ LỆ 0..1 so với khổ trang chứ không theo point/pixel - giống hệt
    /// <c>SignPositionDto</c> của luồng ký. Lý do: template được chọn một lần rồi áp cho nhiều file khác khổ
    /// giấy (A4 dọc/ngang, A3, bản quét lệch), toạ độ tuyệt đối sẽ tràn ở trang nhỏ và teo ở trang lớn.
    /// Gốc toạ độ góc TRÊN-TRÁI, Y hướng XUỐNG (hệ màn hình).
    /// </summary>
    public class SignTemplatePosition
    {
        public int Id { get; set; }

        public int SignTemplateId { get; set; }
        public SignTemplate? SignTemplate { get; set; }

        /// <summary>Khối này là chữ ký, dấu đỏ hay chữ ký tươi.</summary>
        public SignTemplatePositionKind Kind { get; set; }

        /// <summary>Trang đặt khối, tính từ 1.</summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Cạnh trái khối, theo tỉ lệ bề rộng trang (0 = sát mép trái).</summary>
        public double XRatio { get; set; }

        /// <summary>Cạnh trên khối, theo tỉ lệ chiều cao trang (0 = sát mép trên).</summary>
        public double YRatio { get; set; }

        /// <summary>Bề rộng khối, theo tỉ lệ bề rộng trang.</summary>
        public double WidthRatio { get; set; }

        /// <summary>Chiều cao khối, theo tỉ lệ chiều cao trang.</summary>
        public double HeightRatio { get; set; }
    }
}
