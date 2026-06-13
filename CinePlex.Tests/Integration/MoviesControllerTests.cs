using Microsoft.AspNetCore.Mvc;
using Xunit;
using CinePlex.Areas.User.Controllers;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Tests.Helpers;

namespace CinePlex.Tests.Integration
{
    public class MoviesControllerTests : ControllerTestBase
    {
        private static Movie SeedMovie(ApplicationDbContext ctx,
            string title = "Test Movie",
            Genre genre = Genre.Action,
            DateTime? releaseDate = null,
            string directorLastName = "Dir")
        {
            var dir = new Director { FirstName = "A", LastName = directorLastName };
            ctx.Directors.Add(dir); ctx.SaveChanges();
            var movie = new Movie
            {
                Title = title,
                Description = "Desc",
                Duration = 120,
                ReleaseDate = releaseDate ?? DateTime.Today,
                Genre = genre,
                DirectorId = dir.DirectorId
            };
            ctx.Movies.Add(movie); ctx.SaveChanges();
            return movie;
        }

        private static MoviesController BuildController(
            ApplicationDbContext ctx,
            string? userId = null,
            bool isAuthenticated = false)
        {
            var controller = new MoviesController(ctx);
            SetupControllerContext(controller, userId, isAuthenticated);
            return controller;
        }

        [Fact]
        public async Task Index_NoFilters_ReturnsAllMoviesOrderedByTitle()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Charlie");
            SeedMovie(ctx, "Alpha");
            SeedMovie(ctx, "Bravo");
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, null, 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Equal(3, model.Count);
            Assert.Equal("Alpha", model[0].Title);
            Assert.Equal("Bravo", model[1].Title);
            Assert.Equal("Charlie", model[2].Title);
        }

        [Fact]
        public async Task Index_SearchByTitle_ReturnsMatchingMovies()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Action Hero");
            SeedMovie(ctx, "Romance Film");
            var controller = BuildController(ctx);

            var result = await controller.Index("Action", null, null, 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Single(model);
            Assert.Equal("Action Hero", model[0].Title);
        }

        [Fact]
        public async Task Index_SearchByDirectorLastName_ReturnsMatchingMovies()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Raiders", directorLastName: "Spielberg");
            SeedMovie(ctx, "Other", directorLastName: "Nolan");
            var controller = BuildController(ctx);

            var result = await controller.Index("Spielberg", null, null, 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Single(model);
            Assert.Equal("Raiders", model[0].Title);
        }

        [Fact]
        public async Task Index_GenreFilter_ReturnsOnlyMatchingGenre()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Horror One", Genre.Horror);
            SeedMovie(ctx, "Comedy One", Genre.Comedy);
            var controller = BuildController(ctx);

            var result = await controller.Index(null, Genre.Horror, null, 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Single(model);
            Assert.Equal(Genre.Horror, model[0].Genre);
        }

        [Fact]
        public async Task Index_SortByTitleDesc_ReturnsDescendingOrder()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Alpha");
            SeedMovie(ctx, "Charlie");
            SeedMovie(ctx, "Bravo");
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, "title_desc", 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Equal("Charlie", model[0].Title);
            Assert.Equal("Bravo", model[1].Title);
            Assert.Equal("Alpha", model[2].Title);
        }

        [Fact]
        public async Task Index_SortByDate_ReturnsAscendingByReleaseDate()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Newer", releaseDate: new DateTime(2023, 6, 1));
            SeedMovie(ctx, "Older", releaseDate: new DateTime(2020, 1, 1));
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, "date", 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Equal("Older", model[0].Title);
            Assert.Equal("Newer", model[1].Title);
        }

        [Fact]
        public async Task Index_SortByDateDesc_ReturnsDescendingByReleaseDate()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Newer", releaseDate: new DateTime(2023, 6, 1));
            SeedMovie(ctx, "Older", releaseDate: new DateTime(2020, 1, 1));
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, "date_desc", 1);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Equal("Newer", model[0].Title);
            Assert.Equal("Older", model[1].Title);
        }

        [Fact]
        public async Task Index_Pagination_ReturnsSecondPage()
        {
            using var ctx = TestDbContextFactory.Create();
            for (int i = 1; i <= 7; i++)
                SeedMovie(ctx, $"Movie {i:D2}");
            var controller = BuildController(ctx);

            var result = await controller.Index(null, null, null, 2);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IList<Movie>>(view.Model);
            Assert.Single(model);
            Assert.Equal(2, (int)controller.ViewBag.TotalPages);
        }

        [Fact]
        public async Task Index_ViewBag_AllExpectedKeysSet()
        {
            using var ctx = TestDbContextFactory.Create();
            SeedMovie(ctx, "Alpha");
            var controller = BuildController(ctx);

            await controller.Index("test", Genre.Action, "date", 1);

            Assert.Equal("test", (string?)controller.ViewBag.Search);
            Assert.Equal(Genre.Action, (Genre?)controller.ViewBag.Genre);
            Assert.Equal("date", (string?)controller.ViewBag.Sort);
            Assert.Equal(1, (int)controller.ViewBag.Page);
            Assert.NotNull(controller.ViewBag.TotalPages);
            Assert.NotNull(controller.ViewBag.Genres);
        }

        [Fact]
        public async Task Details_MovieExists_ReturnsView()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "Existing");
            var controller = BuildController(ctx);

            var result = await controller.Details(movie.MovieId, null, null);

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Details_MovieNotFound_ReturnsNotFound()
        {
            using var ctx = TestDbContextFactory.Create();
            var controller = BuildController(ctx);

            var result = await controller.Details(9999, null, null);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Details_TrailerUrlWithVParam_SetsEmbedUrl()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); ctx.SaveChanges();
            var movie = new Movie
            {
                Title = "YouTube Movie", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action,
                DirectorId = dir.DirectorId,
                TrailerUrl = "https://youtube.com/watch?v=abc123"
            };
            ctx.Movies.Add(movie); ctx.SaveChanges();
            var controller = BuildController(ctx);

            await controller.Details(movie.MovieId, null, null);

            Assert.Equal("https://www.youtube.com/embed/abc123",
                (string?)controller.ViewBag.EmbedUrl);
        }

        [Fact]
        public async Task Details_TrailerUrlWithoutVParam_EmbedUrlNotSet()
        {
            using var ctx = TestDbContextFactory.Create();
            var dir = new Director { FirstName = "A", LastName = "B" };
            ctx.Directors.Add(dir); ctx.SaveChanges();
            var movie = new Movie
            {
                Title = "No Embed", Description = "D", Duration = 90,
                ReleaseDate = DateTime.Today, Genre = Genre.Action,
                DirectorId = dir.DirectorId,
                TrailerUrl = "https://vimeo.com/12345"
            };
            ctx.Movies.Add(movie); ctx.SaveChanges();
            var controller = BuildController(ctx);

            await controller.Details(movie.MovieId, null, null);

            Assert.Null(controller.ViewBag.EmbedUrl);
        }

        [Fact]
        public async Task Details_Reviews_OnlyApprovedVisible()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "Reviewed");
            var user1 = new AppUser
            {
                Id = "u1", UserName = "u1", Email = "u1@t.com",
                FirstName = "U", LastName = "One", NormalizedUserName = "U1",
                NormalizedEmail = "U1@T.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            var user2 = new AppUser
            {
                Id = "u2", UserName = "u2", Email = "u2@t.com",
                FirstName = "U", LastName = "Two", NormalizedUserName = "U2",
                NormalizedEmail = "U2@T.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            var user3 = new AppUser
            {
                Id = "u3", UserName = "u3", Email = "u3@t.com",
                FirstName = "U", LastName = "Three", NormalizedUserName = "U3",
                NormalizedEmail = "U3@T.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            ctx.Users.AddRange(user1, user2, user3);
            await ctx.SaveChangesAsync();

            ctx.MovieReviews.AddRange(
                new MovieReview
                {
                    MovieId = movie.MovieId, AppUserId = "u1", Rating = 4,
                    ModerationStatus = ReviewModerationStatus.Approved
                },
                new MovieReview
                {
                    MovieId = movie.MovieId, AppUserId = "u2", Rating = 3,
                    ModerationStatus = ReviewModerationStatus.Approved
                },
                new MovieReview
                {
                    MovieId = movie.MovieId, AppUserId = "u3", Rating = 1,
                    ModerationStatus = ReviewModerationStatus.Hidden
                });
            await ctx.SaveChangesAsync();

            var controller = BuildController(ctx);
            await controller.Details(movie.MovieId, null, null);

            var reviews = (IList<MovieReview>)controller.ViewBag.Reviews;
            Assert.Equal(2, reviews.Count);
            Assert.All(reviews, r => Assert.NotEqual(ReviewModerationStatus.Hidden, r.ModerationStatus));
        }

        [Fact]
        public async Task Details_AverageRating_CalculatedCorrectly()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "Rated");
            var user1 = new AppUser
            {
                Id = "u1r", UserName = "u1r", Email = "u1r@t.com",
                FirstName = "U", LastName = "One", NormalizedUserName = "U1R",
                NormalizedEmail = "U1R@T.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            var user2 = new AppUser
            {
                Id = "u2r", UserName = "u2r", Email = "u2r@t.com",
                FirstName = "U", LastName = "Two", NormalizedUserName = "U2R",
                NormalizedEmail = "U2R@T.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            ctx.Users.AddRange(user1, user2);
            await ctx.SaveChangesAsync();

            ctx.MovieReviews.AddRange(
                new MovieReview
                {
                    MovieId = movie.MovieId, AppUserId = "u1r", Rating = 3,
                    ModerationStatus = ReviewModerationStatus.Approved
                },
                new MovieReview
                {
                    MovieId = movie.MovieId, AppUserId = "u2r", Rating = 5,
                    ModerationStatus = ReviewModerationStatus.Approved
                });
            await ctx.SaveChangesAsync();

            var controller = BuildController(ctx);
            await controller.Details(movie.MovieId, null, null);

            Assert.Equal(4.0, (double?)controller.ViewBag.AverageRating);
        }

        [Fact]
        public async Task Details_NoReviews_AverageRatingIsNull()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "Unreviewed");
            var controller = BuildController(ctx);

            await controller.Details(movie.MovieId, null, null);

            Assert.Null((double?)controller.ViewBag.AverageRating);
        }

        [Fact]
        public async Task Details_AuthenticatedUserWithReview_UserReviewSet()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "User Reviewed");
            const string userId = "reviewer-user";
            var appUser = new AppUser
            {
                Id = userId, UserName = "reviewer", Email = "r@r.com",
                FirstName = "R", LastName = "User", NormalizedUserName = "REVIEWER",
                NormalizedEmail = "R@R.COM", SecurityStamp = Guid.NewGuid().ToString()
            };
            ctx.Users.Add(appUser);
            await ctx.SaveChangesAsync();

            ctx.MovieReviews.Add(new MovieReview
            {
                MovieId = movie.MovieId, AppUserId = userId, Rating = 4,
                ModerationStatus = ReviewModerationStatus.Approved
            });
            await ctx.SaveChangesAsync();

            var controller = BuildController(ctx, userId: userId, isAuthenticated: true);
            await controller.Details(movie.MovieId, null, null);

            Assert.NotNull(controller.ViewBag.UserReview);
        }

        [Fact]
        public async Task ScreeningsPartial_ReturnsCorrectPartialView()
        {
            using var ctx = TestDbContextFactory.Create();
            var movie = SeedMovie(ctx, "Partial Movie");
            var controller = BuildController(ctx);

            var result = await controller.ScreeningsPartial(movie.MovieId, null, null);

            var partial = Assert.IsType<PartialViewResult>(result);
            Assert.Equal("_ScreeningsPartial", partial.ViewName);
        }
    }
}
