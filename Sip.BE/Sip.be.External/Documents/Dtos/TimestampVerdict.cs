namespace Sip.be.External.Documents.Dtos
{
    /// <summary>Kết luận verify dấu thời gian tin cậy (RFC 3161) của 1 file.</summary>
    public enum TimestampVerdict
    {
        ValidTrusted = 0,   // PDF: có token, chữ ký TSA hợp lệ, chain về Root G2, thời gian khớp
        NoTimestamp = 1,    // PDF: không có token RFC 3161 -> CHẶN
        UntrustedTsa = 2,   // PDF: token có nhưng chữ ký TSA/chain không về Root G2 -> CHẶN
        TimeMismatch = 3,   // PDF: hợp lệ nhưng lệch giờ > ngưỡng -> CHẶN
        NotApplicable = 4,   // File không phải PDF -> cảnh báo, KHÔNG chặn (trình check đang phát triển)
        UntrustedSigner = 5, // PDF: cert người ký không chain về Root G1 lẫn Root G2 đã ghim -> CHẶN
        InvalidSignature = 6 // PDF: nội dung đã bị sửa sau khi ký / có byte nằm ngoài mọi vùng ký -> CHẶN
    }
}
