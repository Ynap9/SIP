namespace Sip.be.Application.Document.Dtos.Check
{
    /// <summary>Kết luận verify dấu thời gian tin cậy (RFC 3161) của 1 file.</summary>
    public enum TimestampVerdict
    {
        ValidTrusted = 0,   // PDF: có token, chữ ký TSA hợp lệ, chain về Root G2, thời gian khớp (sau +7h)
        NoTimestamp = 1,    // PDF: không có token RFC 3161 -> CHẶN
        UntrustedTsa = 2,   // PDF: token có nhưng chữ ký TSA/chain không về Root G2 -> CHẶN
        TimeMismatch = 3,   // PDF: hợp lệ nhưng lệch giờ > ngưỡng -> CHẶN
        NotApplicable = 4   // File không phải PDF -> cảnh báo, KHÔNG chặn (trình check đang phát triển)
    }

    /// <summary>
    /// Kết quả kiểm tra dấu thời gian tin cậy của 1 file so với TSA Ban Cơ yếu Chính phủ.
    /// Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public class TimestampVerifyDto
    {
        /// <summary>Kết luận cuối cùng.</summary>
        public TimestampVerdict Verdict { get; set; }

        /// <summary>File có token dấu thời gian RFC 3161 hay không.</summary>
        public bool HasTimestamp { get; set; }

        /// <summary>Tên TSA (CN của cert máy chủ cấp dấu thời gian).</summary>
        public string? TimestampAuthority { get; set; }

        /// <summary>Thời gian tin cậy = genTime + 7h (quirk VGCA), "dd/MM/yyyy HH:mm:ss".</summary>
        public string? TrustedTime { get; set; }

        /// <summary>Giờ ký tự khai (signingTime), "dd/MM/yyyy HH:mm:ss".</summary>
        public string? SigningTime { get; set; }

        /// <summary>Mô tả lý do khi không hợp lệ.</summary>
        public string? Reason { get; set; }

        /// <summary>File có dấu thời gian Ban Cơ yếu thật và khớp giờ.</summary>
        public bool IsValid => Verdict == TimestampVerdict.ValidTrusted;

        /// <summary>Verdict này có CHẶN đóng gói không (PDF lỗi = chặn; hợp lệ & non-PDF = không chặn).</summary>
        public bool Blocks => Verdict != TimestampVerdict.ValidTrusted && Verdict != TimestampVerdict.NotApplicable;
    }
}
