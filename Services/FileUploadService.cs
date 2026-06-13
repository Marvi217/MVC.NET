namespace CinePlex.Services
{
    public class FileUploadService : IFileUploadService
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private readonly IWebHostEnvironment _env;

        public FileUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string?> SavePosterAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return null;
            var dir = Path.Combine(_env.WebRootPath, "uploads", "posters");
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid()}{ext}";
            await using var fs = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await file.CopyToAsync(fs);
            return $"/uploads/posters/{name}";
        }

        public async Task<string> SaveBarImageAsync(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "images", "bar");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var name = $"{Guid.NewGuid()}{ext}";
            await using var stream = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await file.CopyToAsync(stream);
            return $"/images/bar/{name}";
        }

        public void DeleteFile(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            var path = Path.Combine(_env.WebRootPath, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
