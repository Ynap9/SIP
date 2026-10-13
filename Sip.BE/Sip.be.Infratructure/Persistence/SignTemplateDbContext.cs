using Microsoft.EntityFrameworkCore;
using Sip.be.Domain.Documents;

namespace Sip.be.infratructure.Persistence
{
    /// <summary>
    /// DB RIÊNG cho template cấu hình chữ ký (sign-templates.db trong %AppData%/SipPack).
    ///
    /// Vì sao tách hẳn file thay vì thêm bảng vào <see cref="SipDbContext"/>: sip.db của máy dev được
    /// build-desktop.ps1 bundle thành seed\sip.db rồi Sip.Desktop copy sang %AppData% ở lần chạy đầu của MỌI
    /// máy cài app - template nằm trong đó sẽ theo bản cài đi khắp nơi. Tách file là bảo đảm mang tính CẤU TRÚC
    /// (cơ chế seed chỉ biết sip.db), khác với việc xoá bảng lúc build vốn chỉ là bảo đảm bằng một bước script
    /// và sẽ hỏng lặng lẽ khi ai đó sửa script.
    /// </summary>
    public class SignTemplateDbContext : DbContext
    {
        public SignTemplateDbContext(DbContextOptions<SignTemplateDbContext> options) : base(options)
        {
        }

        public DbSet<SignTemplate> SignTemplate { get; set; }
        public DbSet<SignTemplatePosition> SignTemplatePosition { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SignTemplate>(entity =>
            {
                entity.Property(e => e.TenTemplate).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Thumbprint).HasMaxLength(100).IsRequired();
                entity.Property(e => e.TenChungThu).HasMaxLength(500);
                entity.Property(e => e.LyDoKy).HasMaxLength(500);
                entity.Property(e => e.NoiKy).HasMaxLength(255);
                entity.Property(e => e.FontChuKySo).HasMaxLength(100);
                entity.Property(e => e.MauChuKySo).HasMaxLength(20);
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Xoá template thì xoá luôn toạ độ của nó - toạ độ không có nghĩa khi đứng một mình.
                entity.HasMany(e => e.Positions)
                    .WithOne(p => p.SignTemplate)
                    .HasForeignKey(p => p.SignTemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SignTemplatePosition>(entity =>
            {
                entity.Property(e => e.PageNumber).HasDefaultValue(1);
                // Lọc theo template rồi mới theo loại khối là đường truy vấn duy nhất của bảng này.
                entity.HasIndex(e => new { e.SignTemplateId, e.Kind });
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
