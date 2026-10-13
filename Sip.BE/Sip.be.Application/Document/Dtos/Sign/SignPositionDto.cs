using Sip.be.External.Documents.Dtos;

namespace Sip.be.Application.Document.Dtos.Sign
{
    /// <summary>
    /// Vị trí khối chữ ký do NGƯỜI DÙNG chọn trên FE, chỉ có nghĩa khi
    /// <see cref="SignRequestDto.UseCustomPosition"/> = true.
    ///
    /// TẤT CẢ giá trị là TỈ LỆ 0..1 so với khổ trang, KHÔNG phải pixel và cũng không phải point:
    /// người dùng chỉ chọn MỘT LẦN rồi áp cho MỌI file trong thư mục, mà các file khác khổ giấy nhau.
    /// FE lấy tỉ lệ = (tọa độ pixel trên khung xem) / (kích thước pixel của khung xem) nên không phụ thuộc
    /// tỉ lệ phóng đang hiển thị. Gốc tọa độ ở góc TRÊN-TRÁI trang, Y hướng XUỐNG (giống hệ màn hình).
    ///
    /// Ràng buộc: 0 ≤ XRatio, 0 ≤ YRatio, WidthRatio &gt; 0, HeightRatio &gt; 0,
    /// XRatio + WidthRatio ≤ 1 và YRatio + HeightRatio ≤ 1 — nhờ đó khối chữ ký KHÔNG BAO GIỜ tràn ra ngoài
    /// trang, dù trang to hay nhỏ.
    /// </summary>
    public class SignPositionDto
    {
        // Không có số thứ tự khối: SignRequestDto.Positions là một DANH SÁCH, mỗi phần tử một khối.

        /// <summary>
        /// Trang được đóng khối này, tính từ 1 — đúng trang người dùng đã thả khối vào khi xem trước.
        /// File có ít trang hơn -> đóng vào trang CUỐI của file đó, KHÔNG báo lỗi và KHÔNG bỏ qua file.
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>Cạnh trái khối chữ ký, theo tỉ lệ bề rộng trang (0 = sát mép trái).</summary>
        public double XRatio { get; set; }

        /// <summary>Cạnh trên khối chữ ký, theo tỉ lệ chiều cao trang (0 = sát mép trên).</summary>
        public double YRatio { get; set; }

        /// <summary>Bề rộng khối chữ ký, theo tỉ lệ bề rộng trang.</summary>
        public double WidthRatio { get; set; }

        /// <summary>Chiều cao khối chữ ký, theo tỉ lệ chiều cao trang.</summary>
        public double HeightRatio { get; set; }

        public PdfSignPlacementKind Kind { get; set; }
    }
}
