namespace YourProject.Models
{
    public class TimerSession
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMinutes { get; set; }
        public string SessionType { get; set; }
        public int FocusRating { get; set; }
        public string? SessionCategory { get; set; }  // "Job" or "Personal"
    }
}
