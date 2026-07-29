namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Kết luận kiểm tra ĐỊNH DẠNG 1 file trước khi ký (bước gate chạy TRƯỚC ký).
    /// Chọn verdict theo thứ tự, DỪNG ở lỗi đầu tiên; chỉ Valid mới ký được (fail-closed).
    /// </summary>
    public enum PdfFormatVerdict
    {
        Valid = 0,        // PDF/A hai lớp hợp lệ -> ký được
        NotPdf = 1,       // Không phải PDF (thiếu header %PDF-) -> chặn
        NotPdfA = 2,      // Là PDF nhưng không khai báo PDF/A (thiếu pdfaid:part) -> chặn
        NotTwoLayer = 3,  // Là PDF/A nhưng thiếu lớp ảnh scan HOẶC lớp text OCR -> chặn
        ReadFailed = 4    // Không đọc nổi file (exception) -> fail-closed, KHÔNG suy ra hợp lệ
    }
}
