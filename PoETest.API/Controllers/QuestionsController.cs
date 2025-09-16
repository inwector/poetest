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
            var allItems = await _context.Items
                .Include(i => i.Modifiers)
                .Include(i => i.Type) // load item type
                .ToListAsync();

            if (allItems.Count < 4)
                return BadRequest("Not enough items in the database to generate a question.");

            // Map difficulty to fame filter
            IEnumerable<int> fameRange = difficulty.ToLower() switch
            {
                "warmup" => new[] { 5 },
                "easy" => new[] { 4, 5 },
                "medium" => new[] { 3, 4 },
                "hard" => new[] { 2, 3 },
                "impossible" => new[] { 1 },
                _ => Enumerable.Range(1, 5)
            };

            var candidates = allItems.Where(i => fameRange.Contains(i.Fame)).ToList();

            if (candidates.Count < 4)
                return BadRequest("Not enough items in the database for this difficulty level.");

            // 🎲 Randomly pick question type: image/text OR modifier
            bool useModifierQuestion = _random.Next(2) == 0;

            if (useModifierQuestion)
            {
                // ---- MODIFIER QUESTION ----
                var allModifiers = candidates
                    .SelectMany(i => i.Modifiers.Select(m => new { Item = i, Modifier = m }))
                    .ToList();

                var uniqueModifiers = allModifiers
                    .GroupBy(x => x.Modifier.ModifierText)
                    .Where(g => g.Count() == 1)
                    .Select(g => g.First())
                    .ToList();

                if (uniqueModifiers.Any())
                {
                    var chosen = uniqueModifiers[_random.Next(uniqueModifiers.Count)];
                    var correctItem = chosen.Item;

                    // restrict incorrect answers to same TypeId
                    var incorrectItems = candidates
                        .Where(i => i.Id != correctItem.Id && i.TypeId == correctItem.TypeId)
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(3)
                        .ToList();

                    if (incorrectItems.Count < 3)
                    {
                        // fallback if not enough same-type items
                        useModifierQuestion = false;
                    }
                    else
                    {
                        var options = new List<AnswerOption>
                {
                    new AnswerOption { Text = correctItem.Name, IsCorrect = true }
                };

                        options.AddRange(incorrectItems.Select(i => new AnswerOption
                        {
                            Text = i.Name,
                            IsCorrect = false
                        }));

                        options = options.OrderBy(x => Guid.NewGuid()).ToList();

                        return Ok(new QuestionDto
                        {
                            QuestionText = $"Which item has the modifier \"{chosen.Modifier.ModifierText}\"?",
                            ImageUrl = null,
                            Options = options
                        });
                    }
                }
            }

            // ---- IMAGE/TEXT QUESTION ----
            var correctImageItem = candidates[_random.Next(candidates.Count)];

            // restrict incorrect answers to same TypeId
            var incorrectImageItems = candidates
                .Where(i => i.Id != correctImageItem.Id && i.TypeId == correctImageItem.TypeId)
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .ToList();

            if (incorrectImageItems.Count < 3)
                return BadRequest("Not enough items of the same type to generate question options.");

            bool isImageQuestion = _random.Next(2) == 0;
            var imageOptions = new List<AnswerOption>();

            if (isImageQuestion)
            {
                imageOptions.Add(new AnswerOption { Text = correctImageItem.Name, IsCorrect = true, ImageUrl = correctImageItem.Link });
                imageOptions.AddRange(incorrectImageItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false,
                    ImageUrl = i.Link
                }));

                imageOptions = imageOptions.OrderBy(x => Guid.NewGuid()).ToList();

                return Ok(new QuestionDto
                {
                    QuestionText = $"Which of these is {correctImageItem.Name}?",
                    ImageUrl = null,
                    Options = imageOptions
                });
            }
            else
            {
                imageOptions.Add(new AnswerOption { Text = correctImageItem.Name, IsCorrect = true });
                imageOptions.AddRange(incorrectImageItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false
                }));

                imageOptions = imageOptions.OrderBy(x => Guid.NewGuid()).ToList();

                return Ok(new QuestionDto
                {
                    QuestionText = "Which item is shown in this picture?",
                    ImageUrl = correctImageItem.Link,
                    Options = imageOptions
                });
            }
        }



    }
}
