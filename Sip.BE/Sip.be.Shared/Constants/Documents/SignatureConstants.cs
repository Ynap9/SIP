namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Hằng số cho kiểm tra dấu thời gian tin cậy (RFC 3161) của chữ ký số PDF do TSA Ban Cơ yếu Chính phủ cấp.
    /// Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public static class SignatureConstants
    {
        // Trust anchor: RootCA chuyên dùng Chính phủ G2 — SHA-256 (fingerprint DER) là NGUỒN CHÂN LÝ.
        // File cert (rootcag2.crt) chỉ là "material", đối chiếu theo pin này khi nạp (tráo file -> fail).
        public const string RootG2Sha256 = "F04BA4B459A7C9A1B971AD9B1CB833E695A80C79AEA63B174B66A70EDFE2FDB8";

        // File cert Root G2 đặt cạnh app (Sip.be.Api/rootcag2.crt -> copy ra output), gitignore, cấp out-of-band từ
        // http://ca.gov.vn/pki/pub/crt/rootcag2.crt (định dạng PEM).
        public const string RootG2FileName = "rootcag2.crt";

        // CA TRUNG GIAN dưới Root G2 (do Root G2 cấp). Ghim theo SHA-256 (fingerprint DER) như Root G2;
        // nạp vào ExtraStore khi dựng chain để cert leaf (TSA/người ký) vẫn về được Root G2 khi token/PDF
        // KHÔNG nhúng sẵn CA trung gian. Cấp out-of-band, gitignore. Xem docs/ca-trust-certificates-g2.md.
        // key = tên file cạnh app; value = SHA-256 pin (DER).
        public static readonly IReadOnlyDictionary<string, string> IntermediateCaPins =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // CA phục vụ các cơ quan Đảng G2
                ["dcscag2.crt"] = "E4B402E3CB4CACC0A20D21FA49BCE76BEB4EB47BA7FFB7A8F71C554DBE561A81",
                // CA phục vụ các cơ quan Nhà nước G2
                ["cpg2.crt"] = "D8CAB7A4AEF92E6BB2509D7C61CEE7B44F3731B2D21074963530277B366FAE4B",
            };

        // OID
        public const string OidSignatureTimeStampToken = "1.2.840.113549.1.9.16.2.14"; // id-aa-signatureTimeStampToken
        public const string OidSigningTime = "1.2.840.113549.1.9.5";                    // PKCS#9 signingTime (giờ tự khai)
        public const string OidEkuTimeStamping = "1.3.6.1.5.5.7.3.8";                    // id-kp-timeStamping

        // Quirk VGCA (đo thực 13 file): genTime trong token bị lưu LÙI 7h so với UTC thật -> +7h trước khi so sánh.
        public const int VgcaGenTimeOffsetHours = 7;

        // Ngưỡng lệch cho phép giữa signingTime và trustedTime (giây), sau khi đã +7h.
        public const int ToleranceSeconds = 300;
    }
}
