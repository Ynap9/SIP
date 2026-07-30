namespace Sip.be.External.Documents.Dtos
{
    /// <summary>
    /// Một chứng thư số đọc được từ Windows certificate store.
    /// Định danh khi ký là <see cref="SerialNumber"/> (giống cách ToolSign chọn cert); kèm
    /// <see cref="Thumbprint"/> vì serial chỉ duy nhất TRONG PHẠM VI 1 issuer, không duy nhất toàn cục.
    /// </summary>
    public class SignCertDto
    {
        /// <summary>Subject đầy đủ (CN=..., O=..., OU=...).</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>CN của người/tổ chức sở hữu — dùng để hiển thị.</summary>
        public string CommonName { get; set; } = string.Empty;

        /// <summary>Issuer đầy đủ (CA cấp cert).</summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>CN của CA cấp cert — dùng để hiển thị (vd "CA phục vụ các cơ quan Nhà nước G2").</summary>
        public string IssuerCommonName { get; set; } = string.Empty;

        /// <summary>Serial (hex) — THAM SỐ CHỌN CERT khi gọi API ký.</summary>
        public string SerialNumber { get; set; } = string.Empty;

        /// <summary>Thumbprint SHA-1 (hex) — định danh duy nhất toàn cục.</summary>
        public string Thumbprint { get; set; } = string.Empty;

        /// <summary>Nguồn chứa cert.</summary>
        public CertSource Source { get; set; }

        /// <summary>Tên provider giữ private key (CSP/KSP) — cơ sở suy ra <see cref="Source"/>, giữ lại để chẩn đoán.</summary>
        public string? KeyProvider { get; set; }

        /// <summary>Hiệu lực từ, "dd/MM/yyyy HH:mm:ss".</summary>
        public string ValidFrom { get; set; } = string.Empty;

        /// <summary>Hiệu lực đến, "dd/MM/yyyy HH:mm:ss".</summary>
        public string ValidTo { get; set; } = string.Empty;

        /// <summary>Cert có private key truy cập được không (không có thì chỉ xem, không ký được).</summary>
        public bool HasPrivateKey { get; set; }

        /// <summary>Đã hết hạn/chưa tới hạn tại thời điểm gọi.</summary>
        public bool IsExpired { get; set; }

        /// <summary>KeyUsage cho phép ký (digitalSignature/nonRepudiation), hoặc cert không khai KeyUsage.</summary>
        public bool AllowsSigning { get; set; }

        /// <summary>Chain về Root G1 hoặc Root G2 đã ghim của Ban Cơ yếu (đúng phép thử mà bước ký áp dụng).</summary>
        public bool IsTrusted { get; set; }

        /// <summary>
        /// Đủ điều kiện dùng để ký = có private key + còn hạn + KeyUsage cho phép + chain về CA Ban Cơ yếu.
        /// Vế chain BẮT BUỘC có mặt ở đây: thiếu nó thì cert ngoài Ban Cơ yếu vẫn hiện "ký được", người dùng
        /// chọn xong mới bị pre-flight của StoreSigningKey chặn — hoặc tệ hơn là ký ra file mà khâu đóng gói
        /// sau đó đánh UntrustedSigner. Phép thử phải trùng khít bước ký, nếu không danh sách này nói dối.
        /// </summary>
        public bool CanSign => HasPrivateKey && !IsExpired && AllowsSigning && IsTrusted;

        /// <summary>
        /// Lý do KHÔNG ký được (null nếu ký được). Trả về thay vì lọc bỏ cert, để khi token đã cắm mà vẫn
        /// không chọn được thì người dùng thấy nguyên nhân — xem docs/token-signing-spike.md §5 (blocker
        /// "list rỗng, không rõ vì sao" từng tốn cả buổi để chẩn đoán).
        /// </summary>
        public string? Reason { get; set; }
    }
}
