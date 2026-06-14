using System.Net;
using System.Net.Mail;
using CinePlex.Models;

namespace CinePlex.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendReservationConfirmedAsync(IReadOnlyList<Reservation> reservations, string recipientEmail, string recipientName)
        {
            if (reservations.Count == 0) return;
            var first = reservations[0];
            var movie  = first.Screening?.Movie;
            var hall   = first.Screening?.Hall;
            var cinema = hall?.Cinema;
            var codes  = string.Join(", ", reservations.Select(r => r.ReservationCode));

            var subject = $"Potwierdzenie rezerwacji - {movie?.Title ?? "CinePlex"}";
            var body = BuildBody(recipientName, new[]
            {
                ("Film",  movie?.Title  ?? "–"),
                ("Kino",  cinema?.Name  ?? "–"),
                ("Sala",  hall != null ? $"#{hall.Number}" : "–"),
                ("Data",  first.Screening?.StartTime.ToString("dd.MM.yyyy HH:mm") ?? "–"),
                ("Kody",  codes),
                ("Kwota", $"{reservations.Sum(r => r.PricePaid):0.00} zł"),
            });

            await SendAsync(recipientEmail, subject, body);
        }

        public async Task SendMarathonConfirmedAsync(IReadOnlyList<Reservation> reservations, string recipientEmail, string recipientName)
        {
            if (reservations.Count == 0) return;
            var first   = reservations[0];
            var marathon = first.Marathon;
            var cinema   = marathon?.Hall?.Cinema;
            var codes    = string.Join(", ", reservations.Select(r => r.ReservationCode));

            var subject = $"Potwierdzenie rezerwacji — {marathon?.Name ?? "Maraton CinePlex"}";
            var body = BuildBody(recipientName, new[]
            {
                ("Maraton", marathon?.Name ?? "–"),
                ("Kino",    cinema?.Name   ?? "–"),
                ("Data",    marathon?.StartTime.ToString("dd.MM.yyyy HH:mm") ?? "–"),
                ("Kody",    codes),
                ("Kwota",   $"{reservations.Sum(r => r.PricePaid):0.00} zł"),
            });

            await SendAsync(recipientEmail, subject, body);
        }

        private static string BuildBody(string recipientName, (string Label, string Value)[] rows)
        {
            var lines = string.Join("\n", rows.Select(r => $"{r.Label,8}: {r.Value}"));
            return $"Witaj {recipientName},\n\nTwoja rezerwacja została potwierdzona!\n\n{lines}\n\nPokaż kod przy wejściu.\n\nCinePlex";
        }

        public async Task SendEmailConfirmationAsync(string recipientEmail, string recipientName, string confirmationLink)
        {
            var subject = "Potwierdź swój adres e-mail - CinePlex";
            var body = $@"
                        <p>Witaj {recipientName},</p>

                        <p>Dziękujemy za rejestrację w CinePlex!</p>

                        <p>
                            Kliknij poniższy link, aby aktywować konto:
                        </p>

                        <p>
                            <a href='{confirmationLink}'>Potwierdź adres e-mail</a>
                        </p>

                        <p>
                            Link jest ważny przez 24 godziny.<br/>
                            Jeśli to nie Ty — zignoruj tę wiadomość.
                        </p>

                        <p>CinePlex</p>";

            await SendAsync(recipientEmail, subject, body);
        }

        private async Task SendAsync(string to, string subject, string body)
        {
            var host = _config["Email:SmtpHost"];
            if (string.IsNullOrEmpty(host))
            {
                _logger.LogInformation("Email (no SMTP configured) → {To} | {Subject}", to, subject);
                return;
            }

            var port = _config.GetValue<int>("Email:SmtpPort", 587);
            var user = _config["Email:Username"] ?? "";
            var pass = _config["Email:Password"] ?? "";
            var from = _config["Email:From"] ?? user;

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass)
            };

            using var message = new MailMessage(from, to)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            await client.SendMailAsync(message);
        }
    }
}
