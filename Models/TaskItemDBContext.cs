using Microsoft.EntityFrameworkCore;

namespace taskAPI.Models
{
    public class TaskItemDBContext:DbContext
    {

        public TaskItemDBContext(DbContextOptions<TaskItemDBContext> options)
            : base(options)
        {
        }

        public DbSet<TaskItem> TaskItems { get; set; } = null!;
    }
}
