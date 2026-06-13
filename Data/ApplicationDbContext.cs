using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CinePlex.Models;

namespace CinePlex.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<Director> Directors { get; set; }
        public DbSet<Cinema> Cinemas { get; set; }
        public DbSet<Hall> Halls { get; set; }
        public DbSet<Screening> Screenings { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<BarItem> BarItems { get; set; }
        public DbSet<BarSet> BarSets { get; set; }
        public DbSet<BarSetItem> BarSetItems { get; set; }
        public DbSet<Marathon> Marathons { get; set; }
        public DbSet<MovieReview> MovieReviews { get; set; }
        public DbSet<HallTypePricing> HallTypePricings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Movie>()
                .HasOne(m => m.Director)
                .WithMany(d => d.Movies)
                .HasForeignKey(m => m.DirectorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Hall>()
                .HasOne(h => h.Cinema)
                .WithMany(c => c.Halls)
                .HasForeignKey(h => h.CinemaId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Screening>()
                .HasOne(s => s.Movie)
                .WithMany(m => m.Screenings)
                .HasForeignKey(s => s.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Screening>()
                .HasOne(s => s.Hall)
                .WithMany(h => h.Screenings)
                .HasForeignKey(s => s.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Reservation>()
                .HasOne(r => r.Screening)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.ScreeningId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Reservation>()
                .HasOne(r => r.Marathon)
                .WithMany(m => m.Reservations)
                .HasForeignKey(r => r.MarathonId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Reservation>()
                .HasOne(r => r.AppUser)
                .WithMany(u => u.Reservations)
                .HasForeignKey(r => r.AppUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Screening>()
                .Property(s => s.TicketPrice)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Reservation>()
                .HasIndex(r => r.ReservationCode)
                .IsUnique();

            builder.Entity<Reservation>()
                .Property(r => r.PricePaid)
                .HasColumnType("decimal(10,2)");

            builder.Entity<BarItem>()
                .Property(b => b.Price)
                .HasColumnType("decimal(10,2)");

            builder.Entity<BarSet>()
                .Property(b => b.Price)
                .HasColumnType("decimal(10,2)");

            builder.Entity<BarSetItem>()
                .HasOne(i => i.BarSet)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.BarSetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BarSetItem>()
                .HasOne(i => i.BarItem)
                .WithMany()
                .HasForeignKey(i => i.BarItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Marathon>()
                .Property(m => m.Price)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Marathon>()
                .HasOne(m => m.Hall)
                .WithMany()
                .HasForeignKey(m => m.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Screening>()
                .HasOne(s => s.Marathon)
                .WithMany(m => m.Screenings)
                .HasForeignKey(s => s.MarathonId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MovieReview>()
                .HasIndex(r => new { r.AppUserId, r.MovieId })
                .IsUnique();

            builder.Entity<MovieReview>()
                .HasOne(r => r.AppUser)
                .WithMany()
                .HasForeignKey(r => r.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MovieReview>()
                .HasOne(r => r.Movie)
                .WithMany()
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<HallTypePricing>()
                .Property(p => p.DefaultPrice)
                .HasColumnType("decimal(10,2)");

            builder.Entity<HallTypePricing>()
                .HasIndex(p => p.HallType)
                .IsUnique();
        }
    }
}
