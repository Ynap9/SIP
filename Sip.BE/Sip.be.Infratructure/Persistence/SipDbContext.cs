using Microsoft.EntityFrameworkCore;
using Sip.be.Domain.Document;
using Sip.be.Domain.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Sip.be.infratructure.Persistence
{
    public class SipDbContext: DbContext
    {
        public SipDbContext(DbContextOptions<SipDbContext> options) : base(options)
        {
            
        }
        public DbSet<ConfigHoSo> ConfigHoSo { get; set; }
        public DbSet<ConfigVanBan> ConfigVanBan { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfigHoSo>(entity =>
            {
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

            });
            modelBuilder.Entity<ConfigVanBan>(entity =>
            {
                entity.Property(e => e.Deleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
            base.OnModelCreating(modelBuilder);


        }
    }
}
