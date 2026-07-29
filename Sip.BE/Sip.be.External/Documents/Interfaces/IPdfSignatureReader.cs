using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Đọc thông tin chữ ký số nhúng trong file PDF (PAdES/PKCS#7): đã ký hay chưa + người ký + thời điểm ký.
    /// Impl ở Sip.be.External (logic phụ trợ, bên thứ 3).
    /// </summary>
    public interface IPdfSignatureReader
    {
        /// <summary>Đọc chữ ký số của 1 file. File không phải PDF -> IsSigned=false.</summary>
        SignatureInfoDto Read(string filePath);
    }
}
