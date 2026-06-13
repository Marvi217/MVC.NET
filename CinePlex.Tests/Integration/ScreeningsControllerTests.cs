using CinePlex.Areas.User.Controllers;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;
using CinePlex.Tests.Helpers;
using CinePlex.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text.Json;
using Xunit;

namespace CinePlex.Tests.Integration
{
    public class ScreeningsControllerTests : ControllerTestBase
    {

        private static (Hall hall, Movie movie, Screening screening, Cinema cinema)
            SeedData(ApplicationDbContext ctx)
        {
            var cinema = new Cinema { Name = "Cinema A", City = "Warsaw", Address = "St 1" };
            ctx.Cinemas.Add(cinema); ctx.SaveChanges();
            var hall = new Hall { Number = "1", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall);
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); ctx.SaveChanges();
            var movie = new Movie
            {
                Title = "Movie X", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie); ctx.SaveChanges();
            var screening = new Screening
            {
                MovieId = movie.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(1), TicketPrice = 20m, IsPublished = true
            };
            ctx.Screenings.Add(screening); ctx.SaveChanges();
            return (hall, movie, screening, cinema);
        }

        private static ScreeningsController BuildController(
            ApplicationDbContext ctx,
            Mock<UserManager<AppUser>>? userMock = null,
            Mock<IEmailService>? emailMock = null,
            string? userId = null,
            bool isAuthenticated = false)
        {
            userMock ??= new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            emailMock ??= new Mock<IEmailService>();
            var controller = new ScreeningsController(ctx, userMock.Object, emailMock.Object);
            SetupControllerContext(controller, userId, isAuthenticated);
            return controller;
        }

        [Fact]
        public async Task Index_NoCinemaId_NoSession_ReturnsCinemaPicker()
        {
            var ctx = TestDbContextFactory.Create();
            SeedData(ctx);
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, null, null);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("CinemaPicker", view.ViewName);
            var model = Assert.IsAssignableFrom<IEnumerable<Cinema>>(view.Model);
            Assert.NotEmpty(model);
        }

        [Fact]
        public async Task Index_WithCinemaId_ReturnsIndexViewWithRepertoireViewModel()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, cinema) = SeedData(ctx);
            var controller = BuildController(ctx);

            var result = await controller.Index(cinema.CinemaId, null, screening.StartTime.Date, null);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Null(view.ViewName); // default view name = "Index"
            Assert.IsType<RepertoireViewModel>(view.Model);
        }

        [Fact]
        public async Task Index_WithMovieIdFilter_OnlyMatchingMovieScreenings()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, movie, screening, cinema) = SeedData(ctx);

            var dir2 = new Director { FirstName = "C", LastName = "D" };
            ctx.Directors.Add(dir2); ctx.SaveChanges();
            var movie2 = new Movie
            {
                Title = "Other Movie", Description = "D2", Duration = 80,
                ReleaseDate = DateTime.Today, DirectorId = dir2.DirectorId
            };
            ctx.Movies.Add(movie2); ctx.SaveChanges();
            var screening2 = new Screening
            {
                MovieId = movie2.MovieId, HallId = hall.HallId,
                StartTime = screening.StartTime, TicketPrice = 15m, IsPublished = true
            };
            ctx.Screenings.Add(screening2); ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Index(cinema.CinemaId, movie.MovieId, screening.StartTime.Date, null);

            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<RepertoireViewModel>(view.Model);
            Assert.All(vm.Groups, g => Assert.Equal(movie.MovieId, g.Movie.MovieId));
        }

        [Fact]
        public async Task Index_WithHallTypeFilter_OnlyMatchingHallType()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, cinema) = SeedData(ctx);

            var imaxHall = new Hall { Number = "2", Capacity = 5, CinemaId = cinema.CinemaId, Type = HallType.IMAX };
            ctx.Halls.Add(imaxHall); ctx.SaveChanges();
            var dir = ctx.Directors.First();
            var movie2 = new Movie
            {
                Title = "IMAX Movie", Description = "D", Duration = 100,
                ReleaseDate = DateTime.Today, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie2); ctx.SaveChanges();
            var imaxScreening = new Screening
            {
                MovieId = movie2.MovieId, HallId = imaxHall.HallId,
                StartTime = screening.StartTime, TicketPrice = 30m, IsPublished = true
            };
            ctx.Screenings.Add(imaxScreening); ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Index(cinema.CinemaId, null, screening.StartTime.Date, HallType.IMAX);

            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<RepertoireViewModel>(view.Model);
            Assert.All(vm.Groups, g =>
                Assert.All(g.HallGroups, hg => Assert.Equal("IMAX", hg.HallTypeLabel)));
        }

        [Fact]
        public async Task Index_UnpublishedNoPublishDate_NotInResults()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, _, _, cinema) = SeedData(ctx);

            var dir = ctx.Directors.First();
            var movie2 = new Movie
            {
                Title = "Hidden Movie", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie2); ctx.SaveChanges();
            var unpublished = new Screening
            {
                MovieId = movie2.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Today.AddDays(1).AddHours(20),
                TicketPrice = 20m, IsPublished = false, PublishDate = null
            };
            ctx.Screenings.Add(unpublished); ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Index(cinema.CinemaId, null, unpublished.StartTime.Date, null);

            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<RepertoireViewModel>(view.Model);
            var allMovieIds = vm.Groups.Select(g => g.Movie.MovieId).ToList();
            Assert.DoesNotContain(movie2.MovieId, allMovieIds);
        }

        [Fact]
        public async Task Index_PublishDateInPast_AppearsInResults()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, _, _, cinema) = SeedData(ctx);

            var dir = ctx.Directors.First();
            var movie2 = new Movie
            {
                Title = "Past Published", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie2); ctx.SaveChanges();
            var screening = new Screening
            {
                MovieId = movie2.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Today.AddDays(1).AddHours(14),
                TicketPrice = 20m, IsPublished = false, PublishDate = DateTime.Now.AddHours(-1)
            };
            ctx.Screenings.Add(screening); ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Index(cinema.CinemaId, null, screening.StartTime.Date, null);

            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<RepertoireViewModel>(view.Model);
            var allMovieIds = vm.Groups.Select(g => g.Movie.MovieId).ToList();
            Assert.Contains(movie2.MovieId, allMovieIds);
        }

        [Fact]
        public async Task AvailableDays_TwoScreeningsInMonth_ReturnsBothDates()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, movie, _, cinema) = SeedData(ctx);

            var date1 = new DateTime(2026, 8, 5, 18, 0, 0);
            var date2 = new DateTime(2026, 8, 12, 20, 0, 0);
            ctx.Screenings.AddRange(
                new Screening { MovieId = movie.MovieId, HallId = hall.HallId, StartTime = date1, TicketPrice = 20m, IsPublished = true },
                new Screening { MovieId = movie.MovieId, HallId = hall.HallId, StartTime = date2, TicketPrice = 20m, IsPublished = true }
            );
            ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.AvailableDays(2026, 8, cinema.CinemaId);

            var json = Assert.IsType<JsonResult>(result);
            var dates = (json.Value as IEnumerable<string>)?.ToList();
            Assert.NotNull(dates);
            Assert.Contains("2026-08-05", dates);
            Assert.Contains("2026-08-12", dates);
        }

        [Fact]
        public async Task AvailableDays_NoScreeningsInMonth_ReturnsEmptyArray()
        {
            var ctx = TestDbContextFactory.Create();
            SeedData(ctx);
            var controller = BuildController(ctx);

            var result = await controller.AvailableDays(2025, 1, null);

            var json = Assert.IsType<JsonResult>(result);
            var dates = (json.Value as IEnumerable<string>)?.ToList();
            Assert.NotNull(dates);
            Assert.Empty(dates);
        }

        [Fact]
        public async Task AvailableDays_MarathonScreeningsExcluded()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, movie, _, cinema) = SeedData(ctx);

            var marathon = new Marathon
            {
                Name = "M", Price = 50m, HallId = hall.HallId,
                StartTime = new DateTime(2026, 9, 1), IsActive = true
            };
            ctx.Marathons.Add(marathon); ctx.SaveChanges();

            var marathonScreening = new Screening
            {
                MovieId = movie.MovieId, HallId = hall.HallId,
                StartTime = new DateTime(2026, 9, 10, 18, 0, 0),
                TicketPrice = 20m, IsPublished = true,
                MarathonId = marathon.MarathonId
            };
            ctx.Screenings.Add(marathonScreening); ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.AvailableDays(2026, 9, cinema.CinemaId);

            var json = Assert.IsType<JsonResult>(result);
            var dates = (json.Value as IEnumerable<string>)?.ToList();
            Assert.NotNull(dates);
            Assert.DoesNotContain("2026-09-10", dates);
        }

        [Fact]
        public async Task Details_ExistingScreening_ReturnsTakenSeatsExcludingCancelled()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            ctx.Reservations.AddRange(
                new Reservation { ScreeningId = screening.ScreeningId, SeatNumber = 1, Status = ReservationStatus.Confirmed, ReservationCode = "CODE0001" },
                new Reservation { ScreeningId = screening.ScreeningId, SeatNumber = 2, Status = ReservationStatus.Cancelled, ReservationCode = "CODE0002" }
            );
            ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Details(screening.ScreeningId);

            var view = Assert.IsType<ViewResult>(result);
            var taken = view.ViewData["TakenSeats"] as List<int>;
            Assert.NotNull(taken);
            Assert.Contains(1, taken);
            Assert.DoesNotContain(2, taken);
        }

        [Fact]
        public async Task Details_UnknownId_ReturnsNotFound()
        {
            var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx);

            var result = await controller.Details(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_PendingNotExpired_CountedAsTaken()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 3,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.Now.AddMinutes(10),
                ReservationCode = "PEND0001"
            });
            ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Details(screening.ScreeningId);

            var view = Assert.IsType<ViewResult>(result);
            var taken = view.ViewData["TakenSeats"] as List<int>;
            Assert.NotNull(taken);
            Assert.Contains(3, taken);
        }

        [Fact]
        public async Task Details_PendingExpired_NotCountedAsTaken()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 4,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.Now.AddMinutes(-5),
                ReservationCode = "PEND0002"
            });
            ctx.SaveChanges();

            var controller = BuildController(ctx);

            var result = await controller.Details(screening.ScreeningId);

            var view = Assert.IsType<ViewResult>(result);
            var taken = view.ViewData["TakenSeats"] as List<int>;
            Assert.NotNull(taken);
            Assert.DoesNotContain(4, taken);
        }

        [Fact]
        public async Task Book_NoSeats_RedirectsToDetails()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);
            var controller = BuildController(ctx, userId: "u1", isAuthenticated: true);

            var result = await controller.Book(screening.ScreeningId, new List<int>());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(screening.ScreeningId, redirect.RouteValues!["id"]);
        }

        [Fact]
        public async Task Book_ScreeningNotFound_ReturnsNotFound()
        {
            var ctx = TestDbContextFactory.Create();
            var user = new AppUser { Id = "u1", UserName = "test", Email = "t@t.com", FirstName = "A", LastName = "B" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(user);

            var controller = BuildController(ctx, userMock, userId: "u1", isAuthenticated: true);

            var result = await controller.Book(999, new List<int> { 1 });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Book_BlockedUser_SetsTempDataError_RedirectsDetails()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            var blockedUser = new AppUser
            {
                Id = "u-blocked", UserName = "blocked", Email = "b@b.com",
                FirstName = "B", LastName = "U", IsBlocked = true
            };
            ctx.Users.Add(blockedUser); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(blockedUser);

            var controller = BuildController(ctx, userMock, userId: "u-blocked", isAuthenticated: true);

            var result = await controller.Book(screening.ScreeningId, new List<int> { 1 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Book_SeatAlreadyTaken_SetsTempDataError_RedirectsDetails()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 5,
                Status = ReservationStatus.Confirmed, ReservationCode = "TAKEN001"
            });
            ctx.SaveChanges();

            var user = new AppUser { Id = "u2", UserName = "user2", Email = "u2@u.com", FirstName = "U", LastName = "S" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(user);

            var emailMock = new Mock<IEmailService>();
            emailMock.Setup(e => e.SendReservationConfirmedAsync(
                It.IsAny<IReadOnlyList<Reservation>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var controller = BuildController(ctx, userMock, emailMock, userId: "u2", isAuthenticated: true);

            var result = await controller.Book(screening.ScreeningId, new List<int> { 5 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Book_ValidBooking_CreatesConfirmedReservations_SetsTempDataSuccess()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, _, screening, _) = SeedData(ctx);

            var user = new AppUser { Id = "u3", UserName = "user3", Email = "u3@u.com", FirstName = "U", LastName = "R" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(user);

            var emailMock = new Mock<IEmailService>();
            emailMock.Setup(e => e.SendReservationConfirmedAsync(
                It.IsAny<IReadOnlyList<Reservation>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var controller = BuildController(ctx, userMock, emailMock, userId: "u3", isAuthenticated: true);

            var result = await controller.Book(screening.ScreeningId, new List<int> { 3, 4 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
            Assert.NotNull(controller.TempData["Success"]);
            var saved = ctx.Reservations.Where(r => r.AppUserId == "u3").ToList();
            Assert.Equal(2, saved.Count);
            Assert.All(saved, r => Assert.Equal(ReservationStatus.Confirmed, r.Status));
        }

        [Fact]
        public async Task Book_EdgeIsolationRejected_SetsTempDataError()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            for (int s = 2; s <= 10; s++)
            {
                ctx.Reservations.Add(new Reservation
                {
                    ScreeningId = screening.ScreeningId, SeatNumber = s,
                    Status = ReservationStatus.Confirmed,
                    ReservationCode = $"EDG{s:D4}"
                });
            }
            ctx.SaveChanges();

            var user = new AppUser { Id = "u4", UserName = "user4", Email = "u4@u.com", FirstName = "U", LastName = "E" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(user);

            var controller = BuildController(ctx, userMock, userId: "u4", isAuthenticated: true);

            var ctx2 = TestDbContextFactory.Create();
            var (_, _, sc2, _) = SeedData(ctx2);

            for (int s = 3; s <= 10; s++)
            {
                ctx2.Reservations.Add(new Reservation
                {
                    ScreeningId = sc2.ScreeningId, SeatNumber = s,
                    Status = ReservationStatus.Confirmed,
                    ReservationCode = $"EG2{s:D4}"
                });
            }
            ctx2.SaveChanges();

            var user2 = new AppUser { Id = "u4b", UserName = "user4b", Email = "u4b@u.com", FirstName = "U", LastName = "E" };
            ctx2.Users.Add(user2); ctx2.SaveChanges();

            var userMock2 = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock2.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                     .ReturnsAsync(user2);

            var controller2 = BuildController(ctx2, userMock2, userId: "u4b", isAuthenticated: true);

            var result2 = await controller2.Book(sc2.ScreeningId, new List<int> { 2 });

            var redirect2 = Assert.IsType<RedirectToActionResult>(result2);
            Assert.Equal("Details", redirect2.ActionName);
            Assert.NotNull(controller2.TempData["Error"]);
        }

        [Fact]
        public async Task Book_PriceMultiplier_AppliedCorrectly()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, _, screening, _) = SeedData(ctx);

            var layout = new CinePlex.Models.HallLayoutConfig
            {
                Rows = new List<CinePlex.Models.HallRowConfig>
                {
                    new() { Label = "A", SeatCount = 5, PriceMultiplier = 1.5m },
                    new() { Label = "B", SeatCount = 5, PriceMultiplier = 1.0m }
                }
            };
            hall.LayoutJson = layout.ToJson();
            ctx.Halls.Update(hall); ctx.SaveChanges();

            var user = new AppUser { Id = "u5", UserName = "user5", Email = "u5@u.com", FirstName = "U", LastName = "P" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            userMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(user);

            var emailMock = new Mock<IEmailService>();
            emailMock.Setup(e => e.SendReservationConfirmedAsync(
                It.IsAny<IReadOnlyList<Reservation>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var controller = BuildController(ctx, userMock, emailMock, userId: "u5", isAuthenticated: true);

            var result = await controller.Book(screening.ScreeningId, new List<int> { 3 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
            var reservation = ctx.Reservations.Single(r => r.AppUserId == "u5" && r.SeatNumber == 3);
            Assert.Equal(30m, reservation.PricePaid);
        }

        [Fact]
        public async Task Confirmation_ValidIdViaTempDataBookingIds_ReturnsViewWithReservations()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, _, screening, _) = SeedData(ctx);

            var res = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 7,
                Status = ReservationStatus.Confirmed, ReservationCode = "CONF0001"
            };
            ctx.Reservations.Add(res); ctx.SaveChanges();

            var controller = BuildController(ctx);
            controller.TempData["BookingIds"] = res.ReservationId.ToString();

            var result = await controller.Confirmation(res.ReservationId);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Reservation>>(view.Model);
            Assert.Contains(model, r => r.ReservationId == res.ReservationId);
        }

        [Fact]
        public async Task Confirmation_UnknownId_ReturnsNotFound()
        {
            var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx);

            var result = await controller.Confirmation(999);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
