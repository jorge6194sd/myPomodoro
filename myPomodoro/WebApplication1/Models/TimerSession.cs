namespace YourProject.Models
{
    public class TimerSession
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationMinutes { get; set; }
        public string SessionType { get; set; }  // "Work" or "Rest"
        public int FocusRating { get; set; }     // 1-5 star rating

        // New Property to capture "Job" vs. "Personal" (or blank for breaks)
        public string SessionCategory { get; set; }
    }
}
