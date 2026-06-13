using CinePlex.Models;

namespace CinePlex.Services
{
    public interface IEmailService
    {
        Task SendReservationConfirmedAsync(IReadOnlyList<Reservation> reservations, string recipientEmail, string recipientName);
        Task SendMarathonConfirmedAsync(IReadOnlyList<Reservation> reservations, string recipientEmail, string recipientName);
    }
}
