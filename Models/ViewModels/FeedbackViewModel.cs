namespace CTSHIPDashboard.Models.ViewModels
{
    public class FeedbackViewModel : BaseViewModel
    {
        public List<Feedback> Feedbacks { get; set; }
        public double AverageSatisfaction { get; set; }
        public double ResolutionIndex { get; set; } // % resolved
        public Dictionary<int, int> ScoresDistribution { get; set; } // Score: Count
    }
}