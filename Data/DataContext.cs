using Microsoft.EntityFrameworkCore;
using VideoAppBackend.Models;

namespace VideoAppBackend.Data
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        public DbSet<Video> Videos { get; set; }
    }
}
