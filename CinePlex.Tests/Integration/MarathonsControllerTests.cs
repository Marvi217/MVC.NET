using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using CinePlex.Areas.User.Controllers;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;
using CinePlex.Tests.Helpers;

namespace CinePlex.Tests.Integration
{
    public class MarathonsControllerTests : ControllerTestBase
    {
        private static (Cinema cinema, Hall hall, Marathon marathon) SeedMarathon(
            ApplicationDbContext ctx, bool isActive = true, DateTime? startTime = null,
            DateTime? publishDate = null)
        {
            var cinema = new Cinema { Name = "CinemaM", City = "Krakow", Address = "M St 1" };
            ctx.Cinemas.Add(cinema); ctx.SaveChanges();
            var hall = new Hall { Number = "H1", Capacity = 30, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); ctx.SaveChanges();
            var marathon = new Marathon
            {
                Name = "Film Fest",
                Price = 100m,
                HallId = hall.HallId,
                StartTime = startTime ?? DateTime.Now.AddDays(5),
                IsActive = isActive,
                PublishDate = publishDate
            };
            ctx.Marathons.Add(marathon); ctx.SaveChanges();
            return (cinema, hall, marathon);
        }

        private static MarathonsController BuildController(
            ApplicationDbContext ctx,
            Mock<IEmailService>? emailMock = null,
            string? userId = null,
            bool isAuthenticated = false)
        {
            var userStoreMock = new Mock<IUserStore<AppUser>>();
#pragma warning disable CS8625 // null literals for optional UserManager ctor params
            var userManagerMock = new Mock<UserManager<AppUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625

            if (userId != null && isAuthenticated)
            {
                var fakeUser = new AppUser
                {
                    Id = userId,
                    UserName = "testuser",
                    Email = "test@example.com",
                    FirstName = "Test",
                    LastName = "User"
                };
                userManagerMock
                    .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync(fakeUser);
            }
            else
            {
                userManagerMock
                    .Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                    .ReturnsAsync((AppUser?)null);
            }

            var email = emailMock ?? new Mock<IEmailService>();
            var controller = new MarathonsController(ctx, userManagerMock.Object, email.Object);
            SetupControllerContext(controller, userId, isAuthenticated);
            return controller;
        }

        [Fact]
        public async Task Index_ActiveMarathonInFuture_ReturnsViewWithMarathon()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Marathon>>(view.Model);
            Assert.Contains(model, m => m.MarathonId == marathon.MarathonId);
        }

        [Fact]
        public async Task Index_InactiveMarathonInFuture_NotInList()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMarathon(ctx, isActive: false);
            var controller = BuildController(ctx);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Marathon>>(view.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task Index_ActiveMarathonInPast_NotInList()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMarathon(ctx, isActive: true, startTime: DateTime.Now.AddDays(-1));
            var controller = BuildController(ctx);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Marathon>>(view.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task Index_PublishDatePastAndInactive_IsInList()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: false,
                publishDate: DateTime.Now.AddHours(-1));
            var controller = BuildController(ctx);

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Marathon>>(view.Model);
            Assert.Contains(model, m => m.MarathonId == marathon.MarathonId);
        }

        [Fact]
        public async Task Details_ActiveMarathonExists_ReturnsViewWithViewBag()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx);

            var result = await controller.Details(marathon.MarathonId);

            Assert.IsType<ViewResult>(result);
            Assert.NotNull(controller.ViewBag.TakenSeats);
        }

        [Fact]
        public async Task Details_MarathonNotFoundOrPastOrInactive_ReturnsNotFound()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMarathon(ctx, isActive: false);
            var controller = BuildController(ctx);

            var result = await controller.Details(9999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_TakenSeatsCalculation_CountsOnlyConfirmed()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);

            ctx.Reservations.AddRange(
                new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId = marathon.MarathonId, SeatNumber = 1, SeatRow = "A",
                    Status = ReservationStatus.Confirmed,
                    ReservationCode = "CODE0001"
                },
                new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId = marathon.MarathonId, SeatNumber = 2, SeatRow = "A",
                    Status = ReservationStatus.Confirmed,
                    ReservationCode = "CODE0002"
                },
                new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId = marathon.MarathonId, SeatNumber = 3, SeatRow = "A",
                    Status = ReservationStatus.Cancelled,
                    ReservationCode = "CODE0003"
                });
            await ctx.SaveChangesAsync();
            var controller = BuildController(ctx);

            await controller.Details(marathon.MarathonId);

            var taken = (HashSet<int>)controller.ViewBag.TakenSeats;
            Assert.Equal(2, taken.Count);
            Assert.Contains(1, taken);
            Assert.Contains(2, taken);
            Assert.DoesNotContain(3, taken);
        }

        [Fact]
        public async Task Checkout_NoSeats_RedirectsToDetails()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx);

            var result = await controller.Checkout(marathon.MarathonId, new List<int>());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
        }

        [Fact]
        public async Task Checkout_SeatConflict_SetsTempDataAndRedirects()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            ctx.Reservations.Add(new Reservation
            {
                ReservationType = ReservationType.Marathon,
                MarathonId = marathon.MarathonId, SeatNumber = 5, SeatRow = "A",
                Status = ReservationStatus.Confirmed, ReservationCode = "TAKEN001"
            });
            await ctx.SaveChangesAsync();
            var controller = BuildController(ctx);

            var result = await controller.Checkout(marathon.MarathonId, new List<int> { 5 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task Checkout_ValidSeats_ReturnsViewWithViewBag()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx);

            var result = await controller.Checkout(marathon.MarathonId, new List<int> { 1, 2 });

            var view = Assert.IsType<ViewResult>(result);
            Assert.NotNull(controller.ViewBag.Marathon);
            Assert.NotNull(controller.ViewBag.Seats);
            Assert.NotNull(controller.ViewBag.SeatLabels);
        }

        [Fact]
        public async Task Checkout_MarathonNotFound_ReturnsNotFound()
        {
            using var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx);

            var result = await controller.Checkout(9999, new List<int> { 1 });

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Checkout_EdgeIsolation_SetsTempDataAndRedirectsDetails()
        {
            using var ctx = TestDbContextFactory.Create();
            var cinema = new Cinema { Name = "CI", City = "C", Address = "A" };
            ctx.Cinemas.Add(cinema); ctx.SaveChanges();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); ctx.SaveChanges();
            var marathon = new Marathon
            {
                Name = "IsoTest", Price = 50m, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(3), IsActive = true
            };
            ctx.Marathons.Add(marathon); ctx.SaveChanges();
            for (int s = 2; s <= 9; s++)
                ctx.Reservations.Add(new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId = marathon.MarathonId, SeatNumber = s, SeatRow = "A",
                    Status = ReservationStatus.Confirmed, ReservationCode = $"ISO{s:D4}"
                });
            await ctx.SaveChangesAsync();
            var controller = BuildController(ctx);

            var result = await controller.Checkout(marathon.MarathonId, new List<int> { 10 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task ProcessCheckout_GuestMissingFirstName_SetsTempDataError()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx, isAuthenticated: false);

            var result = await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 1 },
                firstName: null, lastName: "Doe", email: "d@d.com");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Checkout", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task ProcessCheckout_GuestAllFields_CreatesReservationWithGuestData()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx, isAuthenticated: false);

            await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 1 },
                firstName: "John", lastName: "Doe", email: "john@doe.com");

            var res = ctx.Reservations
                .FirstOrDefault(r => r.MarathonId == marathon.MarathonId);
            Assert.NotNull(res);
            Assert.Equal("John Doe", res!.GuestName);
            Assert.Equal("john@doe.com", res.GuestEmail);
            Assert.Null(res.AppUserId);
        }

        [Fact]
        public async Task ProcessCheckout_AuthenticatedUser_CreatesReservationWithUserId()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            const string userId = "user-123";
            var controller = BuildController(ctx, isAuthenticated: true, userId: userId);

            await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 1 },
                firstName: null, lastName: null, email: null);

            var res = ctx.Reservations
                .FirstOrDefault(r => r.MarathonId == marathon.MarathonId);
            Assert.NotNull(res);
            Assert.Equal(userId, res!.AppUserId);
            Assert.Null(res.GuestName);
        }

        [Fact]
        public async Task ProcessCheckout_SeatConflictAtProcessTime_SetsTempDataRedirectsDetails()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            ctx.Reservations.Add(new Reservation
            {
                ReservationType = ReservationType.Marathon,
                MarathonId = marathon.MarathonId, SeatNumber = 7, SeatRow = "A",
                Status = ReservationStatus.Confirmed, ReservationCode = "CONF0007"
            });
            await ctx.SaveChangesAsync();
            var controller = BuildController(ctx, isAuthenticated: false);

            var result = await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 7 },
                firstName: "X", lastName: "Y", email: "x@y.com");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task ProcessCheckout_EdgeIsolation_SetsTempDataError()
        {
            using var ctx = TestDbContextFactory.Create();
            var cinema = new Cinema { Name = "C", City = "City", Address = "Addr" };
            ctx.Cinemas.Add(cinema); ctx.SaveChanges();
            var hall = new Hall { Number = "H", Capacity = 10, CinemaId = cinema.CinemaId };
            ctx.Halls.Add(hall); ctx.SaveChanges();
            var marathon = new Marathon
            {
                Name = "Edge Test", Price = 50m, HallId = hall.HallId,
                StartTime = DateTime.Now.AddDays(3), IsActive = true
            };
            ctx.Marathons.Add(marathon); ctx.SaveChanges();

            for (int s = 2; s <= 9; s++)
            {
                ctx.Reservations.Add(new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId = marathon.MarathonId, SeatNumber = s, SeatRow = "A",
                    Status = ReservationStatus.Confirmed,
                    ReservationCode = $"EDGE{s:D4}"
                });
            }
            await ctx.SaveChangesAsync();

            var controller = BuildController(ctx, isAuthenticated: false);

            var result = await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 10 },
                firstName: "A", lastName: "B", email: "a@b.com");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.NotNull(controller.TempData["Error"]);
        }

        [Fact]
        public async Task ProcessCheckout_OnSuccess_SendsConfirmationEmail()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var emailMock = new Mock<IEmailService>();
            emailMock.Setup(e => e.SendMarathonConfirmedAsync(
                    It.IsAny<IReadOnlyList<Reservation>>(),
                    It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var controller = BuildController(ctx, emailMock, isAuthenticated: false);

            await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 1 },
                firstName: "Jane", lastName: "Smith", email: "jane@smith.com");

            emailMock.Verify(e => e.SendMarathonConfirmedAsync(
                It.IsAny<IReadOnlyList<Reservation>>(),
                "jane@smith.com", "Jane Smith"), Times.Once);
        }

        [Fact]
        public async Task ProcessCheckout_OnSuccess_RedirectsToConfirmation()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var controller = BuildController(ctx, isAuthenticated: false);

            var result = await controller.ProcessCheckout(
                marathon.MarathonId, new List<int> { 5 },
                firstName: "A", lastName: "B", email: "a@b.com");

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Confirmation", redirect.ActionName);
        }

        [Fact]
        public async Task Confirmation_ValidIdViaTempData_ReturnsViewWithReservations()
        {
            using var ctx = TestDbContextFactory.Create();
            var (_, _, marathon) = SeedMarathon(ctx, isActive: true);
            var res = new Reservation
            {
                ReservationType = ReservationType.Marathon,
                MarathonId = marathon.MarathonId, SeatNumber = 4, SeatRow = "A",
                Status = ReservationStatus.Confirmed, ReservationCode = "CONF0004"
            };
            ctx.Reservations.Add(res);
            await ctx.SaveChangesAsync();

            var controller = BuildController(ctx);
            controller.TempData["MarathonBookingIds"] = res.ReservationId.ToString();

            var result = await controller.Confirmation(res.ReservationId);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<Reservation>>(view.Model);
            Assert.Single(model);
        }

        [Fact]
        public async Task Confirmation_NoReservations_ReturnsNotFound()
        {
            using var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx);

            var result = await controller.Confirmation(9999);

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
