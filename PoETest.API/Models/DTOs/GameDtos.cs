namespace PoETest.API.Models.DTOs
{
    public class GameSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public List<GameQuestionDto> Questions { get; set; } = new();
        public int CurrentQuestionIndex { get; set; }
        public int Score { get; set; }
    }

    public class GameQuestionDto
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public List<AnswerOption> Options { get; set; } = new();
        public string CorrectAnswer { get; set; } = string.Empty;
        public string Difficulty { get; set; } = string.Empty;
        public long StartTimestamp { get; set; }
    }

    public class SubmitScoreDto
    {
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public long TotalTimeMs { get; set; }
    }

    public class AnswerSubmissionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public int QuestionIndex { get; set; }
        public string Answer { get; set; } = string.Empty;
    }
}