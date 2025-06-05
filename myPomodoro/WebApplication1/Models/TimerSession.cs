namespace WebApplication1.Models
{
    public class TimerSession
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMinutes { get; set; }
        public string SessionType { get; set; }  // e.g., "Work" or "Rest"
    }
}
