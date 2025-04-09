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

        // POST: Record completed sessions to CSV, with SessionCategory included
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
                // 6 columns: StartTime,EndTime,DurationMinutes,SessionType,FocusRating,SessionCategory
                csvBuilder.AppendLine("StartTime,EndTime,DurationMinutes,SessionType,FocusRating,SessionCategory");
            }

            // Append each session
            foreach (var session in sessions)
            {
                var cat = session.SessionCategory ?? "";
                csvBuilder.AppendLine(
                    $"{session.StartTime},{session.EndTime},{session.DurationMinutes}," +
                    $"{session.SessionType},{session.FocusRating},{cat}"
                );
            }

            System.IO.File.AppendAllText(csvPath, csvBuilder.ToString());

            // Optional: send email
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

        // GET: Calculate volumes & improvement (all Work, Job-only, Personal-only)
        [HttpGet]
        public IActionResult GetDailyImprovement()
        {
            var csvPath = Path.Combine(_env.ContentRootPath, "App_Data", "TimerSessions.csv");
            if (!System.IO.File.Exists(csvPath))
            {
                // No data => all 0
                return Json(new
                {
                    improvementPercent = 0,
                    todayVolume = 0,
                    previousDayVolume = 0,
                    todayJobVolume = 0,
                    previousDayJobVolume = 0,
                    todayPersonalVolume = 0,
                    previousDayPersonalVolume = 0
                });
            }

            var lines = System.IO.File.ReadAllLines(csvPath);
            var sessionData = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l));

            // Summaries
            var dailyWorkTotals = new Dictionary<DateTime, double>();
            var dailyJobTotals = new Dictionary<DateTime, double>();
            var dailyPersonalTotals = new Dictionary<DateTime, double>();

            foreach (var line in sessionData)
            {
                var parts = line.Split(',');
                if (parts.Length < 6) continue;

                // columns:
                // [0] StartTime
                // [1] EndTime
                // [2] DurationMinutes
                // [3] SessionType
                // [4] FocusRating
                // [5] SessionCategory
                var endTimeStr = parts[1];
                var durationStr = parts[2];
                var sessionTypeStr = parts[3];
                var categoryStr = parts[5];

                // only consider "Work" sessions
                if (!string.Equals(sessionTypeStr, "Work", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!double.TryParse(durationStr, out double durationMinutes))
                    continue;

                if (!DateTime.TryParse(endTimeStr, out DateTime endTime))
                    continue;

                var localDate = endTime.ToLocalTime().Date;

                // All work
                if (!dailyWorkTotals.ContainsKey(localDate))
                    dailyWorkTotals[localDate] = 0;
                dailyWorkTotals[localDate] += durationMinutes;

                // "Job" vs. "Personal"
                if (categoryStr.Equals("Job", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dailyJobTotals.ContainsKey(localDate))
                        dailyJobTotals[localDate] = 0;
                    dailyJobTotals[localDate] += durationMinutes;
                }
                else if (categoryStr.Equals("Personal", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dailyPersonalTotals.ContainsKey(localDate))
                        dailyPersonalTotals[localDate] = 0;
                    dailyPersonalTotals[localDate] += durationMinutes;
                }
            }

            var today = DateTime.Now.Date;
            dailyWorkTotals.TryGetValue(today, out double todayTotal);
            dailyJobTotals.TryGetValue(today, out double todayJobVolume);
            dailyPersonalTotals.TryGetValue(today, out double todayPersonalVolume);

            // find last day prior to today
            var previousDay = dailyWorkTotals.Keys
                .Where(d => d < today)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            double previousDayTotal = 0;
            double previousDayJobVolume = 0;
            double previousDayPersonalVolume = 0;

            if (previousDay != default(DateTime))
            {
                previousDayTotal = dailyWorkTotals[previousDay];
                dailyJobTotals.TryGetValue(previousDay, out previousDayJobVolume);
                dailyPersonalTotals.TryGetValue(previousDay, out previousDayPersonalVolume);
            }

            double improvementPercent = 0;
            if (previousDayTotal > 0)
            {
                improvementPercent = ((todayTotal - previousDayTotal) / previousDayTotal) * 100.0;
            }
            else
            {
                improvementPercent = (todayTotal > 0) ? 100 : 0;
            }

            return Json(new
            {
                improvementPercent,
                todayVolume = todayTotal,
                previousDayVolume = previousDayTotal,

                todayJobVolume,
                previousDayJobVolume,
                todayPersonalVolume,
                previousDayPersonalVolume
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
