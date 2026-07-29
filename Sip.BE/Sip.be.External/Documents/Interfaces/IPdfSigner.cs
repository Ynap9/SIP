using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Ký số 1 file PDF theo chuẩn PAdES (SubFilter ETSI.CAdES.detached) kèm dấu thời gian RFC 3161,
    /// và vẽ khối chữ ký hiển thị trên trang.
    /// Impl ở Sip.be.External: container do SIPPACK tự dựng (nối bản), phần vẽ dùng PDFsharp, phần mật mã
    /// dùng BCL Pkcs. Xem .claude/be/plans/pdf-incremental-signing.plan.md.
    /// </summary>
    public interface IPdfSigner
    {
        /// <summary>
        /// Đọc <paramref name="inputPath"/>, ký, ghi ra <paramref name="outputPath"/>. KHÔNG sửa file gốc.
        /// Luôn ký theo kiểu NỐI BẢN: byte của file gốc được giữ nguyên si và bản ký mới ghi thêm vào cuối,
        /// nên chữ ký có sẵn (nếu có) vẫn phủ đúng vùng byte nó đã ký và không bị vô hiệu.
        /// Ném lỗi nếu ký hỏng (không xin được dấu thời gian, token bị rút...) để tầng trên tính file đó
        /// là failed rồi đi tiếp — không được nuốt lỗi và tạo ra file "ký hụt".
        ///
        /// Vị trí khối chữ ký: <see cref="PdfSignOptionsDto.Placements"/> null/rỗng -> góc TRÊN-PHẢI trang 1
        /// như cũ; có giá trị -> đặt đúng từng tọa độ người dùng chọn, bao nhiêu khối cũng được (ném lỗi nếu
        /// trang không tồn tại hoặc khối tràn ra ngoài trang — đặt lệch còn tệ hơn báo lỗi, vì file đã ký rồi
        /// thì không sửa lại được).
        /// </summary>
        Task SignAsync(string inputPath, string outputPath, PdfSignOptionsDto options,
            CancellationToken cancellationToken = default);
    }
}
