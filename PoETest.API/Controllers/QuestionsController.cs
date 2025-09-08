using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoETest.API.Data;
using PoETest.API.Models.DTOs;

namespace PoETest.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionsController : ControllerBase
    {
        private readonly PoETestContext _context;
        private readonly Random _random = new();

        public QuestionsController(PoETestContext context)
        {
            _context = context;
        }

        [HttpGet("random/{difficulty}")]
        public async Task<ActionResult<QuestionDto>> GetRandomQuestion(string difficulty)
        {
            var allItems = await _context.Items.ToListAsync();
            if (allItems.Count < 4)
                return BadRequest("Not enough items in the database to generate a question.");

            // Map difficulty to fame filter
            IEnumerable<int> fameRange = difficulty.ToLower() switch
            {
                "warmup" => new[] { 5 },
                "easy" => new[] { 4, 5 },
                "medium" => new[] { 3, 4 },
                "hard" => new[] { 2, 3 },
                "impossible" => new[] { 1, 2 },
                _ => Enumerable.Range(1, 5) // default = all items
            };

            var candidates = allItems.Where(i => fameRange.Contains(i.Fame)).ToList();

            if (candidates.Count < 4)
                return BadRequest("Not enough items in the database for this difficulty level.");

            // Pick correct item
            var correctItem = candidates[_random.Next(candidates.Count)];

            // Pick 3 incorrect items (must also come from same difficulty pool!)
            var incorrectItems = candidates
                .Where(i => i.Id != correctItem.Id)
                .OrderBy(x => Guid.NewGuid())
                .Take(3)
                .ToList();

            // Randomly decide image → text OR text → image
            bool isImageQuestion = _random.Next(2) == 0;
            var options = new List<AnswerOption>();

            if (isImageQuestion)
            {
                // Question with text, answers = images
                options.Add(new AnswerOption { Text = correctItem.Name, IsCorrect = true, ImageUrl = correctItem.Link });
                options.AddRange(incorrectItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false,
                    ImageUrl = i.Link
                }));

                options = options.OrderBy(x => Guid.NewGuid()).ToList();

                return Ok(new QuestionDto
                {
                    QuestionText = $"Which of these is {correctItem.Name}?",
                    ImageUrl = null,
                    Options = options
                });
            }
            else
            {
                // Question with image, answers = text
                options.Add(new AnswerOption { Text = correctItem.Name, IsCorrect = true, ImageUrl = "" });
                options.AddRange(incorrectItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false,
                    ImageUrl = ""
                }));

                options = options.OrderBy(x => Guid.NewGuid()).ToList();

                return Ok(new QuestionDto
                {
                    QuestionText = "Which item is shown in this picture?",
                    ImageUrl = correctItem.Link,
                    Options = options
                });
            }
        }

    }
}
