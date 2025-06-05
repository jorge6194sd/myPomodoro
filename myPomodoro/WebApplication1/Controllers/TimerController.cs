using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using System.Text;
using WebApplication1.Data;      // for TimerSessionRepository
using WebApplication1.Models; // for TimerSession
using System.Globalization;

namespace YourProject.Controllers
{
    public class TimerController : Controller
    {
        private readonly TimerSessionRepository _repo;
        private readonly IConfiguration _config;

        public TimerController(TimerSessionRepository repo, IConfiguration config)
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

            // Insert into SQL
            await _repo.InsertSessionsAsync(sessions);
            return Ok("Session recorded successfully (SQL).");
        }

        // GET: Return LAST 7 DAYS (Work sessions) in daily totals for the chart
        [HttpGet]
        public async Task<IActionResult> GetDailyTotals()
        {
            var allSessions = await _repo.GetAllAsync();
            var today = DateTime.Now.Date;
            var sevenDaysAgo = today.AddDays(-6);
            // This includes today as day 0, plus the 6 preceding days

            // 1) Filter only Work sessions in the last 7 days
            var grouped = allSessions
                .Where(s => s.SessionType.Equals("Work", StringComparison.OrdinalIgnoreCase)
                            && s.EndTime.Date >= sevenDaysAgo)
                .GroupBy(s => s.EndTime.Date)
                .Select(g => new {
                    Date = g.Key,
                    TotalMinutes = g.Sum(x => x.DurationMinutes)
                })
                .ToList();

            // 2) Fill in missing days from sevenDaysAgo..today
            var result = new List<object>();
            for (int i = 0; i < 7; i++)
            {
                var day = sevenDaysAgo.AddDays(i);
                var found = grouped.FirstOrDefault(d => d.Date == day);
                double minutes = found?.TotalMinutes ?? 0;
                result.Add(new
                {
                    Date = day,
                    TotalMinutes = minutes
                });
            }
            // result will have 7 entries, one for each day
            // e.g. [ { date: '2025-05-23T00:00:00', totalMinutes: 30 }, ... ]

            return Ok(result);
        }

        // GET: Calculate volumes & improvement from DB
        [HttpGet]
        public async Task<IActionResult> GetDailyImprovement()
        {
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

                if (!string.IsNullOrEmpty(session.SessionCategory))
                {
                    if (session.SessionCategory.Equals("Job", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!dailyJobTotals.ContainsKey(localDate))
                            dailyJobTotals[localDate] = 0;
                        dailyJobTotals[localDate] += session.DurationMinutes;
                    }
                    else if (session.SessionCategory.Equals("Personal", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!dailyPersonalTotals.ContainsKey(localDate))
                            dailyPersonalTotals[localDate] = 0;
                        dailyPersonalTotals[localDate] += session.DurationMinutes;
                    }
                }
            }

            var today = DateTime.Now.Date;
            dailyWorkTotals.TryGetValue(today, out double todayTotal);
            dailyJobTotals.TryGetValue(today, out double todayJobVolume);
            dailyPersonalTotals.TryGetValue(today, out double todayPersonalVolume);

            // find the day before today
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
    }
}
