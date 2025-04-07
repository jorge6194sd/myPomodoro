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

        // GET: Calculate today's volume vs the LAST prior day with data
        [HttpGet]
        public IActionResult GetDailyImprovement()
        {
            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");
            if (!System.IO.File.Exists(csvPath))
            {
                // No data => 0% improvement
                return Json(new { improvementPercent = 0, todayVolume = 0, previousDayVolume = 0 });
            }

            var lines = System.IO.File.ReadAllLines(csvPath);
            // skip header
            var sessionData = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            // daily sums for "Work" sessions
            var dailyWorkTotals = new Dictionary<DateTime, double>();

            foreach (var line in sessionData)
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                // columns: StartTime,EndTime,DurationMinutes,SessionType,FocusRating
                var durationStr = parts[2];
                var sessionTypeStr = parts[3];
                var endTimeStr = parts[1];

                // Only add to daily volume if it's a "Work" session
                if (!string.Equals(sessionTypeStr, "Work", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!double.TryParse(durationStr, out double durationMinutes))
                    continue;

                if (!DateTime.TryParse(endTimeStr, out DateTime endTime))
                    continue;

                // We'll consider local date from endTime
                var localDate = endTime.ToLocalTime().Date;

                if (!dailyWorkTotals.ContainsKey(localDate))
                {
                    dailyWorkTotals[localDate] = 0;
                }
                dailyWorkTotals[localDate] += durationMinutes;
            }

            var today = DateTime.Now.Date;

            // Sum for "today" if it exists
            dailyWorkTotals.TryGetValue(today, out double todayTotal);

            // We want the "most recent" day BEFORE "today" that we have data for
            var previousDay = dailyWorkTotals.Keys
                .Where(d => d < today)
                .OrderByDescending(d => d) // largest date < today
                .FirstOrDefault();         // or default(DateTime) if none

            double previousDayTotal = 0;
            if (previousDay != default(DateTime))
            {
                previousDayTotal = dailyWorkTotals[previousDay];
            }

            double improvementPercent = 0;
            if (previousDayTotal > 0)
            {
                improvementPercent = ((todayTotal - previousDayTotal) / previousDayTotal) * 100.0;
            }
            else
            {
                // If there's no prior day with data, or prior day total was 0
                improvementPercent = (todayTotal > 0) ? 100 : 0;
            }

            return Json(new
            {
                improvementPercent,
                todayVolume = todayTotal,
                previousDayVolume = previousDayTotal
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
