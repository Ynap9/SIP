using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Photino.NET;
using Sip.be.External.Documents;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Sip.Desktop
{
    /// <summary>
    /// Host desktop hướng C: 1 tiến trình .NET vừa chạy Kestrel (API + serve Angular SPA)
    /// vừa mở cửa sổ WebView2 (Photino) trỏ tới SPA. Không spawn process phụ.
    /// </summary>
    internal static class Program
    {
        private const string AppTitle = "SIPPACK";

        // Thư mục con cạnh exe chứa bộ cài trình điều khiển token (đi theo cả bản .zip lẫn bản cài).
        private const string DriversFolderName = "drivers";

        // ProductCode của VGCA GCA01-Client v2, đọc từ bảng Property của chính file .msi.
        // Dùng ProductCode chứ không so tên hiển thị vì tên đổi theo phiên bản/ngôn ngữ.
        private const string VgcaProductCode = "{648B71AE-68FC-45D4-B1A3-21BC86229A73}";

        // Khoá EdgeUpdate của WebView2 Evergreen.
        private const string WebView2ClientKey =
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        private const int WebView2InstallTimeoutMs = 300_000;
        private const int DriverInstallTimeoutMs = 600_000;

        [STAThread]
        private static void Main(string[] args)
        {
            // Content root = thư mục chứa exe (để tìm wwwroot khi mở app từ shortcut)
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // SQLite vào %AppData%/SipPack (không nằm trong thư mục cài -> tránh lỗi quyền ghi)
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SipPack");
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "sip.db");

            // Lần đầu chạy: nếu AppData chưa có DB mà app có kèm DB seed -> copy sang (config TT05 có sẵn).
            var seedPath = Path.Combine(AppContext.BaseDirectory, "seed", "sip.db");
            if (!File.Exists(dbPath) && File.Exists(seedPath))
            {
                File.Copy(seedPath, dbPath);
            }

            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection", $"Data Source={dbPath}");

            // Khởi động Kestrel trong process, cổng trống (port 0 -> hệ thống tự cấp cổng)
            var app = Sip.be.Api.Program.BuildApp(args);

            // Ghi hiện trạng máy NGAY sau khi NLog được cấu hình trong BuildApp, trước mọi thao tác khác:
            // lỗi token luôn xảy ra ở máy người dùng nên dữ liệu chẩn đoán phải nằm sẵn trong log.
            // Đường dẫn thật do BuildApp chốt (Log\ cạnh app, hoặc %AppData% khi chỗ đó không ghi được)
            // -> đọc lại từ biến môi trường để log ghi đúng chỗ nó đang nằm, không đoán.
            LogEnvironmentDiagnostics(
                Environment.GetEnvironmentVariable("SIPPACK_LOG_DIR") ?? "(chưa xác định)");
            app.Urls.Clear();
            app.Urls.Add("http://127.0.0.1:0");
            app.Start();

            var url = app.Services
                .GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.First();

            // WebView2 phải có TRƯỚC khi dựng cửa sổ, nếu không PhotinoWindow ném lỗi và app chết
            // câm. Bản .zip không đi qua bộ cài nên đây là đường duy nhất để máy trắng chạy được.
            EnsureWebView2Runtime();

            // Trình điều khiển USB token cài nền, không chặn việc mở cửa sổ: người dùng vẫn dùng
            // được Kiểm tra/Đóng gói/Bóc tách trong lúc chờ. Thiếu middleware thì màn Ký số báo
            // "Không đọc được chứng thư số nào" — chính là lỗi mà bước này dựng ra để tránh.
            _ = Task.Run(EnsureTokenDrivers);

            // Mở cửa sổ WebView2 trỏ tới SPA do chính Kestrel phục vụ
            var window = new PhotinoWindow()
                .SetTitle(AppTitle)
                .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", "sippack.ico"))
                .SetUseOsDefaultSize(false)
                .SetSize(1280, 820)
                .Center()
                .SetResizable(true)
                // Sàn thu nhỏ. Hẹp hơn nữa giao diện vẫn vừa (cả app tự thu nhỏ đều theo cỡ cửa sổ) nhưng
                // VÙNG CLIENT phải ở trên 992px: đó là mốc CSS chuyển sidebar sang dạng overlay, và mốc này
                // đo theo bề rộng client thật chứ không theo hệ toạ độ đã thu nhỏ - tụt xuống dưới là bố cục
                // đổi kiểu, đúng thứ cần tránh.
                // Con số ở đây là cỡ NGOÀI, đo được rộng hơn client 16px (viền cửa sổ): 1008 vừa đúng chạm
                // mốc, lấy 1024 để còn biên nếu viền dày hơn ở DPI khác.
                .SetMinSize(1024, 700)
                .SetDevToolsEnabled(true) // F12 / chuột phải -> Inspect để debug
                .RegisterWebMessageReceivedHandler(OnWebMessage)
                .RegisterWindowCreatedHandler(OnWindowCreated)
                .Load(new Uri(url));

            window.WaitForClose();

            app.StopAsync().GetAwaiter().GetResult();
        }

        // ===== Smart card subsystem (winscard) =====
        // Dùng để phân biệt "middleware chưa cài" với "Windows không thấy đầu đọc" - hai nguyên nhân
        // cho cùng một triệu chứng "cắm token mà không có chứng thư nào".
        private const uint SCARD_SCOPE_USER = 0;
        private const int SCARD_E_NO_SERVICE = unchecked((int)0x8010001D);
        private const int SCARD_E_NO_READERS_AVAILABLE = unchecked((int)0x8010002E);

        [DllImport("winscard.dll", SetLastError = true)]
        private static extern int SCardEstablishContext(
            uint dwScope, IntPtr pvReserved1, IntPtr pvReserved2, out IntPtr phContext);

        [DllImport("winscard.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SCardListReadersW")]
        private static extern int SCardListReaders(
            IntPtr hContext, string? mszGroups, char[]? mszReaders, ref uint pcchReaders);

        [DllImport("winscard.dll")]
        private static extern int SCardReleaseContext(IntPtr hContext);

        /// <summary>
        /// Ghi hiện trạng máy vào log ngay khi mở app. Sinh ra vì các lỗi liên quan tới USB token chỉ
        /// xuất hiện trên máy người dùng, nơi không có debugger và không ai đọc được stack trace — phải
        /// có sẵn dữ liệu trong file log thì mới truy được nguyên nhân từ xa.
        ///
        /// Điểm mấu chốt: chứng thư trong token KHÔNG tự có mặt trong Windows certificate store. Dịch vụ
        /// Smart Card (SCardSvr) phải chạy để thấy đầu đọc, rồi Certificate Propagation (CertPropSvc) mới
        /// chép chứng thư từ token vào store cho SIPPACK đọc. Thiếu một trong hai thì app chỉ thấy các
        /// chứng thư phần mềm sẵn có (localhost, cert dev...) — đúng triệu chứng "không đọc được token".
        /// </summary>
        private static void LogEnvironmentDiagnostics(string logDirectory)
        {
            var logger = NLog.LogManager.GetCurrentClassLogger();
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var elevated = new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

                logger.Info("===== CHẨN ĐOÁN MÔI TRƯỜNG =====");
                logger.Info($"OS={Environment.OSVersion.VersionString}, 64bit={Environment.Is64BitOperatingSystem}");
                logger.Info($"Người dùng đang chạy app: {identity.Name}, quyền admin={elevated}");
                logger.Info($"Thư mục log: {logDirectory}");
                logger.Info($"Thư mục app: {AppContext.BaseDirectory}");

                // Trạng thái khởi động 2 dịch vụ quyết định việc token lên được store hay không.
                // 2=Automatic, 3=Manual, 4=Disabled. Disabled là hỏng chắc chắn.
                foreach (var svc in new[] { "SCardSvr", "CertPropSvc" })
                {
                    using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                        .OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svc}");
                    logger.Info(key == null
                        ? $"Dịch vụ {svc}: KHÔNG TỒN TẠI trên máy này"
                        : $"Dịch vụ {svc}: Start={key.GetValue("Start")} (2=Tự động, 3=Thủ công, 4=Đã tắt)");
                }

                // Đầu đọc smart card mà Windows nhìn thấy. Middleware token đăng ký chính nó ở đây.
                var rc = SCardEstablishContext(SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out var ctx);
                if (rc == SCARD_E_NO_SERVICE)
                {
                    logger.Warn("Smart card: dịch vụ SCardSvr KHÔNG CHẠY -> Windows không thể đọc token.");
                }
                else if (rc != 0)
                {
                    logger.Warn($"Smart card: SCardEstablishContext lỗi 0x{rc:X8}");
                }
                else
                {
                    uint len = 0;
                    rc = SCardListReaders(ctx, null, null, ref len);
                    if (rc == SCARD_E_NO_READERS_AVAILABLE)
                    {
                        logger.Warn("Smart card: KHÔNG CÓ ĐẦU ĐỌC NÀO -> token chưa cắm, hoặc middleware chưa cài/chưa khởi động lại máy.");
                    }
                    else if (rc != 0)
                    {
                        logger.Warn($"Smart card: SCardListReaders lỗi 0x{rc:X8}");
                    }
                    else
                    {
                        var buffer = new char[len];
                        if (SCardListReaders(ctx, null, buffer, ref len) == 0)
                        {
                            var readers = new string(buffer, 0, (int)len)
                                .Split('\0', StringSplitOptions.RemoveEmptyEntries);
                            logger.Info($"Smart card: {readers.Length} đầu đọc -> {string.Join(" | ", readers)}");
                        }
                    }
                    SCardReleaseContext(ctx);
                }

                logger.Info("===== HẾT CHẨN ĐOÁN =====");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Không ghi được chẩn đoán môi trường.");
            }
        }

        /// <summary>
        /// Đặt tham số cấp tiến trình cho các CSP đời cũ (CAPI): hProv = NULL nghĩa là áp cho MỌI provider
        /// trong tiến trình này. Middleware token nào đăng ký khóa qua CSP chứ không qua KSP sẽ lấy HWND
        /// này làm cửa sổ chủ cho hộp thoại nhập PIN.
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptSetProvParam(IntPtr hProv, uint dwParam, ref IntPtr pbData, uint dwFlags);

        private const uint PP_CLIENT_HWND = 1;

        /// <summary>
        /// Cửa sổ native vừa dựng xong -> ghi lại HWND để middleware token parent hộp thoại PIN vào đó.
        /// Phải lấy ở đây chứ không lấy ngay sau khi tạo đối tượng PhotinoWindow: handle chỉ có thật sau
        /// khi cửa sổ native được tạo. Ký số chỉ xảy ra khi người dùng thao tác nên luôn sau thời điểm này.
        /// </summary>
        private static void OnWindowCreated(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = ((PhotinoWindow)sender!).WindowHandle;
                var logger = NLog.LogManager.GetCurrentClassLogger();
                if (hwnd == IntPtr.Zero)
                {
                    logger.Warn("OnWindowCreated: HWND = 0, hộp thoại PIN của token sẽ không có cửa sổ chủ.");
                    return;
                }

                SigningUiContext.WindowHandle = hwnd;   // nhánh CNG/KSP: đặt theo từng khóa lúc ký thử
                var capiOk = CryptSetProvParam(IntPtr.Zero, PP_CLIENT_HWND, ref hwnd, 0); // nhánh CAPI/CSP
                logger.Info($"OnWindowCreated: HWND=0x{hwnd.ToInt64():X}, PP_CLIENT_HWND={capiOk}");
            }
            catch
            {
                // Không lấy được handle thì hộp PIN vẫn có thể hiện (chỉ là không đúng lớp cửa sổ);
                // đây không phải lý do để chặn mở app.
            }
        }

        /// <summary>
        /// Cài Microsoft Edge WebView2 Runtime nếu máy chưa có. Chạy ĐỒNG BỘ vì cửa sổ Photino phụ
        /// thuộc trực tiếp vào nó — thiếu là app chết câm ngay khi dựng cửa sổ. Bootstrapper của
        /// Microsoft tự xin nâng quyền khi cần nên không ép verb runas ở đây.
        /// </summary>
        private static void EnsureWebView2Runtime()
        {
            try
            {
                // Máy 64-bit ghi khoá này ở view 32-bit (WOW6432Node); bản cài theo user thì ở HKCU.
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = hklm.OpenSubKey(WebView2ClientKey))
                {
                    if (key?.GetValue("pv") is string pv && pv.Length > 0 && pv != "0.0.0.0") return;
                }
                using (var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
                using (var key = hkcu.OpenSubKey(WebView2ClientKey))
                {
                    if (key?.GetValue("pv") is string pv && pv.Length > 0 && pv != "0.0.0.0") return;
                }

                var setup = Path.Combine(AppContext.BaseDirectory, DriversFolderName, "MicrosoftEdgeWebView2Setup.exe");
                if (!File.Exists(setup)) return;

                using var proc = Process.Start(new ProcessStartInfo(setup, "/silent /install")
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                proc?.WaitForExit(WebView2InstallTimeoutMs);
            }
            catch
            {
                // Cài hỏng thì vẫn thử mở cửa sổ: máy có thể đã có WebView2 theo đường khác
                // (Edge, Office) mà khoá EdgeUpdate không phản ánh.
            }
        }

        /// <summary>
        /// Cài ngầm middleware đọc USB token khi máy còn thiếu. Không có middleware thì SIPPACK
        /// không thấy chứng thư nào trong token — nó lấy cert qua Windows CNG/Certificate Store
        /// chứ không nói chuyện trực tiếp với token qua PKCS#11, middleware chính là cầu nối.
        /// Nhận diện bằng định danh chính xác (nhánh registry riêng / ProductCode), không so tên hiển thị.
        /// </summary>
        private static void EnsureTokenDrivers()
        {
            try
            {
                var driversDir = Path.Combine(AppContext.BaseDirectory, DriversFolderName);
                if (!Directory.Exists(driversDir)) return;

                // Người dùng đã từ chối hộp UAC một lần -> lần sau mở app không hỏi lại nữa.
                var skipMarker = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SipPack", "bo-qua-cai-driver");
                if (File.Exists(skipMarker)) return;

                // --- Bit4id Universal MW ---
                bool bit4idInstalled;
                using (var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var key = hklm32.OpenSubKey(@"SOFTWARE\bit4id"))
                {
                    bit4idInstalled = key != null && key.GetSubKeyNames().Length > 0;
                }
                if (!bit4idInstalled)
                {
                    var pf32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    bit4idInstalled = File.Exists(Path.Combine(pf32, "Bit4Id", "Universal MW", "uninst.exe"));
                }
                if (!bit4idInstalled)
                {
                    var bit4id = Directory.GetFiles(driversDir, "bit4id*.exe").FirstOrDefault();
                    // /S là cờ cài im lặng của NSIS — bộ cài gốc bit4id cũng dựng bằng NSIS.
                    if (bit4id != null) RunInstallerElevated(bit4id, "/S", skipMarker);
                }

                // --- VGCA GCA01-Client v2 ---
                var vgcaInstalled = false;
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = hklm.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + VgcaProductCode);
                    if (key != null) { vgcaInstalled = true; break; }
                }
                if (!vgcaInstalled)
                {
                    var msi = Directory.GetFiles(driversDir, "gca01-client*.msi").FirstOrDefault();
                    if (msi != null)
                    {
                        RunInstallerElevated("msiexec.exe", $"/i \"{msi}\" /qn /norestart", skipMarker);
                    }
                }

                bool tokenManagerInstalled;
                using (var hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = hklm64.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography\Defaults\Provider"))
                {
                    tokenManagerInstalled = key != null && key.GetSubKeyNames()
                        .Any(name => name.StartsWith("eToken", StringComparison.OrdinalIgnoreCase));
                }
                if (!tokenManagerInstalled)
                {
                    tokenManagerInstalled = File.Exists(Path.Combine(Environment.SystemDirectory, "eTOKCSP.dll"));
                }
                if (!tokenManagerInstalled)
                {
                    var tokenManager = Directory.GetFiles(driversDir, "VGCAv1_Installer*.exe").FirstOrDefault();
                    if (tokenManager != null) RunInstallerElevated(tokenManager, "/S", skipMarker);
                }
            }
            catch
            {
                // Cài driver là việc "nếu được thì tốt": hỏng cũng không được làm chết app, các
                // chức năng Kiểm tra/Đóng gói/Bóc tách không phụ thuộc token.
            }
        }

        /// <summary>
        /// Chạy một bộ cài với quyền admin — cài trình điều khiển bắt buộc phải nâng quyền nên
        /// Windows sẽ hiện hộp UAC. Người dùng bấm "No" thì ghi dấu để lần mở app sau không hỏi lại.
        /// </summary>
        private static void RunInstallerElevated(string fileName, string arguments, string skipMarker)
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                proc?.WaitForExit(DriverInstallTimeoutMs);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Mã 1223: người dùng từ chối nâng quyền ở hộp UAC.
                Directory.CreateDirectory(Path.GetDirectoryName(skipMarker)!);
                File.WriteAllText(skipMarker, "Nguoi dung da tu choi cai trinh dieu khien token.");
            }
        }

        /// <summary>
        /// Cầu nối FE -> native:
        /// - "pickFolder": mở dialog chọn thư mục, trả "folder:&lt;path&gt;".
        /// - "pickFile": mở dialog chọn file .zip, trả "file:&lt;path&gt;".
        /// - "openPath:&lt;path&gt;": mở thư mục/file trong Explorer.
        /// Lưu ý: chữ ký ShowOpenFolder/ShowOpenFile có thể khác theo phiên bản Photino.NET -> chỉnh nếu build báo lỗi.
        /// </summary>
        private static void OnWebMessage(object? sender, string message)
        {
            var window = (PhotinoWindow)sender!;

            if (message == "pickFolder")
            {
                var paths = window.ShowOpenFolder("Chọn thư mục", null, false);
                var path = (paths != null && paths.Length > 0) ? paths[0] : string.Empty;
                window.SendWebMessage("folder:" + path);
            }
            else if (message == "pickFile")
            {
                var filters = new (string Name, string[] Extensions)[] { ("Gói SIP (*.zip)", new[] { "zip" }) };
                var paths = window.ShowOpenFile("Chọn file gói SIP (.zip)", null, false, filters);
                var path = (paths != null && paths.Length > 0) ? paths[0] : string.Empty;
                window.SendWebMessage("file:" + path);
            }
            else if (message == "pickImage")
            {
                var filters = new (string Name, string[] Extensions)[] { ("Ảnh (*.png, *.jpg, *.jpeg)", new[] { "png", "jpg", "jpeg" }) };
                var paths = window.ShowOpenFile("Chọn ảnh", null, false, filters);
                var path = (paths != null && paths.Length > 0) ? paths[0] : string.Empty;
                window.SendWebMessage("image:" + path);
            }
            else if (message.StartsWith("openPath:", StringComparison.Ordinal))
            {
                // Chuẩn hoá separator về Windows (BE có thể trả forward-slash lẫn lộn do outputPath dạng /).
                var path = message["openPath:".Length..].Replace('/', '\\');
                if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
                {
                    // ShellExecute: thư mục -> mở Explorer; file -> mở bằng app/trình duyệt mặc định.
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
        }
    }
}
