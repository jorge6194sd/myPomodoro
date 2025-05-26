using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using YourProject.Data;      // <-- for TimerSessionRepository
using WebApplication1.Models; // <-- for TimerSession
using System.Text;           // (if you still want email logic using StringBuilder)
using System.Globalization;
using System.Threading.Tasks;

namespace YourProject.Controllers
{
    public class TimerController : Controller
    {
        private readonly TimerSessionRepository _repo;
        private readonly IConfiguration _config;

        // If you need IWebHostEnvironment for other things, you can keep it.
        public TimerController(
            TimerSessionRepository repo,
            IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        // Serve the main Timer page
        public IActionResult Index()
        {
            return View();
        }

        // POST: Record completed sessions to SQL (Dapper)
        [HttpPost]
        public async Task<IActionResult> RecordSession([FromBody] List<TimerSession> sessions)
        {
            if (sessions == null || sessions.Count == 0)
                return BadRequest("No sessions provided.");

            // Insert into SQL instead of writing CSV
            await _repo.InsertSessionsAsync(sessions);

            // Optional: email logic, if your _config has EmailSettings
            try
            {
                var shouldSendEmail = _config.GetValue<bool>("EmailSettings:SendEmail");
                if (shouldSendEmail)
                {
                    // Build an email body from the sessions
                    var sb = new StringBuilder();
                    foreach (var s in sessions)
                    {
                        sb.AppendLine($"{s.StartTime}, {s.EndTime}, {s.DurationMinutes}, {s.SessionType}, {s.FocusRating}, {s.SessionCategory}");
                    }
                    SendEmail(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to send email. Error: {ex.Message}");
            }

            return Ok("Session recorded successfully (SQL).");
        }

        // GET: Calculate volumes & improvement from DB, not from CSV
        [HttpGet]
        public async Task<IActionResult> GetDailyImprovement()
        {
            // We'll pull all sessions from DB
            var allSessions = await _repo.GetAllAsync();
            if (allSessions == null || !allSessions.Any())
            {
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

            // Group "Work" sessions by date
            var dailyWorkTotals = new Dictionary<DateTime, double>();
            var dailyJobTotals = new Dictionary<DateTime, double>();
            var dailyPersonalTotals = new Dictionary<DateTime, double>();

            foreach (var session in allSessions)
            {
                if (!session.SessionType.Equals("Work", StringComparison.OrdinalIgnoreCase))
                    continue;

                var localDate = session.EndTime.ToLocalTime().Date;
                if (!dailyWorkTotals.ContainsKey(localDate))
                    dailyWorkTotals[localDate] = 0;
                dailyWorkTotals[localDate] += session.DurationMinutes;

                // job vs. personal
                if (session.SessionCategory != null &&
                    session.SessionCategory.Equals("Job", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dailyJobTotals.ContainsKey(localDate))
                        dailyJobTotals[localDate] = 0;
                    dailyJobTotals[localDate] += session.DurationMinutes;
                }
                else if (session.SessionCategory != null &&
                         session.SessionCategory.Equals("Personal", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dailyPersonalTotals.ContainsKey(localDate))
                        dailyPersonalTotals[localDate] = 0;
                    dailyPersonalTotals[localDate] += session.DurationMinutes;
                }
            }

            var today = DateTime.Now.Date;
            dailyWorkTotals.TryGetValue(today, out double todayTotal);
            dailyJobTotals.TryGetValue(today, out double todayJobVolume);
            dailyPersonalTotals.TryGetValue(today, out double todayPersonalVolume);

            // Find the most recent day before today
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

        private void SendEmail(string content)
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
                    Body = "Here are the completed timer sessions:\n\n" + content
                };
                mailMessage.To.Add(new MailAddress(toEmail));

                client.Send(mailMessage);
            }
        }
    }
}
