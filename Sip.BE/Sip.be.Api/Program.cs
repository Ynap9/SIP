using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using Sip.be.Application.Base;
using Sip.be.Application.Document.Implements;
using Sip.be.Application.Document.Interfaces;
using Sip.be.infratructure.Persistence;
using Sip.be.Shared.Constants;

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
            builder.Host.UseNLog();

            // Add services to the container.
            // AddApplicationPart: bắt buộc khi host bằng Sip.Desktop (entry assembly khác) -> MVC tìm thấy controller trong Sip.be.Api.
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(Program).Assembly);
            #region services
            builder.Services.AddScoped<IConfigService, ConfigService>();
            builder.Services.AddScoped<ICheckService, CheckService>();
            builder.Services.AddScoped<IPackService, PackService>();
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

            // Swagger để test API
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Áp dụng migration khi khởi động (tạo DB nếu chưa có) - cần cho bản desktop
            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<SipDbContext>().Database.Migrate();
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
