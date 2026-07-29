namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Xin DẤU THỜI GIAN tin cậy (RFC 3161) từ TSA Ban Cơ yếu Chính phủ cho 1 chữ ký.
    /// Impl ở Sip.be.External (gọi mạng ra ngoài). Xem .claude/be/plans/pdf-signing-be.plan.md step 2.
    /// </summary>
    public interface ITimestampClient
    {
        /// <summary>
        /// Xin token dấu thời gian đóng lên <paramref name="signatureValue"/> (trường signature của SignerInfo —
        /// theo RFC 3161/CAdES, TSA đóng dấu lên hash của chính giá trị chữ ký, không phải lên tài liệu).
        /// Nhận mảng byte thô thay vì SignerInfo để tầng Application không phải phụ thuộc gói mật mã Pkcs.
        /// Trả về token CMS đã mã hóa DER, sẵn sàng gắn làm unsigned attribute 1.2.840.113549.1.9.16.2.14.
        /// FAIL-CLOSED: không xin được token thì ném lỗi để file đó tính là ký hỏng — TUYỆT ĐỐI không phát hành
        /// chữ ký thiếu dấu thời gian (đúng lỗi khiến ToolSign bị loại, xem docs/toolsign-integration.md §7).
        /// </summary>
        Task<byte[]> RequestTokenAsync(byte[] signatureValue, CancellationToken cancellationToken = default);
    }
}
