namespace CTSHIPDashboard.Models
{
    public class Feedback
    {
        public int Id { get; set; }
        public int EnrolleeId { get; set; }
        public Enrollee Enrollee { get; set; }
        public int ProviderId { get; set; }
        public Provider Provider { get; set; }
        public int SatisfactionScore { get; set; } // 1-10
        public string Comments { get; set; }
        public bool Resolved { get; set; }
    }
}
