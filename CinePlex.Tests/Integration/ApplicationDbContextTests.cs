using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Tests.Helpers;

namespace CinePlex.Tests.Integration
{
    public class ApplicationDbContextTests
    {

        [Fact]
        public async Task AddAndRetrieveMovie_SavesCorrectly()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "S", LastName = "King" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();

            var movie = new Movie
            {
                Title = "Shining", Description = "Horror film", Duration = 144,
                ReleaseDate = new DateTime(1980, 5, 23), Genre = Genre.Horror,
                DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            await ctx.SaveChangesAsync();

            var loaded = await ctx.Movies.FindAsync(movie.MovieId);
            Assert.NotNull(loaded);
            Assert.Equal("Shining", loaded!.Title);
            Assert.Equal(144, loaded.Duration);
        }

        [Fact]
        public async Task AddCinemaWithHall_NavigationPropertyLoaded()
        {
            using var ctx = TestDbContextFactory.Create();
            var cinema = new Cinema { Name = "Multiplex", City = "Warsaw", Address = "Main St 1" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();

            var hall = new Hall { Number = "A1", Capacity = 100, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            var loaded = await ctx.Halls
                .Include(h => h.Cinema)
                .FirstAsync(h => h.HallId == hall.HallId);

            Assert.NotNull(loaded.Cinema);
            Assert.Equal("Multiplex", loaded.Cinema!.Name);
        }

        [Fact]
        public async Task AddScreeningWithMovieAndHall_NavigationsWork()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "Dir" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();

            var movie = new Movie
            {
                Title = "NavTest", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action,
                DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);

            var cinema = new Cinema { Name = "C", City = "X", Address = "Y" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();

            var hall = new Hall { Number = "H", Capacity = 50, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1), TicketPrice = 25m,
                MovieId = movie.MovieId, HallId = hall.HallId
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            var loaded = await ctx.Screenings
                .Include(s => s.Movie)
                .Include(s => s.Hall)
                .FirstAsync(s => s.ScreeningId == screening.ScreeningId);

            Assert.NotNull(loaded.Movie);
            Assert.NotNull(loaded.Hall);
            Assert.Equal("NavTest", loaded.Movie!.Title);
        }

        [Fact]
        public async Task DeleteCinema_CascadesToHall()
        {
            using var ctx = TestDbContextFactory.Create();
            var cinema = new Cinema { Name = "CascadeTest", City = "Z", Address = "Z" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();

            var hall = new Hall { Number = "X1", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();
            int hallId = hall.HallId;

            ctx.Cinemas.Remove(cinema);
            await ctx.SaveChangesAsync();

            var loadedHall = await ctx.Halls.FindAsync(hallId);
            Assert.Null(loadedHall);
        }

        [Fact]
        public async Task DeleteHallWithScreening_InMemory_DoesNotThrowByDefault()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();

            var movie = new Movie
            {
                Title = "M", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action,
                DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);

            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();

            var hall = new Hall { Number = "R", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            ctx.Screenings.Add(new Screening
            {
                StartTime = DateTime.Now.AddDays(1), MovieId = movie.MovieId,
                HallId = hall.HallId
            });
            await ctx.SaveChangesAsync();

            var hallRelationship = ctx.Model.FindEntityType(typeof(Screening))!
                .GetForeignKeys()
                .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Hall));

            Assert.NotNull(hallRelationship);
            Assert.Equal(
                DeleteBehavior.Restrict,
                hallRelationship!.DeleteBehavior);
        }

        [Fact]
        public void DeleteMovieForeignKey_ConfiguredAsRestrict()
        {
            using var ctx = TestDbContextFactory.Create();
            var movieFk = ctx.Model.FindEntityType(typeof(Screening))!
                .GetForeignKeys()
                .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Movie));

            Assert.NotNull(movieFk);
            Assert.Equal(DeleteBehavior.Restrict, movieFk!.DeleteBehavior);
        }

        [Fact]
        public void DeleteScreeningForeignKeyOnReservation_ConfiguredAsRestrict()
        {
            using var ctx = TestDbContextFactory.Create();
            var fk = ctx.Model.FindEntityType(typeof(Reservation))!
                .GetForeignKeys()
                .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Screening));

            Assert.NotNull(fk);
            Assert.Equal(DeleteBehavior.Restrict, fk!.DeleteBehavior);
        }

        [Fact]
        public async Task ReservationCode_MustBeUnique_ThrowsOnDuplicate()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();
            var movie = new Movie
            {
                Title = "M", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action,
                DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();
            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1), MovieId = movie.MovieId, HallId = hall.HallId
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            ctx.Reservations.Add(new Reservation
            {
                SeatNumber = 1, ScreeningId = screening.ScreeningId,
                ReservationCode = "DUPCODE1"
            });
            await ctx.SaveChangesAsync();

            ctx.Reservations.Add(new Reservation
            {
                SeatNumber = 2, ScreeningId = screening.ScreeningId,
                ReservationCode = "DUPCODE1"
            });
            await ctx.SaveChangesAsync();
        }

        [Fact]
        public void ReservationCode_UniqueIndex_DefinedInModel()
        {
            using var ctx = TestDbContextFactory.Create();
            var entity = ctx.Model.FindEntityType(typeof(Reservation))!;
            var codeIndex = entity.GetIndexes()
                .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Reservation.ReservationCode)));
            Assert.NotNull(codeIndex);
            Assert.True(codeIndex.IsUnique);
        }

        [Fact]
        public void MovieReview_UserMovieCombo_UniqueIndex_DefinedInModel()
        {
            using var ctx = TestDbContextFactory.Create();
            var entity = ctx.Model.FindEntityType(typeof(MovieReview))!;
            var compositeIndex = entity.GetIndexes()
                .FirstOrDefault(i =>
                    i.Properties.Any(p => p.Name == nameof(MovieReview.AppUserId)) &&
                    i.Properties.Any(p => p.Name == nameof(MovieReview.MovieId)));
            Assert.NotNull(compositeIndex);
            Assert.True(compositeIndex.IsUnique);
        }

        [Fact]
        public async Task TicketPrice_SavedAndRetrievedCorrectly()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();
            var movie = new Movie
            {
                Title = "PriceTest", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1),
                TicketPrice = 29.99m,
                MovieId = movie.MovieId,
                HallId = hall.HallId
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            var loaded = await ctx.Screenings.FindAsync(screening.ScreeningId);
            Assert.Equal(29.99m, loaded!.TicketPrice);
        }

        [Fact]
        public async Task MarathonPrice_SavedAndRetrievedCorrectly()
        {
            using var ctx = TestDbContextFactory.Create();
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            var marathon = new Marathon
            {
                Name = "Marathon Price", Price = 149.50m, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(1), IsActive = true
            };
            ctx.Marathons.Add(marathon); await ctx.SaveChangesAsync();

            var loaded = await ctx.Marathons.FindAsync(marathon.MarathonId);
            Assert.Equal(149.50m, loaded!.Price);
        }

        [Fact]
        public async Task Reservation_NullAppUserId_SavesAsGuest()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();
            var movie = new Movie
            {
                Title = "Guest", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();
            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1), MovieId = movie.MovieId, HallId = hall.HallId
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            var reservation = new Reservation
            {
                SeatNumber = 1, ScreeningId = screening.ScreeningId,
                AppUserId = null, GuestEmail = "g@g.com", GuestName = "Guest"
            };
            ctx.Reservations.Add(reservation);
            await ctx.SaveChangesAsync();

            var loaded = await ctx.Reservations.FindAsync(reservation.ReservationId);
            Assert.NotNull(loaded);
            Assert.Null(loaded!.AppUserId);
        }

        [Fact]
        public async Task Screening_NullMarathonId_SavesWithoutMarathon()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();
            var movie = new Movie
            {
                Title = "StandaloneScreen", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();

            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1), MovieId = movie.MovieId,
                HallId = hall.HallId, MarathonId = null
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            var loaded = await ctx.Screenings.FindAsync(screening.ScreeningId);
            Assert.NotNull(loaded);
            Assert.Null(loaded!.MarathonId);
        }

        [Fact]
        public async Task DeleteAppUser_ReservationAppUserIdBecomesNull()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); await ctx.SaveChangesAsync();
            var movie = new Movie
            {
                Title = "SetNull", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie);
            var cinema = new Cinema { Name = "C", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); await ctx.SaveChangesAsync();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); await ctx.SaveChangesAsync();
            var screening = new Screening
            {
                StartTime = DateTime.Now.AddDays(1), MovieId = movie.MovieId, HallId = hall.HallId
            };
            ctx.Screenings.Add(screening); await ctx.SaveChangesAsync();

            var user = new AppUser
            {
                Id = "del-user", UserName = "del", Email = "del@d.com",
                FirstName = "D", LastName = "U", NormalizedUserName = "DEL",
                NormalizedEmail = "DEL@D.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            ctx.Users.Add(user); await ctx.SaveChangesAsync();

            var reservation = new Reservation
            {
                SeatNumber = 5, ScreeningId = screening.ScreeningId,
                AppUserId = "del-user", ReservationCode = "SETNULL1"
            };
            ctx.Reservations.Add(reservation); await ctx.SaveChangesAsync();

            ctx.Users.Remove(user);
            await ctx.SaveChangesAsync();

            ctx.ChangeTracker.Clear();
            var loaded = await ctx.Reservations.FindAsync(reservation.ReservationId);
            Assert.NotNull(loaded);
            Assert.Null(loaded!.AppUserId);
        }
    }
}
