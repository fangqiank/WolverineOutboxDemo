using Microsoft.EntityFrameworkCore;
using WolverineOutboxDemo.Api.Models;
using WolverineOutboxDemo.Api.Sagas;

namespace WolverineOutboxDemo.Api.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options): DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<MessageHistory> MessageHistories => Set<MessageHistory>();
        public DbSet<UserRegistrationSaga> RegistrationSagas => Set<UserRegistrationSaga>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<MessageHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MessageType).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Direction).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.HasIndex(e => e.CorrelationId);
                entity.HasIndex(e => e.Timestamp);
            });

            modelBuilder.Entity<UserRegistrationSaga>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
                entity.ToTable("user_registration_sagas");
            });
        }
    }
}
