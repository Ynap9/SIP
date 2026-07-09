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
