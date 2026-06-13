using Microsoft.AspNetCore.Identity;
using CinePlex.Models;

namespace CinePlex.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<AppUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            await SeedRolesAsync(roleManager);
            await SeedAdminAsync(userManager);
            await SeedDataAsync(context);
        }

        private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in new[] { "Admin", "User" })
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
        }

        private static async Task SeedAdminAsync(UserManager<AppUser> userManager)
        {
            if (await userManager.FindByEmailAsync("admin@cineplex.com") == null)
            {
                var admin = new AppUser
                {
                    UserName = "admin@cineplex.com",
                    Email = "admin@cineplex.com",
                    FirstName = "System",
                    LastName = "Admin",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        private static async Task SeedDataAsync(ApplicationDbContext context)
        {
            if (context.Directors.Any()) return;

            var directors = new List<Director>
            {
                new Director { FirstName = "Christopher", LastName = "Nolan", Nationality = "British", Biography = "One of the greatest contemporary filmmakers." },
                new Director { FirstName = "Ridley", LastName = "Scott", Nationality = "British", Biography = "Creator of iconic sci-fi and historical films." },
                new Director { FirstName = "Steven", LastName = "Spielberg", Nationality = "American", Biography = "Hollywood legend, creator of Jurassic Park and Schindler's List." },
                new Director { FirstName = "Denis", LastName = "Villeneuve", Nationality = "Canadian", Biography = "Director of Dune and Blade Runner 2049." }
            };
            context.Directors.AddRange(directors);
            await context.SaveChangesAsync();

            var movies = new List<Movie>
            {
                new Movie { Title = "Inception", Description = "A thief who steals secrets through dreams.", Duration = 148, ReleaseDate = new DateTime(2010, 7, 16), Genre = Genre.SciFi, AgeRating = AgeRating.Age13, DirectorId = directors[0].DirectorId },
                new Movie { Title = "Interstellar", Description = "A journey through a wormhole in search of a new home.", Duration = 169, ReleaseDate = new DateTime(2014, 11, 7), Genre = Genre.SciFi, AgeRating = AgeRating.Age7, DirectorId = directors[0].DirectorId },
                new Movie { Title = "Gladiator", Description = "A Roman general fighting in the arena.", Duration = 155, ReleaseDate = new DateTime(2000, 5, 5), Genre = Genre.Action, AgeRating = AgeRating.Age16, DirectorId = directors[1].DirectorId },
                new Movie { Title = "Indiana Jones", Description = "Adventures of a famous archaeologist.", Duration = 115, ReleaseDate = new DateTime(1981, 6, 12), Genre = Genre.Adventure, AgeRating = AgeRating.Age7, DirectorId = directors[2].DirectorId },
                new Movie { Title = "Dune", Description = "A sci-fi epic on the desert planet Arrakis.", Duration = 155, ReleaseDate = new DateTime(2021, 10, 22), Genre = Genre.SciFi, AgeRating = AgeRating.Age13, DirectorId = directors[3].DirectorId }
            };
            context.Movies.AddRange(movies);
            await context.SaveChangesAsync();

            var cinemas = new List<Cinema>
            {
                new Cinema { Name = "CinePlex Warsaw", City = "Warsaw", Address = "Złota 59", Phone = "+48 22 100 200 300", Email = "warsaw@cineplex.com" },
                new Cinema { Name = "CinePlex Krakow", City = "Krakow", Address = "al. Pokoju 44", Phone = "+48 12 100 200 300", Email = "krakow@cineplex.com" },
                new Cinema { Name = "CinePlex Wroclaw", City = "Wroclaw", Address = "Świdnicka 2", Phone = "+48 71 100 200 300", Email = "wroclaw@cineplex.com" }
            };
            context.Cinemas.AddRange(cinemas);
            await context.SaveChangesAsync();

            var halls = new List<Hall>
            {
                new Hall { Number = "1", Capacity = 200, Type = HallType.IMAX, CinemaId = cinemas[0].CinemaId },
                new Hall { Number = "2", Capacity = 120, Type = HallType.ThreeD, CinemaId = cinemas[0].CinemaId },
                new Hall { Number = "3", Capacity = 80, Type = HallType.Standard, CinemaId = cinemas[0].CinemaId },
                new Hall { Number = "1", Capacity = 150, Type = HallType.ThreeD, CinemaId = cinemas[1].CinemaId },
                new Hall { Number = "2", Capacity = 100, Type = HallType.Standard, CinemaId = cinemas[1].CinemaId },
                new Hall { Number = "1", Capacity = 180, Type = HallType.IMAX, CinemaId = cinemas[2].CinemaId }
            };
            context.Halls.AddRange(halls);
            await context.SaveChangesAsync();

            var screenings = new List<Screening>
            {
                new Screening { StartTime = DateTime.Now.AddDays(1).Date.AddHours(10), TicketPrice = 12.99m, AudioVersion = AudioVersion.Dubbed, IsPublished = false, MovieId = movies[0].MovieId, HallId = halls[0].HallId },
                new Screening { StartTime = DateTime.Now.AddDays(1).Date.AddHours(14), TicketPrice = 14.99m, AudioVersion = AudioVersion.Subtitled, IsPublished = false, MovieId = movies[0].MovieId, HallId = halls[1].HallId },
                new Screening { StartTime = DateTime.Now.AddDays(1).Date.AddHours(18), TicketPrice = 16.99m, AudioVersion = AudioVersion.Original, IsPublished = false, MovieId = movies[1].MovieId, HallId = halls[0].HallId },
                new Screening { StartTime = DateTime.Now.AddDays(2).Date.AddHours(12), TicketPrice = 12.99m, AudioVersion = AudioVersion.Dubbed, IsPublished = false, MovieId = movies[2].MovieId, HallId = halls[3].HallId },
                new Screening { StartTime = DateTime.Now.AddDays(2).Date.AddHours(16), TicketPrice = 14.99m, AudioVersion = AudioVersion.Subtitled, IsPublished = false, MovieId = movies[3].MovieId, HallId = halls[4].HallId },
                new Screening { StartTime = DateTime.Now.AddDays(3).Date.AddHours(20), TicketPrice = 18.99m, AudioVersion = AudioVersion.Subtitled, IsPublished = false, MovieId = movies[4].MovieId, HallId = halls[5].HallId }
            };
            context.Screenings.AddRange(screenings);
            await context.SaveChangesAsync();
        }
    }
}
