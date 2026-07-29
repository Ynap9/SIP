using Sip.be.External.Documents.Dtos;

namespace Sip.be.External.Documents.Interfaces
{
    /// <summary>
    /// Khóa ký: tra chứng thư số, kiểm tra trước khi ký, ký dữ liệu thô, và theo dõi token còn cắm hay không.
    /// Impl ở Sip.be.External (phụ thuộc nền tảng: Windows cert store + CNG).
    /// Nguồn khóa nằm SAU interface này để câu hỏi CNG-hay-PKCS#11 (docs/pdf-signing-readiness.md §4)
    /// không rò rỉ sang code dựng PDF/CMS. Xem .claude/be/plans/pdf-signing-be.plan.md step 3.
    ///
    /// VỀ MÃ PIN: code này KHÔNG BAO GIỜ đọc, truyền hay lưu chuỗi PIN. Hộp thoại PIN do middleware của token
    /// (bit4id xPKI / VGCA PKI Manager) bật lên khi khóa bí mật được dùng lần đầu. Ta chỉ giữ HANDLE khóa sống
    /// suốt lô ký — middleware tự cache PIN theo handle đó, nên người dùng nhập PIN đúng 1 lần ở bước kiểm tra
    /// chứng thư rồi ký cả lô không bị hỏi lại. ClearCache() nhả handle -> lần chạy sau hỏi PIN lại.
    /// </summary>
    public interface ISigningKey
    {
        /// <summary>
        /// Kiểm tra chứng thư số TRƯỚC khi ký cả lô, trả kết quả từng bước:
        /// tìm thấy -> có khóa bí mật -> còn hạn -> KeyUsage cho ký -> KÝ THỬ được -> chain về CA Ban Cơ yếu.
        /// Bước "ký thử" chính là chỗ hộp thoại PIN bật lên (lần duy nhất trong lô).
        /// Kết quả + handle khóa được CACHE theo thumbprint: các bước nặng chỉ chạy 1 lần cho cả lô,
        /// không lặp theo từng file. Gọi lại trong cùng lô trả về bản cache.
        /// Không ném lỗi: mọi thất bại nằm trong DTO để người dùng biết hỏng ở khâu nào.
        /// </summary>
        TokenVerifyDto Verify(string thumbprint);

        /// <summary>
        /// Ký <paramref name="data"/> (SHA-256; RSA dùng PKCS#1 v1.5). Dùng lại handle khóa đã cache nên
        /// KHÔNG bật lại hộp thoại PIN. Ném lỗi nếu không ký được (token bị rút, PIN bị hủy...).
        /// </summary>
        byte[] SignData(string thumbprint, byte[] data);

        /// <summary>
        /// Token còn cắm không — probe RẺ cho job giám sát chạy song song trong lúc ký.
        /// Quét store TƯƠI (bỏ qua cache, vì cache chính là thứ cần kiểm chứng) và KHÔNG chạm khóa bí mật,
        /// nên gọi lặp lại liên tục cũng không bật hộp thoại PIN.
        /// Rút token khỏi máy -> cert biến mất khỏi store -> trả false.
        /// </summary>
        bool IsTokenPresent(string thumbprint);

        /// <summary>
        /// Xóa cache (kết quả kiểm tra + chứng thư) và DISPOSE handle khóa, nhả phiên PIN của middleware.
        /// BẮT BUỘC gọi khi kết thúc lô ký (finally) và ngay khi phát hiện token bị rút.
        /// Nhờ vậy lần chạy sau — kể cả khi người dùng vừa cắm lại token — luôn phải verify lại và nhập PIN lại
        /// thay vì tin vào kết quả cũ.
        /// </summary>
        void ClearCache();
    }
}
