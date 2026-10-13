using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Implements;
using Sip.be.Application.Document.Interfaces;
using Sip.be.External.Documents;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants;
using Sip.be.Shared.Constants.Documents;
using Sip.be.External.Documents.Interfaces;
using Sip.be.External.Documents.Implements;

namespace Sip.be.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            logger.Info("Starting application...");

            try
            {
                var app = BuildApp(args);
                app.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Application stopped because of an exception");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Dựng WebApplication (services + pipeline) - dùng chung cho bản web (Program.Main)
        /// và bản desktop (Sip.Desktop tự host trong process). Trả về app đã cấu hình, CHƯA Run.
        /// </summary>
        public static WebApplication BuildApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            Console.WriteLine("EnvironmentName => " + builder.Environment.EnvironmentName);

            builder.Logging.ClearProviders();

            // Chốt thư mục ghi log TRƯỚC khi NLog nạp cấu hình (nlog.config đọc SIPPACK_LOG_DIR).
            // Ưu tiên Log\ cạnh app vì đó là chỗ người dùng tự tìm ra được; chỉ lùi về %AppData% khi
            // thư mục đó không ghi được. Phải THỬ GHI THẬT chứ không chỉ tạo thư mục: dưới Program Files
            // lệnh tạo thư mục có thể qua nhờ ảo hoá/quyền thừa kế nhưng ghi file thì vẫn bị chặn.
            var appLogDir = Path.Combine(AppContext.BaseDirectory, "Log");
            string logDir;
            try
            {
                Directory.CreateDirectory(appLogDir);
                var probe = Path.Combine(appLogDir, ".write-test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                logDir = appLogDir;
            }
            catch
            {
                logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SipPack", "logs");
                Directory.CreateDirectory(logDir);
            }
            Environment.SetEnvironmentVariable("SIPPACK_LOG_DIR", logDir);

            builder.Host.UseNLog();

            // Add services to the container.
            // AddApplicationPart: bắt buộc khi host bằng Sip.Desktop (entry assembly khác) -> MVC tìm thấy controller trong Sip.be.Api.
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(Program).Assembly);
            #region services
            builder.Services.AddScoped<IConfigService, ConfigService>();
            builder.Services.AddScoped<ICheckService, CheckService>();
            builder.Services.AddScoped<IPackService, PackService>();
            builder.Services.AddScoped<IUnpackService, UnpackService>();
            builder.Services.AddScoped<ISignService, SignService>();
            // Template cấu hình chữ ký - CRUD trên DB riêng của máy, tách hẳn khỏi ISignService.
            builder.Services.AddScoped<ISignTemplateService, SignTemplateService>();
            // Logic phụ trợ (bên thứ 3) ở Sip.be.External - đọc/verify chữ ký số PDF, đọc chứng thư số
            builder.Services.AddScoped<IPdfSignatureReader, PdfSignatureReader>();
            builder.Services.AddScoped<ISignatureVerifier, SignatureVerifier>();
            // Soi định dạng PDF/A hai lớp cho bước gate trước khi ký (kiem-tra-dinh-dang)
            builder.Services.AddScoped<IPdfFormatInspector, PdfFormatInspector>();
            builder.Services.AddScoped<ICertificateProvider, CertificateProvider>();
            // Ký số: Scoped để cache chứng thư + handle khóa sống đúng 1 lô ký (xem ISigningKey.ClearCache).
            builder.Services.AddScoped<ISigningKey, StoreSigningKey>();
            builder.Services.AddScoped<ITimestampClient, TimestampClient>();
            builder.Services.AddScoped<IPdfSigner, PdfSigner>();
            builder.Services.AddScoped<ISignaturePlacementResolver, SignaturePlacementResolver>();
            // Đo khổ trang cho bước kiểm vị trí chữ ký trước khi ký (kiem-tra-vi-tri)
            builder.Services.AddScoped<IPdfPageInspector, PdfPageInspector>();

            #endregion
            #region mapper
            builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile));
            #endregion
            #region cors
            string allowOrigins = builder.Configuration.GetSection("AllowedHosts")!.Value!;
            Console.WriteLine($"CORS: {allowOrigins}");
            var origins = allowOrigins
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    ProgramExtensions.CorsPolicy,
                    policy =>
                    {
                        if (origins.Length == 0 || origins.Contains("*"))
                        {
                            policy.AllowAnyOrigin();
                        }
                        else
                        {
                            policy.WithOrigins(origins);
                        }

                        policy
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .WithExposedHeaders("Content-Disposition");
                    }
                );
                options.AddPolicy("SignalRPolicy", builder =>
                {
                    builder
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });
            #endregion

            // SQLite (DB local chạy trên máy cài app)
            builder.Services.AddDbContext<SipDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            // DB RIÊNG cho template cấu hình chữ ký. Đường dẫn CHỐT BẰNG CODE, KHÔNG lấy từ cấu hình và không
            // nhận từ Sip.Desktop: template là dữ liệu của riêng một máy, phải nằm ngoài thư mục cài (người
            // dùng nén zip cả thư mục mang sang máy khác) và ngoài sip.db (sip.db của máy dev bị build-desktop
            // đóng thành seed rồi copy sang mọi máy cài app). Chốt cứng ở đây thì kể cả chạy dev-mode template
            // cũng ghi vào %AppData% chứ không rơi vào repo để bị đóng gói theo.
            var signTemplatesReset = SignTemplateMachineBinding.Ensure();
            builder.Services.AddDbContext<SignTemplateDbContext>(options =>
                options.UseSqlite($"Data Source={SignTemplateConstants.DatabasePath}"));

            // Swagger để test API
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Áp dụng migration khi khởi động (tạo DB nếu chưa có) - cần cho bản desktop
            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<SipDbContext>().Database.Migrate();
                scope.ServiceProvider.GetRequiredService<SignTemplateDbContext>().Database.Migrate();
            }
            if (signTemplatesReset)
            {
                app.Logger.LogWarning("Dữ liệu template chữ ký thuộc máy khác nên đã được xoá và khởi tạo lại.");
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Serve Angular SPA (wwwroot) + API cùng origin cho bản desktop
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseCors(ProgramExtensions.CorsPolicy);

            app.UseAuthorization();

            app.MapControllers();

            // SPA fallback: route không khớp API/file tĩnh -> trả index.html
            app.MapFallbackToFile("index.html");

            return app;
        }
    }
}
