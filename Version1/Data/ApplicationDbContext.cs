using Microsoft.EntityFrameworkCore;
using Version1.Modal;

namespace Version1.Data
{
    public class ApplicationDbContext:DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options) :base(options) { 
        }   
        public DbSet<OmrResult> OmrResultstable { get; set; } 
    }
}
