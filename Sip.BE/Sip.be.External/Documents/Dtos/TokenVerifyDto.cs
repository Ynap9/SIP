namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Kết quả KIỂM TRA CHỨNG THƯ SỐ trước khi ký cả lô. Trả về kết quả TỪNG bước chứ không chỉ true/false:
    /// ký hỏng cả 500 file vì 1 chứng thư sai là chuyện phải chặn ngay từ file 0, và người dùng cần biết
    /// hỏng ở khâu nào để xử lý (cắm lại token / chọn cert khác / xin cert mới).
    /// </summary>
    public class TokenVerifyDto
    {
        /// <summary>Chứng thư số dùng ký được hay không = tất cả các bước dưới đều đạt.</summary>
        public bool Valid { get; set; }

        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>CN của người/tổ chức sở hữu.</summary>
        public string? CommonName { get; set; }

        /// <summary>CN của CA cấp chứng thư.</summary>
        public string? IssuerCommonName { get; set; }

        // ----- Kết quả từng bước kiểm, theo đúng thứ tự chạy -----

        /// <summary>Bước 1a: tìm thấy chứng thư theo thumbprint trong store.</summary>
        public bool FoundInStore { get; set; }

        /// <summary>Bước 1b: có khóa bí mật trên máy.</summary>
        public bool HasPrivateKey { get; set; }

        /// <summary>Bước 1c: còn trong thời hạn hiệu lực (xét theo giờ Việt Nam).</summary>
        public bool NotExpired { get; set; }

        /// <summary>Bước 1d: KeyUsage cho phép ký (digitalSignature/nonRepudiation).</summary>
        public bool AllowsSigning { get; set; }

        /// <summary>
        /// Bước 2: ký thử dữ liệu giả thành công. Đây là bằng chứng DUY NHẤT rằng token đang cắm thật
        /// và PIN dùng được — đọc metadata không bao giờ chạm tới khóa.
        /// </summary>
        public bool CanSignTest { get; set; }

        /// <summary>
        /// Bước 3: chain của chứng thư về được Root G1 hoặc Root G2 đã ghim. Không đạt thì mọi file ký ra
        /// sau đó đều bị chính verifier của SIPPACK đánh UntrustedSigner và chặn đóng gói.
        /// </summary>
        public bool TrustedChain { get; set; }

        /// <summary>Tên bước hỏng đầu tiên (rỗng nếu hợp lệ) — để FE chỉ đúng chỗ cần xử lý.</summary>
        public string? FailedCheck { get; set; }

        /// <summary>Mô tả lý do hỏng (rỗng nếu hợp lệ).</summary>
        public string? Reason { get; set; }
    }
}
