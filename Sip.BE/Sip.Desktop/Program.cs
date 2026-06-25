using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        /// Cầu nối FE -> native:
        /// - "pickFolder": mở dialog chọn thư mục, trả "folder:&lt;path&gt;".
        /// - "openPath:&lt;path&gt;": mở thư mục/file trong Explorer.
        /// Lưu ý: chữ ký ShowOpenFolder có thể khác theo phiên bản Photino.NET -> chỉnh nếu build báo lỗi.
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
            else if (message.StartsWith("openPath:", StringComparison.Ordinal))
            {
                var path = message["openPath:".Length..];
                if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
                    {
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
