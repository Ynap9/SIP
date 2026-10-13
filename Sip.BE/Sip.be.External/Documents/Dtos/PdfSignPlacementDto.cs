namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Vị trí NGƯỜI DÙNG chọn cho khối chữ ký hiển thị. Có DTO này (thay vì để code tự chọn góc trên-phải)
    /// là toàn bộ mục đích của tính năng chọn vị trí: FE đo tọa độ trên trang đang xem rồi gửi xuống.
    ///
    /// TẤT CẢ số ở đây là TỈ LỆ 0..1 so với khổ trang, KHÔNG phải point. Lý do: người dùng chỉ chọn vị trí
    /// MỘT LẦN rồi áp cho MỌI file trong thư mục, mà các file có khổ trang khác nhau (A4 dọc 595x842,
    /// A4 ngang, A3, trang scan lệch khổ...). Gửi point tuyệt đối thì cùng một ô sẽ tràn ra ngoài trang nhỏ
    /// và teo lại một góc trang lớn; gửi tỉ lệ thì khối chữ ký tự co giãn theo đúng khổ của từng trang và
    /// KHÔNG BAO GIỜ tràn (đã chặn X+Width ≤ 1 và Y+Height ≤ 1 ở tầng nhận request).
    ///
    /// Gốc tọa độ ở góc TRÊN-TRÁI trang, trục Y hướng XUỐNG — giống hệ tọa độ màn hình nên FE không phải
    /// lật trục. Bên trong <c>PdfIncrementalSigner</c> mới đổi sang hệ của PDF (gốc dưới-trái) theo
    /// /MediaBox thật của từng trang.
    /// </summary>
    public sealed class PdfSignPlacementDto
    {
        /// <summary>
        /// Trang được đóng khối này, tính từ 1. File ít trang hơn -> đóng vào trang CUỐI của chính file đó
        /// (kẹp lại), KHÔNG báo lỗi: người dùng chọn vị trí một lần cho cả lô, đánh trượt một file chỉ vì nó
        /// ngắn hơn là hỏng cả lô vì một con số không liên quan tới file đó.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Cạnh trái khối chữ ký, tính bằng tỉ lệ bề rộng trang (0 = sát mép trái).</summary>
        public double XRatio { get; set; }

        /// <summary>Cạnh trên khối chữ ký, tính bằng tỉ lệ chiều cao trang (0 = sát mép trên).</summary>
        public double YRatio { get; set; }

        /// <summary>Bề rộng khối chữ ký, tính bằng tỉ lệ bề rộng trang.</summary>
        public double WidthRatio { get; set; }

        /// <summary>Chiều cao khối chữ ký, tính bằng tỉ lệ chiều cao trang.</summary>
        public double HeightRatio { get; set; }

        public PdfSignPlacementKind Kind { get; set; }
    }
}
