using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sip.be.Shared.Constants.Documents;

namespace Sip.be.infratructure.Persistence
{
    /// <summary>
    /// Chỉ phục vụ lệnh "dotnet ef migrations add" lúc phát triển. Runtime KHÔNG dùng class này - app tự đăng ký
    /// context trong Program.BuildApp. Có nó thì EF không phải dựng cả WebApplication để tìm ra context.
    /// </summary>
    public class SignTemplateDbContextFactory : IDesignTimeDbContextFactory<SignTemplateDbContext>
    {
        public SignTemplateDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<SignTemplateDbContext>()
                .UseSqlite($"Data Source={SignTemplateConstants.DatabasePath}")
                .Options;

            return new SignTemplateDbContext(options);
        }
    }
}
