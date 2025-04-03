using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using System.Text;
using YourProject.Models;
using System.Globalization;

namespace YourProject.Controllers
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

        // Serve the main Timer page
        public IActionResult Index()
        {
            return View();
        }

        // POST: Record completed sessions to CSV, store FocusRating, etc.
        [HttpPost]
        public IActionResult RecordSession([FromBody] List<TimerSession> sessions)
        {
            if (sessions == null || !sessions.Any())
                return BadRequest("No sessions provided.");

            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

            var csvBuilder = new StringBuilder();

            // If file doesn't exist, add header row
            bool fileExists = System.IO.File.Exists(csvPath);
            if (!fileExists)
            {
                // New column: FocusRating
                csvBuilder.AppendLine("StartTime,EndTime,DurationMinutes,SessionType,FocusRating");
            }

            // Append each session
            foreach (var session in sessions)
            {
                csvBuilder.AppendLine(
                    $"{session.StartTime},{session.EndTime},{session.DurationMinutes},{session.SessionType},{session.FocusRating}"
                );
            }

            // Write to CSV
            System.IO.File.AppendAllText(csvPath, csvBuilder.ToString());

            // Optional: send email with CSV data
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
                return StatusCode(500, $"Failed to send email. Error: {ex.Message}");
            }

            return Ok("Session recorded successfully.");
        }

        // GET: Calculate today's volume vs. yesterday => improvement percentage
        [HttpGet]
        public IActionResult GetDailyImprovement()
        {
            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");
            if (!System.IO.File.Exists(csvPath))
            {
                // No data => 0% improvement
                return Json(new { improvementPercent = 0, todayVolume = 0, yesterdayVolume = 0 });
            }

            var lines = System.IO.File.ReadAllLines(csvPath);
            // skip header
            var sessionData = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            // daily sums for "Work" sessions
            var dailyWorkTotals = new Dictionary<string, double>();

            foreach (var line in sessionData)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                // columns: StartTime, EndTime, DurationMinutes, SessionType, FocusRating
                var durationStr = parts[2];
                var sessionTypeStr = parts[3];
                var endTimeStr = parts[1];

                if (!string.Equals(sessionTypeStr, "Work", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!double.TryParse(durationStr, out double durationMinutes))
                    continue;

                if (!DateTime.TryParse(endTimeStr, out DateTime endTime))
                    continue;

                var localDate = endTime.ToLocalTime().Date;
                var dateKey = localDate.ToString("yyyy-MM-dd");

                if (!dailyWorkTotals.ContainsKey(dateKey))
                {
                    dailyWorkTotals[dateKey] = 0;
                }
                dailyWorkTotals[dateKey] += durationMinutes;
            }

            var today = DateTime.Now.Date;
            var todayKey = today.ToString("yyyy-MM-dd");
            var yesterdayKey = today.AddDays(-1).ToString("yyyy-MM-dd");

            dailyWorkTotals.TryGetValue(todayKey, out double todayTotal);
            dailyWorkTotals.TryGetValue(yesterdayKey, out double yesterdayTotal);

            double improvementPercent = 0;
            if (yesterdayTotal > 0)
            {
                improvementPercent = ((todayTotal - yesterdayTotal) / yesterdayTotal) * 100.0;
            }
            else
            {
                // if no data for yesterday
                improvementPercent = (todayTotal > 0) ? 100 : 0;
            }

            return Json(new
            {
                improvementPercent,
                todayVolume = todayTotal,
                yesterdayVolume = yesterdayTotal
            });
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
