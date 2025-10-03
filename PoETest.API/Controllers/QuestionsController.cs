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

        // Ascendancy ItemType Ids
        private readonly HashSet<int> ascendancyTypeIds = new()
        {
            27, // Assassin
            28, // Berserker
            29, // Champion
            30, // Chieftain
            31, // Deadeye
            32, // Elementalist
            33, // Gladiator
            34, // Guardian
            35, // Hierophant
            36, // Inquisitor
            37, // Juggernaut
            38, // Necromancer
            39, // Occultist
            40, // Pathfinder
            41, // Saboteur
            43, // Slayer
            44, // Trickster
            45  // Warden
        };

        public QuestionsController(PoETestContext context)
        {
            _context = context;
        }

        [HttpGet("random/{difficulty}")]
        public async Task<ActionResult<QuestionDto>> GetRandomQuestion(string difficulty)
        {
            var allItems = await _context.Items
                .Include(i => i.Modifiers)
                .Include(i => i.Type)
                .ToListAsync();

            if (allItems.Count < 4)
                return BadRequest("Not enough items in the database to generate a question.");

            // 🎯 Difficulty → Fame mapping
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

            // Allow ascendancy questions only for difficulties 2, 3, 4
            bool allowAscendancyQuestions = fameRange.Any(f => f is 2 or 3 or 4);

            // Randomly choose between Ascendancy, Modifier, or Image/Text
            if (allowAscendancyQuestions && _random.Next(3) == 0)
            {
                var ascendancyItems = candidates.Where(i => ascendancyTypeIds.Contains(i.TypeId)).ToList();
                if (ascendancyItems.Any())
                {
                    var chosenItem = ascendancyItems[_random.Next(ascendancyItems.Count)];
                    var allAscendancies = allItems.Where(i => ascendancyTypeIds.Contains(i.TypeId)).ToList();

                    var chosenMods = chosenItem.Modifiers.ToList();
                    if (!chosenMods.Any())
                        return BadRequest("No modifiers available for chosen ascendancy item.");

                    if (_random.Next(2) == 0)
                    {
                        // ---- TYPE A: Modifier Set → Ascendancy ----
                        var modifierSet = chosenItem.Modifiers.ToList();

                        var questionText = "Which ascendancy grants the following?\n" +
                                           string.Join("\n", modifierSet.Select(m => "- " + m.ModifierText));

                        var options = new List<AnswerOption>
                        {
                            new AnswerOption { Text = chosenItem.Type.Name, IsCorrect = true }
                        };

                        options.AddRange(allAscendancies
                            .Where(i => i.TypeId != chosenItem.TypeId)
                            .OrderBy(_ => Guid.NewGuid())
                            .Take(3)
                            .Select(i => new AnswerOption
                            {
                                Text = i.Type.Name,
                                IsCorrect = false
                            }));

                        return Ok(new QuestionDto
                        {
                            QuestionText = questionText,
                            Options = options.OrderBy(x => Guid.NewGuid()).ToList()
                        });
                    }

                    else
                    {
                        // ---- TYPE B: Right/Wrong Modifier Set ----
                        var correctMods = chosenMods.Select(m => m.ModifierText).ToList();

                        var wrongMods = allAscendancies
                            .Where(i => i.TypeId != chosenItem.TypeId)
                            .SelectMany(i => i.Modifiers)
                            .Select(m => m.ModifierText)
                            .Distinct()
                            .OrderBy(_ => Guid.NewGuid())
                            .Take(10)
                            .ToList();

                        bool mostlyCorrect = _random.Next(2) == 0;
                        List<string> shownMods;

                        if (mostlyCorrect && correctMods.Count >= 3)
                        {
                            shownMods = correctMods.OrderBy(_ => Guid.NewGuid()).Take(3).ToList();
                            shownMods.Add(wrongMods[_random.Next(wrongMods.Count)]);
                        }
                        else
                        {
                            shownMods = wrongMods.Take(3).ToList();
                            shownMods.Add(correctMods[_random.Next(correctMods.Count)]);
                        }

                        return Ok(new QuestionDto
                        {
                            QuestionText = $"Which of these modifiers belong to the ascendancy {chosenItem.Type.Name}?",
                            Options = shownMods
                                .Select(m => new AnswerOption
                                {
                                    Text = m,
                                    IsCorrect = correctMods.Contains(m)
                                })
                                .OrderBy(x => Guid.NewGuid())
                                .ToList()
                        });
                    }
                }
            }

            // ---- IMAGE/TEXT OR MODIFIER QUESTION (Fallback) ----
            bool useModifierQuestion = _random.Next(2) == 0;

            if (useModifierQuestion)
            {
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

                    var incorrectItems = candidates
                        .Where(i => i.Id != correctItem.Id && i.TypeId == correctItem.TypeId)
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(3)
                        .ToList();

                    if (incorrectItems.Count >= 3)
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

                        return Ok(new QuestionDto
                        {
                            QuestionText = $"Which item has the modifier \"{chosen.Modifier.ModifierText}\"?",
                            Options = options.OrderBy(x => Guid.NewGuid()).ToList()
                        });
                    }
                }
            }

            // ---- IMAGE/TEXT QUESTION ----
            var correctImageItem = candidates[_random.Next(candidates.Count)];

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

                return Ok(new QuestionDto
                {
                    QuestionText = $"Which of these is {correctImageItem.Name}?",
                    Options = imageOptions.OrderBy(x => Guid.NewGuid()).ToList()
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

                return Ok(new QuestionDto
                {
                    QuestionText = "Which item is shown in this picture?",
                    ImageUrl = correctImageItem.Link,
                    Options = imageOptions.OrderBy(x => Guid.NewGuid()).ToList()
                });
            }

            // Fallback return
            return BadRequest("Could not generate a question with the given difficulty and data.");
        }
    }
}
