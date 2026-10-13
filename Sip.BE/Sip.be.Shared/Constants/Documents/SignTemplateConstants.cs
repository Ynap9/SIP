namespace Sip.be.Shared.Constants.Documents
{
    /// <summary>
    /// Hằng số cho luồng TEMPLATE CẤU HÌNH CHỮ KÝ: chỗ lưu dữ liệu, chỗ lưu ảnh, file PDF mẫu.
    /// </summary>
    public static class SignTemplateConstants
    {
        // ===== Chỗ lưu =====
        // Template là dữ liệu RIÊNG CỦA MỘT MÁY. Nó phải nằm ngoài thư mục cài và ngoài sip.db, vì:
        //  - thư mục cài bị người dùng nén zip mang sang máy khác;
        //  - sip.db của máy dev được build-desktop.ps1 đóng thành seed\sip.db rồi copy sang MỌI máy cài app.
        // Đường dẫn được chốt bằng code (không qua cấu hình) nên dev-mode cũng ghi vào %AppData%, không rơi
        // vào repo để rồi bị đóng gói theo.
        public const string AppDataFolderName = "SipPack";
        public const string DatabaseFileName = "sign-templates.db";
        public const string MachineBindingFileName = "sign-templates.machine";
        public const string MachineGuidRegistryPath = @"SOFTWARE\Microsoft\Cryptography";
        public const string MachineGuidValueName = "MachineGuid";

        /// <summary>Thư mục chứa ảnh dấu đỏ / chữ ký tươi đã copy về, mỗi template một thư mục con theo Id.</summary>
        public const string AssetFolderName = "template-assets";

        // ===== File PDF mẫu =====
        // Asset CHỈ ĐỌC nằm cạnh app -> resolve từ AppContext.BaseDirectory là đúng (chỉ đường dẫn GHI ĐƯỢC
        // mới không được suy từ BaseDirectory, xem docs/bug/nlog-path-unwritable-in-installed-app.md).
        public const string SamplePdfRelativePath = @"Templates\sample\file-mau-ky-so.pdf";

        // ===== Ảnh =====
        /// <summary>Đuôi ảnh cho phép dùng làm dấu đỏ / chữ ký tươi.</summary>
        public static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg" };

        /// <summary>Trần dung lượng một ảnh (5 MB) - ảnh dấu/chữ ký chỉ vài chục KB, to hơn mức này là chọn nhầm file.</summary>
        public const long MaxImageBytes = 5 * 1024 * 1024;

        // ===== Phân trang =====
        public const int DefaultPageNumber = 1;
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 200;

        /// <summary>Đường dẫn tuyệt đối tới thư mục dữ liệu của app trong %AppData%.</summary>
        public static string AppDataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);

        /// <summary>Đường dẫn tuyệt đối tới file DB template.</summary>
        public static string DatabasePath => Path.Combine(AppDataDirectory, DatabaseFileName);

        public static string MachineBindingPath => Path.Combine(AppDataDirectory, MachineBindingFileName);

        /// <summary>Đường dẫn tuyệt đối tới thư mục ảnh của template.</summary>
        public static string AssetDirectory => Path.Combine(AppDataDirectory, AssetFolderName);

        /// <summary>Đường dẫn tuyệt đối tới file PDF mẫu đi kèm app.</summary>
        public static string SamplePdfPath => Path.Combine(AppContext.BaseDirectory, SamplePdfRelativePath);
    }
}
