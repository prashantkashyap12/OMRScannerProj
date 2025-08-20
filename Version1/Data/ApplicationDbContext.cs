using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using SQCScanner.Modal;
using SQCScanner.Services;
using Version1.Modal;

namespace Version1.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options) :base(options) {  }
        public DbSet<ImgTemp> ImgTemplate { get; set; }
        public DbSet<EmpModel> empModels { get; set; }
        public DbSet<userAuthChecked> LoginTeken { get; set; }


        // yaha se hum code se master table ko handle kar rahe hai override kr rahe hai, 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ImgTemp>().HasKey(e => e.Id);           // as Make Primary key
            modelBuilder.Entity<EmpModel>().HasKey(e => e.EmpId);       // as Make Primary key
            modelBuilder.Entity<userAuthChecked>().HasKey(e => e.id);   // as Make Primary key
        }
    }
}
