using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoETest.API.Data;
using PoETest.API.Models;
using PoETest.API.Models.DTOs;

namespace PoETest.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private readonly PoETestContext _context;
        private readonly Random _random = new();

        private readonly HashSet<int> ascendancyTypeIds = new()
        {
            27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 43, 44, 45
        };

        private readonly string[] difficulties = { "warmup", "easy", "medium", "hard", "impossible" };

        public GameController(PoETestContext context)
        {
            _context = context;
        }

        [HttpPost("start")]
        public async Task<ActionResult<GameSessionDto>> StartGame()
        {
            var allItems = await _context.Items
                .Include(i => i.Modifiers)
                .Include(i => i.Type)
                .ToListAsync();

            if (allItems.Count < 20)
                return BadRequest("Not enough items in database to start game.");

            var session = new GameSessionDto
            {
                SessionId = Guid.NewGuid().ToString(),
                Questions = new List<GameQuestionDto>(),
                CurrentQuestionIndex = 0,
                Score = 0
            };

            // Generate 5 questions per difficulty (25 total)
            foreach (var difficulty in difficulties)
            {
                var questions = await GenerateQuestionsForDifficulty(difficulty, 5, allItems);
                if (questions == null || questions.Count < 5)
                {
                    // Log what went wrong
                    Console.WriteLine($"Failed to generate questions for {difficulty}. Generated {questions?.Count ?? 0} questions.");
                    return BadRequest(new
                    {
                        error = $"Could not generate enough questions for {difficulty} difficulty.",
                        generated = questions?.Count ?? 0,
                        required = 5
                    });
                }

                session.Questions.AddRange(questions);
            }

            return Ok(session);
        }

        [HttpPost("submit")]
        public async Task<ActionResult<LeaderboardEntry>> SubmitScore([FromBody] SubmitScoreDto dto)
        {
            var entry = new LeaderboardEntry
            {
                Name = dto.Name,
                Score = dto.Score,
                Date = DateTime.UtcNow,
                TotalTimeMs = dto.TotalTimeMs,
            };

            _context.Leaderboard.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(entry);
        }

        [HttpGet("leaderboard")]
        public async Task<ActionResult<List<LeaderboardEntry>>> GetLeaderboard([FromQuery] int top = 10)
        {
            var entries = await _context.Leaderboard
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.Date)
                .Take(top)
                .ToListAsync();

            return Ok(entries);
        }

        private async Task<List<GameQuestionDto>> GenerateQuestionsForDifficulty(
    string difficulty,
    int count,
    List<Item> allItems)
        {
            var questions = new List<GameQuestionDto>();
            var usedTypeIds = new HashSet<int>();

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
            bool allowAscendancyQuestions = fameRange.Any(f => f is 2 or 3 or 4);

            // Debug logging
            Console.WriteLine($"[{difficulty}] Total candidates: {candidates.Count}");
            Console.WriteLine($"[{difficulty}] Ascendancy questions allowed: {allowAscendancyQuestions}");

            int attempts = 0;
            int maxAttempts = 100;
            int ascendancyAttempts = 0, modifierAttempts = 0, imageAttempts = 0;
            int ascendancySuccess = 0, modifierSuccess = 0, imageSuccess = 0;

            while (questions.Count < count && attempts < maxAttempts)
            {
                attempts++;

                GameQuestionDto? question = null;

                // Try ascendancy question if allowed
                if (allowAscendancyQuestions && _random.Next(3) == 0)
                {
                    ascendancyAttempts++;
                    question = await GenerateAscendancyQuestion(allItems, candidates, usedTypeIds);
                    if (question != null) ascendancySuccess++;
                }

                // Try modifier question
                if (question == null && _random.Next(2) == 0)
                {
                    modifierAttempts++;
                    question = GenerateModifierQuestion(allItems, candidates, usedTypeIds);
                    if (question != null) modifierSuccess++;
                }

                // Try image/text question
                if (question == null)
                {
                    imageAttempts++;
                    question = GenerateImageQuestion(allItems, candidates, usedTypeIds);
                    if (question != null) imageSuccess++;
                }

                if (question != null)
                {
                    question.Difficulty = difficulty;
                    questions.Add(question);
                }
            }

            Console.WriteLine($"[{difficulty}] Generated {questions.Count}/{count} questions in {attempts} attempts");
            Console.WriteLine($"[{difficulty}] Ascendancy: {ascendancySuccess}/{ascendancyAttempts}, Modifier: {modifierSuccess}/{modifierAttempts}, Image: {imageSuccess}/{imageAttempts}");

            return questions;
        }

        private async Task<GameQuestionDto?> GenerateAscendancyQuestion(
            List<Item> allItems,
            List<Item> candidates,
            HashSet<int> usedTypeIds)
        {
            var ascendancyItems = candidates
                .Where(i => ascendancyTypeIds.Contains(i.TypeId) && !usedTypeIds.Contains(i.TypeId))
                .ToList();

            if (!ascendancyItems.Any())
                return null;

            var chosenItem = ascendancyItems[_random.Next(ascendancyItems.Count)];
            var allAscendancies = allItems.Where(i => ascendancyTypeIds.Contains(i.TypeId)).ToList();

            var chosenMods = chosenItem.Modifiers.ToList();
            if (!chosenMods.Any())
                return null;

            if (_random.Next(2) == 0)
            {
                // TYPE A: Modifier Set → Ascendancy
                // IMPORTANT: Only use modifiers that are UNIQUE to this ascendancy
                var modifierSet = chosenItem.Modifiers
                    .Where(m => !allAscendancies
                        .Where(a => a.TypeId != chosenItem.TypeId)
                        .Any(a => a.Modifiers.Any(mod => mod.ModifierText == m.ModifierText)))
                    .ToList();

                // If no unique modifiers, skip this question type
                if (!modifierSet.Any())
                {
                    return null; // Try another question type
                }

                var questionText = "Which ascendancy grants the following?<ul>" +
                                   string.Join("", modifierSet.Select(m => $"<li>{m.ModifierText}</li>")) +
                                   "</ul>";

                var options = new List<AnswerOption>
                {
                    new AnswerOption { Text = chosenItem.Type.Name, IsCorrect = true }
                };

                var otherAscendancyNames = allAscendancies
                    .Select(i => i.Type.Name)  // This should already be the ascendancy name
                    .Distinct()
                    .Where(name => name != chosenItem.Type.Name)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();

                options.AddRange(otherAscendancyNames.Select(name => new AnswerOption
                {
                    Text = name,
                    IsCorrect = false
                }));

                usedTypeIds.Add(chosenItem.TypeId);

                return new GameQuestionDto
                {
                    QuestionText = questionText,
                    Options = options.OrderBy(x => Guid.NewGuid()).ToList()
                };
            }
            else
            {
                // TYPE B: Right/Wrong Modifier Set - Always 1 correct, 3 wrong
                var correctMods = chosenMods.Select(m => m.ModifierText).ToList();

                var wrongMods = allAscendancies
                    .Where(i => i.TypeId != chosenItem.TypeId)
                    .SelectMany(i => i.Modifiers)
                    .Select(m => m.ModifierText)
                    .Distinct()
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();

                if (!wrongMods.Any() || wrongMods.Count < 3)
                    return null;

                // Always show 1 correct and 3 wrong modifiers
                var shownMods = new List<string>();
                shownMods.Add(correctMods[_random.Next(correctMods.Count)]);
                shownMods.AddRange(wrongMods);

                usedTypeIds.Add(chosenItem.TypeId);

                return new GameQuestionDto
                {
                    QuestionText = $"Which of these modifiers belongs to the ascendancy {chosenItem.Type.Name}?",
                    Options = shownMods
                        .OrderBy(x => Guid.NewGuid())
                        .Select(m => new AnswerOption
                        {
                            Text = m,
                            IsCorrect = correctMods.Contains(m)
                        })
                        .ToList(),
                    CorrectAnswer = correctMods.First(m => shownMods.Contains(m))
                };
            }
        }

        private GameQuestionDto? GenerateModifierQuestion(
    List<Item> allItems,
    List<Item> candidates,
    HashSet<int> usedItemIds)
        {
            var availableCandidates = candidates.Where(i => !usedItemIds.Contains(i.Id)).ToList();

            // Check uniqueness across ALL items
            var allModifiers = allItems
                .SelectMany(i => i.Modifiers.Select(m => new { ItemId = i.Id, ModifierText = m.ModifierText }))
                .ToList();

            var modifiersUniqueGlobally = allModifiers
                .GroupBy(x => x.ModifierText)
                .Where(g => g.Count() == 1)
                .Select(g => g.First().ModifierText)
                .ToHashSet();

            var uniqueModifiers = availableCandidates
                .SelectMany(i => i.Modifiers.Select(m => new { Item = i, Modifier = m }))
                .Where(x => modifiersUniqueGlobally.Contains(x.Modifier.ModifierText))
                .ToList();

            if (!uniqueModifiers.Any())
                return null;

            var chosen = uniqueModifiers[_random.Next(uniqueModifiers.Count)];
            var correctItem = chosen.Item;

            bool isAscendancy = ascendancyTypeIds.Contains(correctItem.TypeId);

            List<AnswerOption> options;

            if (isAscendancy)
            {
                var incorrectAscendancies = allItems
                    .Where(i => ascendancyTypeIds.Contains(i.TypeId) && i.TypeId != correctItem.TypeId)
                    .Select(i => i.Type.Name)
                    .Distinct()
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();

                if (incorrectAscendancies.Count < 3)
                    return null;

                options = new List<AnswerOption>
        {
            new AnswerOption { Text = correctItem.Type.Name, IsCorrect = true }
        };

                options.AddRange(incorrectAscendancies.Select(name => new AnswerOption
                {
                    Text = name,
                    IsCorrect = false
                }));
            }
            else
            {
                // Try same fame first
                var incorrectItems = candidates
                    .Where(i => i.Id != correctItem.Id && i.TypeId == correctItem.TypeId)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();

                // If not enough, try one fame level lower
                if (incorrectItems.Count < 3)
                {
                    int fallbackFame = correctItem.Fame - 1;
                    if (fallbackFame >= 1)
                    {
                        incorrectItems = allItems
                            .Where(i => i.Id != correctItem.Id &&
                                       i.TypeId == correctItem.TypeId &&
                                       i.Fame == fallbackFame)
                            .OrderBy(_ => Guid.NewGuid())
                            .Take(3)
                            .ToList();
                    }
                }

                // If still not enough, expand to all fame levels as last resort
                if (incorrectItems.Count < 3)
                {
                    incorrectItems = allItems
                        .Where(i => i.Id != correctItem.Id && i.TypeId == correctItem.TypeId)
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(3)
                        .ToList();
                }

                Console.WriteLine($"  [Modifier] Unique '{correctItem.Type.Name}' - Found {incorrectItems.Count}/3 incorrect options (TypeId: {correctItem.TypeId})");

                if (incorrectItems.Count < 3)
                    return null;

                options = new List<AnswerOption>
        {
            new AnswerOption { Text = correctItem.Name, IsCorrect = true }
        };

                options.AddRange(incorrectItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false
                }));
            }

            usedItemIds.Add(correctItem.Id);

            return new GameQuestionDto
            {
                QuestionText = $"Which '{(isAscendancy ? "Ascendancy" : "Unique")}' has the modifier \"{chosen.Modifier.ModifierText}\"?",
                Options = options.OrderBy(x => Guid.NewGuid()).ToList(),
                CorrectAnswer = isAscendancy ? correctItem.Type.Name : correctItem.Name
            };
        }

        private GameQuestionDto? GenerateImageQuestion(
    List<Item> allItems,
    List<Item> candidates,
    HashSet<int> usedItemIds)
        {
            var availableCandidates = candidates
                .Where(i => !usedItemIds.Contains(i.Id) && !ascendancyTypeIds.Contains(i.TypeId))
                .ToList();

            Console.WriteLine($"  [Image] Available candidates: {availableCandidates.Count}");

            if (!availableCandidates.Any())
                return null;

            var correctImageItem = availableCandidates[_random.Next(availableCandidates.Count)];

            // Try same fame first
            var incorrectImageItems = candidates
                .Where(i => i.Id != correctImageItem.Id && i.TypeId == correctImageItem.TypeId && !ascendancyTypeIds.Contains(i.TypeId))
                .OrderBy(_ => Guid.NewGuid())
                .Take(3)
                .ToList();

            // If not enough, try one fame level lower
            if (incorrectImageItems.Count < 3)
            {
                int fallbackFame = correctImageItem.Fame - 1;
                if (fallbackFame >= 1)
                {
                    incorrectImageItems = allItems
                        .Where(i => i.Id != correctImageItem.Id &&
                                   i.TypeId == correctImageItem.TypeId &&
                                   i.Fame == fallbackFame &&
                                   !ascendancyTypeIds.Contains(i.TypeId))
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(3)
                        .ToList();
                }
            }

            // If still not enough, expand to all fame levels as last resort
            if (incorrectImageItems.Count < 3)
            {
                incorrectImageItems = allItems
                    .Where(i => i.Id != correctImageItem.Id &&
                               i.TypeId == correctImageItem.TypeId &&
                               !ascendancyTypeIds.Contains(i.TypeId))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();
            }

            Console.WriteLine($"  [Image] Item '{correctImageItem.Name}' (TypeId: {correctImageItem.TypeId}) - Found {incorrectImageItems.Count}/3 incorrect options");

            if (incorrectImageItems.Count < 3)
                return null;

            bool isImageQuestion = _random.Next(2) == 0;
            var imageOptions = new List<AnswerOption>();

            if (isImageQuestion)
            {
                imageOptions.Add(new AnswerOption
                {
                    Text = correctImageItem.Name,
                    IsCorrect = true,
                    ImageUrl = correctImageItem.Link
                });
                imageOptions.AddRange(incorrectImageItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false,
                    ImageUrl = i.Link
                }));

                usedItemIds.Add(correctImageItem.Id);

                return new GameQuestionDto
                {
                    QuestionText = $"Which of these is {correctImageItem.Name}?",
                    Options = imageOptions.OrderBy(x => Guid.NewGuid()).ToList(),
                    CorrectAnswer = correctImageItem.Name
                };
            }
            else
            {
                imageOptions.Add(new AnswerOption { Text = correctImageItem.Name, IsCorrect = true });
                imageOptions.AddRange(incorrectImageItems.Select(i => new AnswerOption
                {
                    Text = i.Name,
                    IsCorrect = false
                }));

                usedItemIds.Add(correctImageItem.Id);

                return new GameQuestionDto
                {
                    QuestionText = "Which item is shown in this picture?",
                    ImageUrl = correctImageItem.Link,
                    Options = imageOptions.OrderBy(x => Guid.NewGuid()).ToList(),
                    CorrectAnswer = correctImageItem.Name
                };
            }
        }
    }
}