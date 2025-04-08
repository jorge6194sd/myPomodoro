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

        // POST: Record completed sessions to CSV, including SessionCategory
        [HttpPost]
        public IActionResult RecordSession([FromBody] List<TimerSession> sessions)
        {
            if (sessions == null || !sessions.Any())
                return BadRequest("No sessions provided.");

            // 1) Build path to CSV in App_Data
            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath)!);

            var csvBuilder = new StringBuilder();

            // 2) If file doesn't exist, add a header row
            bool fileExists = System.IO.File.Exists(csvPath);
            if (!fileExists)
            {
                // 6 columns now:
                // StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory
                csvBuilder.AppendLine("StartTime,EndTime,DurationMinutes,SessionType,FocusRating,SessionCategory");
            }

            // 3) Append each session as a new CSV line
            foreach (var session in sessions)
            {
                // If session.SessionCategory is null, default to empty
                var cat = session.SessionCategory ?? "";

                // Build one CSV line
                csvBuilder.AppendLine(
                    $"{session.StartTime},{session.EndTime},{session.DurationMinutes}," +
                    $"{session.SessionType},{session.FocusRating},{cat}"
                );
            }

            // 4) Append to the CSV file
            System.IO.File.AppendAllText(csvPath, csvBuilder.ToString());

            // 5) Optional: send email if configured
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
            // Skip the header
            var sessionData = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            // We'll sum daily "Work" sessions
            var dailyWorkTotals = new Dictionary<DateTime, double>();

            foreach (var line in sessionData)
            {
                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                // columns: StartTime, EndTime, DurationMinutes, SessionType, FocusRating, SessionCategory
                // indexes:  0          1       2               3            4            5
                var durationStr = parts[2];
                var sessionTypeStr = parts[3];
                var endTimeStr = parts[1];

                // Only add to daily volume if "Work"
                if (!string.Equals(sessionTypeStr, "Work", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!double.TryParse(durationStr, out double durationMinutes))
                    continue;

                if (!DateTime.TryParse(endTimeStr, out DateTime endTime))
                    continue;

                // use local date from endTime
                var localDate = endTime.ToLocalTime().Date;

                if (!dailyWorkTotals.ContainsKey(localDate))
                {
                    dailyWorkTotals[localDate] = 0;
                }
                dailyWorkTotals[localDate] += durationMinutes;
            }

            var today = DateTime.Now.Date;
            dailyWorkTotals.TryGetValue(today, out double todayTotal);

            // Find the last day prior to today
            var previousDay = dailyWorkTotals.Keys
                .Where(d => d < today)
                .OrderByDescending(d => d)
                .FirstOrDefault();  // will be default(DateTime) if none

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
                // If no prior day or prior day = 0
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
