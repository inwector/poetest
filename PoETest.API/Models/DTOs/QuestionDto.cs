namespace PoETest.API.Models.DTOs
{
    public class QuestionDto
    {
        public string QuestionText { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } 
        public List<AnswerOption> Options { get; set; } = new();
    }

    public class AnswerOption
    {
        public string Text { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        public string? ImageUrl { get; set; } = string.Empty;
    }
}
