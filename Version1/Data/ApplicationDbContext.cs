using Microsoft.EntityFrameworkCore;
using SQCScanner.Modal;
using Version1.Modal;

namespace Version1.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options) :base(options) { 
        }   
        
        //public DbSet<OmrResult> OmrResultstable { get; set; }
        public DbSet<EmpModel> empModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //modelBuilder.Entity<OmrResult>().HasKey(e => e.Id);
            modelBuilder.Entity<EmpModel>().HasKey(e => e.EmpId);
        }
    }
}
