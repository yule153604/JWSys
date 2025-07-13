namespace JWSystem.Models
{
    public class AppSettings
    {
        public string BaseUrl { get; set; } = "";
        public string LoginPath { get; set; } = "";
        public string PushPlusUrl { get; set; } = "";
        public SelectorSettings Selectors { get; set; } = new();
    }

    public class SelectorSettings
    {
        public GradeSelectors Grade { get; set; } = new();
        public ScheduleSelectors Schedule { get; set; } = new();
        public ExamSelectors Exam { get; set; } = new();
        public EvaluationSelectors Evaluation { get; set; } = new();
    }

    public class GradeSelectors
    {
        public string Semester { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Score { get; set; } = "";
        public string Credit { get; set; } = "";
        public string GPA { get; set; } = "";
    }

    public class ScheduleSelectors
    {
        public string CourseTitle { get; set; } = "";
        public string CourseTable { get; set; } = "";
    }

    public class ExamSelectors
    {
        public string TermOptions { get; set; } = "";
        public string ExamListTable { get; set; } = "";
    }

    public class EvaluationSelectors
    {
        public string EvaluationLinks { get; set; } = "";
    }

    public class UserSecrets
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PushToken { get; set; } = "";
    }
}
