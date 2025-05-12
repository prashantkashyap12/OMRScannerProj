using Microsoft.EntityFrameworkCore;
using SQCScanner.Modal;
using SQCScanner.Services;
using Version1.Modal;

namespace Version1.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options) :base(options) { 
        }   
        
        public DbSet<ImgTemp> OmrResult { get; set; }
        public DbSet<EmpModel> empModels { get; set; }
        public DbSet<TempRecord> TemplateRec { get; set; }
        public DbSet<ImgTemp> ImgTemplate { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ImgTemp>().HasKey(e => e.Id);
            modelBuilder.Entity<EmpModel>().HasKey(e => e.EmpId);
            modelBuilder.Entity<TempRecord>().HasKey(e => e.Id);
        }
    }
}
