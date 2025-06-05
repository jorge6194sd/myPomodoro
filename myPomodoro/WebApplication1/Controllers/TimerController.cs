using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using System.Text;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
    public class TimerController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public TimerController(IWebHostEnvironment env, IConfiguration config)
        {
            _env = env;
            _config = config;
        }

        // 1. Serve the main Timer page
        public IActionResult Index()
        {
            return View();
        }

        // 2. Record completed session to a CSV file and optionally send email
        [HttpPost]
        public IActionResult RecordSession([FromBody] List<TimerSession> sessions)
        {
            if (sessions == null || !sessions.Any())
                return BadRequest("No sessions provided.");

            // 2a. Save to CSV file
            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");

            // Ensure the directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

            var csvBuilder = new StringBuilder();

            // If file doesn't exist, add header row
            if (!System.IO.File.Exists(csvPath))
            {
                csvBuilder.AppendLine("StartTime,EndTime,DurationMinutes,SessionType");
            }

            // Append each session
            foreach (var session in sessions)
            {
                csvBuilder.AppendLine(
                    $"{session.StartTime},{session.EndTime},{session.DurationMinutes},{session.SessionType}"
                );
            }

            // Write to CSV
            System.IO.File.AppendAllText(csvPath, csvBuilder.ToString());

            // 2b. Send email with CSV data
            try
            {
                var shouldSendEmail = _config.GetValue<bool>("EmailSettings:SendEmail");
                if (shouldSendEmail)
                {
                    SendEmail(csvBuilder.ToString());
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception
                return StatusCode(500, $"Failed to send email. Error: {ex.Message}");
            }

            return Ok("Session recorded successfully.");
        }

        private void SendEmail(string csvContent)
        {
            var smtpHost = _config.GetValue<string>("EmailSettings:SmtpHost");
            var smtpPort = _config.GetValue<int>("EmailSettings:SmtpPort");
            var smtpUser = _config.GetValue<string>("EmailSettings:SmtpUser");
            var smtpPass = _config.GetValue<string>("EmailSettings:SmtpPass");
            var fromEmail = _config.GetValue<string>("EmailSettings:FromEmail");
            var toEmail = _config.GetValue<string>("EmailSettings:ToEmail");

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = "Completed Timer Sessions",
                    Body = "Here are the completed timer sessions:\n\n" + csvContent
                };
                mailMessage.To.Add(new MailAddress(toEmail));

                client.Send(mailMessage);
            }
        }
    }
}
