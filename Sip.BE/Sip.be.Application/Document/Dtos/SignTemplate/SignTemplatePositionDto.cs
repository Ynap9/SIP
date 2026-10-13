using Sip.be.Domain.Documents;

namespace Sip.be.Application.Document.Dtos.SignTemplate
{
    /// <summary>
    /// Toạ độ MỘT khối trong template, theo TỈ LỆ 0..1 so với khổ trang - cùng hệ quy chiếu với
    /// <see cref="Sign.SignPositionDto"/> của luồng ký, để template áp thẳng sang lúc ký mà không phải quy đổi.
    /// Gốc toạ độ góc TRÊN-TRÁI, Y hướng XUỐNG (hệ màn hình).
    ///
    /// <see cref="Kind"/> dùng thẳng enum của Domain thay vì khai một bản sao ở tầng DTO: hai bản sao của cùng
    /// một bảng đánh số chắc chắn sẽ có lúc lệch nhau, mà lệch ở đây nghĩa là ảnh dấu đỏ bị vẽ vào ô chữ ký.
    /// </summary>
    public class SignTemplatePositionDto
    {
        /// <summary>Khối này là chữ ký (0), dấu đỏ (1) hay chữ ký tươi (2).</summary>
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
