using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Photino.NET;
using System.Diagnostics;

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
                .SetUseOsDefaultSize(false)
                .SetSize(1280, 820)
                .Center()
                .SetResizable(true)
                .SetDevToolsEnabled(true) // F12 / chuột phải -> Inspect để debug
                .RegisterWebMessageReceivedHandler(OnWebMessage)
                .Load(new Uri(url));

            window.WaitForClose();

            app.StopAsync().GetAwaiter().GetResult();
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
