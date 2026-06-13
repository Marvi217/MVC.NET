using CinePlex.Areas.User.Controllers;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;
using CinePlex.Tests.Helpers;
using CinePlex.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Text;
using Xunit;

namespace CinePlex.Tests.Integration
{
    public class CheckoutControllerTests : ControllerTestBase
    {

        private static (Hall hall, Movie movie, Screening screening) SeedScreening(ApplicationDbContext ctx)
        {
            var cinema = new Cinema { Name = "Test Cinema", City = "Warsaw", Address = "Test St 1" };
            ctx.Cinemas.Add(cinema);
            ctx.SaveChanges();

            var hall = new Hall { Number = "1", Capacity = 20, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall);

            var director = new Director { FirstName = "Test", LastName = "Director" };
            ctx.Directors.Add(director);
            ctx.SaveChanges();

            var movie = new Movie
            {
                Title = "Test Movie", Description = "Desc", Duration = 120,
                ReleaseDate = DateTime.Today, DirectorId = director.DirectorId
            };
            ctx.Movies.Add(movie);
            ctx.SaveChanges();

            var screening = new Screening
            {
                MovieId = movie.MovieId, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(1), TicketPrice = 25m, IsPublished = true
            };
            ctx.Screenings.Add(screening);
            ctx.SaveChanges();
            return (hall, movie, screening);
        }

        private static (CheckoutController controller, ApplicationDbContext ctx,
            Mock<UserManager<AppUser>> userManagerMock, Mock<IEmailService> emailMock)
            BuildGuest()
        {
            var ctx = TestDbContextFactory.Create();
#pragma warning disable CS8625 // null args are the standard pattern for mocking UserManager
            var userManagerMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
            var emailMock = new Mock<IEmailService>();
            var controller = new CheckoutController(ctx, userManagerMock.Object, emailMock.Object);
            SetupControllerContext(controller, userId: null, isAuthenticated: false);
            return (controller, ctx, userManagerMock, emailMock);
        }

        private static (CheckoutController controller, ApplicationDbContext ctx,
            Mock<UserManager<AppUser>> userManagerMock, Mock<IEmailService> emailMock, AppUser user)
            BuildAuthenticated()
        {
            var ctx = TestDbContextFactory.Create();
#pragma warning disable CS8625
            var userManagerMock = new Mock<UserManager<AppUser>>(
                Mock.Of<IUserStore<AppUser>>(), null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
            var emailMock = new Mock<IEmailService>();

            var user = new AppUser
            {
                Id = "user-1", UserName = "testuser@example.com",
                Email = "testuser@example.com", FirstName = "John", LastName = "Doe"
            };

            userManagerMock
                .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(user);
            userManagerMock
                .Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .Returns(user.Id);

            var controller = new CheckoutController(ctx, userManagerMock.Object, emailMock.Object);
            SetupControllerContext(controller, userId: user.Id, isAuthenticated: true);
            return (controller, ctx, userManagerMock, emailMock, user);
        }

        private static void SetSession(Controller controller, string key, string value) =>
            ((FakeSession)controller.HttpContext.Session)
                .Set(key, Encoding.UTF8.GetBytes(value));

        private const string SessionKey = "PendingCheckoutIds";

        [Fact]
        public async Task Index_ValidSeatsNoConflicts_ReturnsViewWithViewModel()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var result = await controller.Index(screening.ScreeningId, "1,2");

            var view = Assert.IsType<ViewResult>(result);
            var vm = Assert.IsType<CheckoutViewModel>(view.Model);
            Assert.Equal(screening.ScreeningId, vm.ScreeningId);
            Assert.Equal(new List<int> { 1, 2 }, vm.SeatNumbers);
        }

        [Fact]
        public async Task Index_EmptySeats_RedirectsToScreeningsIndex()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var result = await controller.Index(screening.ScreeningId, "");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Screenings", redirect.ControllerName);
        }

        [Fact]
        public async Task Index_ScreeningNotFound_ReturnsNotFound()
        {
            var (controller, _, _, _) = BuildGuest();

            var result = await controller.Index(9999, "1,2");

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Index_SeatAlreadyTaken_RedirectsWithError()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId,
                SeatNumber = 1,
                Status = ReservationStatus.Confirmed,
                ReservationCode = "TAKEN001",
                PricePaid = 25m
            });
            ctx.SaveChanges();

            var result = await controller.Index(screening.ScreeningId, "1,2");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("Screenings", redirect.ControllerName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Index_CancelPendingIds_OwnedBySession_CancelsThem()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var pending = new Reservation
            {
                ScreeningId = screening.ScreeningId,
                SeatNumber = 5,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.Now.AddMinutes(10),
                ReservationCode = "PEND0001",
                PricePaid = 25m
            };
            ctx.Reservations.Add(pending);
            ctx.SaveChanges();

            SetSession(controller, SessionKey, pending.ReservationId.ToString());

            var result = await controller.Index(
                screening.ScreeningId, "1,2",
                cancelPendingIds: pending.ReservationId.ToString());

            var refreshed = ctx.Reservations.Find(pending.ReservationId)!;
            Assert.Equal(ReservationStatus.Cancelled, refreshed.Status);

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Index_LayoutWithPriceMultiplier_SeatDetailPriceIsMultiplied()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (hall, _, screening) = SeedScreening(ctx);

            var layout = new HallLayoutConfig
            {
                Rows = new List<HallRowConfig>
                {
                    new() { Label = "A", SeatCount = 10, PriceMultiplier = 2.0m },
                    new() { Label = "B", SeatCount = 10, PriceMultiplier = 1.0m }
                }
            };
            hall.LayoutJson = layout.ToJson();
            ctx.SaveChanges();

            var result = await controller.Index(screening.ScreeningId, "1");

            var view = Assert.IsType<ViewResult>(result);
            var seatDetails = controller.ViewBag.SeatDetails as List<SeatDetail>;
            Assert.NotNull(seatDetails);
            Assert.Single(seatDetails!);
            Assert.Equal(50m, seatDetails![0].Price); // 25 * 2.0
        }

        [Fact]
        public async Task Process_GuestWithoutEmail_SetsTempDataErrorAndRedirects()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var vm = new CheckoutViewModel
            {
                ScreeningId = screening.ScreeningId,
                SeatNumbers = new List<int> { 1, 2 },
                FirstName = "Anna",
                LastName = "Kowalska",
                Email = ""        // missing
            };

            var result = await controller.Process(vm);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Process_GuestWithAllFields_CreatesPendingReservationsAndRedirectsToPay()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var vm = new CheckoutViewModel
            {
                ScreeningId = screening.ScreeningId,
                SeatNumbers = new List<int> { 3, 4 },
                FirstName = "Jan",
                LastName = "Nowak",
                Email = "jan@example.com"
            };

            var result = await controller.Process(vm);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Pay", redirect.ActionName);

            var reservations = ctx.Reservations
                .Where(r => r.ScreeningId == screening.ScreeningId)
                .ToList();
            Assert.Equal(2, reservations.Count);
            Assert.All(reservations, r => Assert.Equal(ReservationStatus.Pending, r.Status));
            Assert.All(reservations, r => Assert.Equal("jan@example.com", r.GuestEmail));
        }

        [Fact]
        public async Task Process_AuthenticatedUser_SetsAppUserIdOnReservations()
        {
            var (controller, ctx, _, _, user) = BuildAuthenticated();
            var (_, _, screening) = SeedScreening(ctx);

            var vm = new CheckoutViewModel
            {
                ScreeningId = screening.ScreeningId,
                SeatNumbers = new List<int> { 5, 6 }
            };

            var result = await controller.Process(vm);

            Assert.IsType<RedirectToActionResult>(result);
            var reservations = ctx.Reservations
                .Where(r => r.ScreeningId == screening.ScreeningId)
                .ToList();
            Assert.Equal(2, reservations.Count);
            Assert.All(reservations, r => Assert.Equal(user.Id, r.AppUserId));
            Assert.All(reservations, r => Assert.Null(r.GuestEmail));
        }

        [Fact]
        public async Task Process_SeatTakenBetweenIndexAndProcess_SetsTempDataErrorAndRedirects()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            ctx.Reservations.Add(new Reservation
            {
                ScreeningId = screening.ScreeningId,
                SeatNumber = 7,
                Status = ReservationStatus.Confirmed,
                ReservationCode = "RACECON1",
                PricePaid = 25m
            });
            ctx.SaveChanges();

            var vm = new CheckoutViewModel
            {
                ScreeningId = screening.ScreeningId,
                SeatNumbers = new List<int> { 7 },
                FirstName = "Jan", LastName = "Nowak", Email = "jan@example.com"
            };

            var result = await controller.Process(vm);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal("Screenings", redirect.ControllerName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Process_ScreeningNotFound_ReturnsNotFound()
        {
            var (controller, _, _, _) = BuildGuest();

            var vm = new CheckoutViewModel
            {
                ScreeningId = 9999,
                SeatNumbers = new List<int> { 1 },
                FirstName = "Jan", LastName = "Nowak", Email = "jan@example.com"
            };

            var result = await controller.Process(vm);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Process_CreatesReservationsWithPendingStatusAndCorrectExpiry()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var before = DateTime.Now;
            var vm = new CheckoutViewModel
            {
                ScreeningId = screening.ScreeningId,
                SeatNumbers = new List<int> { 8 },
                FirstName = "Anna", LastName = "Kowalska", Email = "anna@example.com"
            };

            await controller.Process(vm);
            var after = DateTime.Now;

            var reservation = ctx.Reservations
                .Single(r => r.ScreeningId == screening.ScreeningId && r.SeatNumber == 8);

            Assert.Equal(ReservationStatus.Pending, reservation.Status);
            Assert.NotNull(reservation.ExpiresAt);
            Assert.InRange(reservation.ExpiresAt!.Value,
                before.AddMinutes(14).AddSeconds(59),
                after.AddMinutes(15).AddSeconds(1));
            Assert.Equal(screening.TicketPrice, reservation.PricePaid);
        }

        [Fact]
        public async Task Pay_EmptyPendingIds_RedirectsToScreeningsIndex()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var result = await controller.Pay(screening.ScreeningId, "1,2", "");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Screenings", redirect.ControllerName);
        }

        [Fact]
        public async Task Pay_ValidGuestSessionOwnership_ReturnsView()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId,
                SeatNumber = 1,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.Now.AddMinutes(14),
                ReservationCode = "PAY00001",
                PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            var ids = reservation.ReservationId.ToString();
            SetSession(controller, SessionKey, ids);

            var result = await controller.Pay(screening.ScreeningId, "1", ids);

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Pay_NoOwnership_ReturnsForbid()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId,
                SeatNumber = 2,
                Status = ReservationStatus.Pending,
                ExpiresAt = DateTime.Now.AddMinutes(14),
                ReservationCode = "FORBID01",
                PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            SetSession(controller, SessionKey, "99999");

            var result = await controller.Pay(
                screening.ScreeningId, "2", reservation.ReservationId.ToString());

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task SimulateSuccess_PendingReservations_BecomeConfirmedWithNoExpiry()
        {
            var (controller, ctx, _, emailMock) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var r1 = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 1,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                GuestName = "Jan Nowak", GuestEmail = "jan@example.com",
                ReservationCode = "SIM00001", PricePaid = 25m
            };
            var r2 = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 2,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                GuestName = "Jan Nowak", GuestEmail = "jan@example.com",
                ReservationCode = "SIM00002", PricePaid = 25m
            };
            ctx.Reservations.AddRange(r1, r2);
            ctx.SaveChanges();

            var ids = $"{r1.ReservationId},{r2.ReservationId}";
            SetSession(controller, SessionKey, ids);

            await controller.SimulateSuccess(ids, screening.ScreeningId, "1,2");

            var updated = ctx.Reservations
                .Where(r => r.ScreeningId == screening.ScreeningId)
                .ToList();
            Assert.All(updated, r => Assert.Equal(ReservationStatus.Confirmed, r.Status));
            Assert.All(updated, r => Assert.Null(r.ExpiresAt));
        }

        [Fact]
        public async Task SimulateSuccess_SendsConfirmationEmail()
        {
            var (controller, ctx, _, emailMock) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 3,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                GuestName = "Anna Kowalska", GuestEmail = "anna@example.com",
                ReservationCode = "MAIL0001", PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            var ids = reservation.ReservationId.ToString();
            SetSession(controller, SessionKey, ids);

            await controller.SimulateSuccess(ids, screening.ScreeningId, "3");

            emailMock.Verify(
                e => e.SendReservationConfirmedAsync(
                    It.IsAny<IReadOnlyList<Reservation>>(),
                    "anna@example.com",
                    "Anna Kowalska"),
                Times.Once);
        }

        [Fact]
        public async Task SimulateSuccess_RedirectsToScreeningsConfirmation()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 4,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                GuestName = "Test User", GuestEmail = "test@example.com",
                ReservationCode = "REDIR001", PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            var ids = reservation.ReservationId.ToString();
            SetSession(controller, SessionKey, ids);

            var result = await controller.SimulateSuccess(ids, screening.ScreeningId, "4");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
            Assert.Equal("Screenings", redirect.ControllerName);
        }

        [Fact]
        public async Task SimulateSuccess_OwnershipCheckFails_ReturnsForbid()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 9,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                ReservationCode = "FORBSUC1", PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            SetSession(controller, SessionKey, "99999");

            var result = await controller.SimulateSuccess(
                reservation.ReservationId.ToString(), screening.ScreeningId, "9");

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task SimulateFail_SetsPaymentErrorTempDataAndRedirectsToIndex()
        {
            var (controller, ctx, _, _) = BuildGuest();
            var (_, _, screening) = SeedScreening(ctx);

            var reservation = new Reservation
            {
                ScreeningId = screening.ScreeningId, SeatNumber = 10,
                Status = ReservationStatus.Pending, ExpiresAt = DateTime.Now.AddMinutes(10),
                ReservationCode = "FAIL0001", PricePaid = 25m
            };
            ctx.Reservations.Add(reservation);
            ctx.SaveChanges();

            var ids = reservation.ReservationId.ToString();
            SetSession(controller, SessionKey, ids);

            var result = await controller.SimulateFail(ids, screening.ScreeningId, "10");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.NotNull(controller.TempData["PaymentError"]);

            Assert.NotNull(redirect.RouteValues);
            Assert.True(redirect.RouteValues!.ContainsKey("cancelPendingIds"));
            Assert.Equal(ids, redirect.RouteValues["cancelPendingIds"]?.ToString());
        }
    }
}
