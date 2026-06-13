using CinePlex.Areas.User.Controllers;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Tests.Helpers;
using CinePlex.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CinePlex.Tests.Integration
{
    public class ReservationsControllerTests : ControllerTestBase
    {

        private static (Hall hall, Screening screening, Cinema cinema)
            SeedScreeningData(ApplicationDbContext ctx)
        {
            var cinema = new Cinema { Name = "Cinema B", City = "Krakow", Address = "Main 1" };
            ctx.Cinemas.Add(cinema); ctx.SaveChanges();
            var hall = new Hall { Number = "1", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall);
            var dir = new Director { FirstName = "X", LastName = "Y" };
            ctx.Directors.Add(dir); ctx.SaveChanges();
            var movie = new Movie
            {
                Title = "Test Film", Description = "Desc", Duration = 90,
                ReleaseDate = DateTime.Today, DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie); ctx.SaveChanges();
            var screening = new Screening
            {
                MovieId = movie.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(2), TicketPrice = 25m, IsPublished = true
            };
            ctx.Screenings.Add(screening); ctx.SaveChanges();
            return (hall, screening, cinema);
        }

        private static ReservationsController BuildController(
            ApplicationDbContext ctx,
            Mock<UserManager<AppUser>> userMock,
            string? userId = null,
            bool isAuthenticated = true)
        {
            var controller = new ReservationsController(ctx, userMock.Object);
            SetupControllerContext(controller, userId, isAuthenticated);
            return controller;
        }

        private static Mock<UserManager<AppUser>> UserMockFor(AppUser? user)
        {
            var mock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            mock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(user);
            return mock;
        }

        [Fact]
        public async Task Index_AuthenticatedUserWithReservations_ReturnsThreeItemsSortedByEventDate()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, screening, _) = SeedScreeningData(ctx);

            const string userId = "uid-1";
            var user = new AppUser { Id = userId, UserName = "alice", Email = "a@a.com", FirstName = "A", LastName = "L" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var s2 = new Screening
            {
                MovieId = screening.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(5), TicketPrice = 25m, IsPublished = true
            };
            ctx.Screenings.Add(s2); ctx.SaveChanges();

            ctx.Reservations.AddRange(
                new Reservation { ScreeningId = screening.ScreeningId, AppUserId = userId, SeatNumber = 1, ReservationCode = "RES00001" },
                new Reservation { ScreeningId = s2.ScreeningId,        AppUserId = userId, SeatNumber = 2, ReservationCode = "RES00002" }
            );

            var marathon = new Marathon
            {
                Name = "M", Price = 50m, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(3), IsActive = true
            };
            ctx.Marathons.Add(marathon); ctx.SaveChanges();
            ctx.Reservations.Add(new Reservation
            {
                ReservationType = ReservationType.Marathon,
                MarathonId = marathon.MarathonId, AppUserId = userId,
                SeatNumber = 1, SeatRow = "A", PricePaid = 50m, ReservationCode = "MARA0001"
            });
            ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), userId);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var items = Assert.IsAssignableFrom<List<BookingHistoryItem>>(view.Model);
            Assert.Equal(3, items.Count);
            for (int i = 0; i < items.Count - 1; i++)
                Assert.True(items[i].EventDate >= items[i + 1].EventDate);
        }

        [Fact]
        public async Task Index_EmptyReservations_ReturnsEmptyList()
        {
            var ctx = TestDbContextFactory.Create();
            SeedScreeningData(ctx);

            const string userId = "uid-empty";
            var user = new AppUser { Id = userId, UserName = "empty", Email = "e@e.com", FirstName = "E", LastName = "U" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), userId);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var items = Assert.IsAssignableFrom<List<BookingHistoryItem>>(view.Model);
            Assert.Empty(items);
        }

        [Fact]
        public async Task Index_UnauthenticatedUser_ReturnsUnauthorized()
        {
            var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx, UserMockFor(null), userId: null, isAuthenticated: false);

            var result = await controller.Index();

            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task Details_Owner_ReturnsViewResult()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            const string userId = "uid-owner";
            var user = new AppUser { Id = userId, UserName = "owner", Email = "o@o.com", FirstName = "O", LastName = "W" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, AppUserId = userId,
                SeatNumber = 3, ReservationCode = "OWN00001"
            };
            ctx.Reservations.Add(reservation); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), userId);

            var result = await controller.Details(reservation.ReservationId);

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Details_NotOwner_ReturnsForbid()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            var owner = new AppUser { Id = "uid-own2", UserName = "owner2", Email = "o2@o.com", FirstName = "O", LastName = "T" };
            var other = new AppUser { Id = "uid-oth", UserName = "other", Email = "oth@o.com", FirstName = "O", LastName = "H" };
            ctx.Users.AddRange(owner, other); ctx.SaveChanges();

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, AppUserId = owner.Id,
                SeatNumber = 4, ReservationCode = "NOTOW001"
            };
            ctx.Reservations.Add(reservation); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(other), other.Id);

            var result = await controller.Details(reservation.ReservationId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Details_NotFound_ReturnsNotFound()
        {
            var ctx = TestDbContextFactory.Create();
            var user = new AppUser { Id = "uid-nf", UserName = "nf", Email = "nf@nf.com", FirstName = "N", LastName = "F" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), user.Id);

            var result = await controller.Details(99999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Cancel_SuccessfulCancellation_StatusCancelled_RedirectsIndex()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            const string userId = "uid-cancel";
            var user = new AppUser { Id = userId, UserName = "canceler", Email = "c@c.com", FirstName = "C", LastName = "A" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, AppUserId = userId,
                SeatNumber = 5, Status = ReservationStatus.Confirmed,
                ReservationCode = "CAN00001"
            };
            ctx.Reservations.Add(reservation); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), userId);

            var result = await controller.Cancel(reservation.ReservationId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            var updated = ctx.Reservations.Find(reservation.ReservationId);
            Assert.Equal(ReservationStatus.Cancelled, updated!.Status);
        }

        [Fact]
        public async Task Cancel_TooLate_SetsTempDataError_RedirectsDetails()
        {
            var ctx = TestDbContextFactory.Create();
            var (hall, _, cinema) = SeedScreeningData(ctx);

            const string userId = "uid-late";
            var user = new AppUser { Id = userId, UserName = "late", Email = "l@l.com", FirstName = "L", LastName = "T" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var dir = ctx.Directors.First();
            var movie = ctx.Movies.First();
            var nearScreening = new Screening
            {
                MovieId = movie.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Now.AddMinutes(30), TicketPrice = 25m, IsPublished = true
            };
            ctx.Screenings.Add(nearScreening); ctx.SaveChanges();

            var reservation = new Reservation
            {
                ScreeningId = nearScreening.ScreeningId, AppUserId = userId,
                SeatNumber = 6, Status = ReservationStatus.Confirmed,
                ReservationCode = "LATE0001"
            };
            ctx.Reservations.Add(reservation); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), userId);

            var result = await controller.Cancel(reservation.ReservationId);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Cancel_NotOwner_ReturnsForbid()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            var owner = new AppUser { Id = "uid-co1", UserName = "co1", Email = "co1@c.com", FirstName = "C", LastName = "O" };
            var other = new AppUser { Id = "uid-co2", UserName = "co2", Email = "co2@c.com", FirstName = "C", LastName = "T" };
            ctx.Users.AddRange(owner, other); ctx.SaveChanges();

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, AppUserId = owner.Id,
                SeatNumber = 7, ReservationCode = "FORB0001"
            };
            ctx.Reservations.Add(reservation); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(other), other.Id);

            var result = await controller.Cancel(reservation.ReservationId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Cancel_NotFound_ReturnsNotFound()
        {
            var ctx = TestDbContextFactory.Create();
            var user = new AppUser { Id = "uid-cnf", UserName = "cnf", Email = "cnf@c.com", FirstName = "C", LastName = "N" };
            ctx.Users.Add(user); ctx.SaveChanges();

            var controller = BuildController(ctx, UserMockFor(user), user.Id);

            var result = await controller.Cancel(99999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public void Check_Get_ReturnsView()
        {
            var ctx = TestDbContextFactory.Create();
            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            var controller = new ReservationsController(ctx, userMock.Object);
            SetupControllerContext(controller, isAuthenticated: false);

            var result = controller.Check();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Check_Post_ValidCode_ReturnsCheckResultView()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 8,
                Status = ReservationStatus.Confirmed, ReservationCode = "VALIDC01"
            });
            ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            var controller = new ReservationsController(ctx, userMock.Object);
            SetupControllerContext(controller, isAuthenticated: false);

            var result = await controller.Check("VALIDC01");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("CheckResult", view.ViewName);
            Assert.IsType<Reservation>(view.Model);
        }

        [Fact]
        public async Task Check_Post_InvalidCode_SetsViewBagError_ReturnsCheckView()
        {
            var ctx = TestDbContextFactory.Create();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            var controller = new ReservationsController(ctx, userMock.Object);
            SetupControllerContext(controller, isAuthenticated: false);

            var result = await controller.Check("NOTFOUND");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Null(view.ViewName);
            Assert.NotNull(view.ViewData["Error"]);
        }

        [Fact]
        public async Task Check_Post_CaseInsensitive_FindsReservation()
        {
            var ctx = TestDbContextFactory.Create();
            var (_, screening, _) = SeedScreeningData(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 9,
                Status = ReservationStatus.Confirmed, ReservationCode = "ABC12345"
            });
            ctx.SaveChanges();

            var userMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
            var controller = new ReservationsController(ctx, userMock.Object);
            SetupControllerContext(controller, isAuthenticated: false);

            var result = await controller.Check("abc12345");

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("CheckResult", view.ViewName);
        }
    }
}
