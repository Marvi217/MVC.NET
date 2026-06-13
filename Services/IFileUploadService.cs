namespace CinePlex.Services
{
    public interface IFileUploadService
    {
        Task<string?> SavePosterAsync(IFormFile? file);
        Task<string> SaveBarImageAsync(IFormFile file);
        void DeleteFile(string? url);
    }
}
