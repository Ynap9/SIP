using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Đọc chứng thư số từ Windows certificate store (cert cá nhân, cert cấp máy, cert trên USB token).
    /// Impl ở Sip.be.External (logic phụ trợ, phụ thuộc nền tảng). Xem docs/plans/pdf-signing.plan.md step 2.
    /// </summary>
    public interface ICertificateProvider
    {
        /// <summary>
        /// Liệt kê MỌI cert trong store cá nhân (CurrentUser\My) và store máy (LocalMachine\My), gồm cả cert
        /// trên USB token — token được middleware (bit4id xPKI/VGCA PKI Manager) bắc cầu vào store nên đọc
        /// chung một đường, không cần PKCS#11.
        /// Trả về cả cert KHÔNG ký được, kèm cờ + <see cref="SignCertDto.Reason"/> để chẩn đoán; việc lọc do
        /// tầng service quyết định. Chỉ đọc metadata của khóa, KHÔNG dùng khóa để ký nên không bật hộp thoại PIN.
        /// Store nào mở lỗi thì bỏ qua store đó (thường LocalMachine cần quyền), không làm hỏng cả lời gọi.
        /// </summary>
        List<SignCertDto> GetCertificates();
    }
}
