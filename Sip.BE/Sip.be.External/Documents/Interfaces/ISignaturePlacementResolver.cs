using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Chốt VỊ TRÍ + KÍCH THƯỚC khối chữ ký hiển thị cho MỘT trang PDF cụ thể.
    ///
    /// Vì sao tách riêng khỏi bộ ký: người dùng chỉ chọn vị trí MỘT LẦN rồi áp cho cả thư mục, mà mỗi file
    /// một khổ trang. Toàn bộ phép quy đổi tỉ lệ -> point và co giãn khối chữ ký theo khổ trang nằm gọn ở
    /// đây và chạy LẠI cho từng trang của từng file — thay vì tính một lần rồi áp cứng một ô point cho mọi
    /// file (chính là cách làm tràn trang nhỏ và teo trên trang lớn).
    /// Hàm THUẦN: chỉ ăn số liệu khổ trang, không đọc file, không phụ thuộc PDFsharp nên test được bằng số
    /// liệu tay.
    /// </summary>
    public interface ISignaturePlacementResolver
    {
        PdfRectDto Resolve(PdfRectDto pageBox, PdfSignPlacementDto? placement, double width, double height);
    }
}
