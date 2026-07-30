namespace Sip.be.External.Documents
{
    /// <summary>
    /// HWND của cửa sổ ứng dụng, để middleware token biết parent hộp thoại nhập PIN vào đâu.
    ///
    /// Vì sao cần: thao tác ký chạy trên luồng xử lý request của Kestrel, luồng đó KHÔNG sở hữu cửa sổ nào.
    /// CSP/KSP của token dựng hộp PIN không có cửa sổ chủ thì hộp thoại rơi ra sau cửa sổ app hoặc không hiện
    /// lên được — người dùng thấy đúng triệu chứng "cắm token mà không hỏi PIN" và không ký được.
    ///
    /// Static có chủ ý: đây là ngữ cảnh UI cấp tiến trình, không phải trạng thái theo request; còn ISigningKey
    /// thì đăng ký Scoped nên không mang được giá trị này qua các lô ký.
    /// Host desktop gán khi cửa sổ được tạo; chạy dạng API thuần thì để nguyên 0 và mọi thứ vẫn hoạt động như cũ.
    /// </summary>
    public static class SigningUiContext
    {
        public static IntPtr WindowHandle { get; set; } = IntPtr.Zero;
    }
}
