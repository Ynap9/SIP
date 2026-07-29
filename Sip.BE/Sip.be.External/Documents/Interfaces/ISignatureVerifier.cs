using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Verify DẤU THỜI GIAN tin cậy (RFC 3161) của file PDF so với TSA Ban Cơ yếu Chính phủ.
    /// Impl ở Sip.be.External (logic phụ trợ, bên thứ 3). Xem docs/verify-signature-timestamp.md.
    /// </summary>
    public interface ISignatureVerifier
    {
        /// <summary>Verify 1 file. Non-PDF -> NotApplicable; PDF không/không hợp lệ dấu thời gian -> verdict chặn.</summary>
        TimestampVerifyDto Verify(string filePath);
    }
}
