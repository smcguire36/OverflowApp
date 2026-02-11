using Contracts;
using FastExpressionCompiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using QuestionService.Services;
using System.Security.Claims;
using Wolverine;

namespace QuestionService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionsController(QuestionDbContext db, IMessageBus bus, TagService tagService) : ControllerBase
    {
        [Authorize]
        [HttpPost]
        public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
        {
            if (!await tagService.AreTagsValidAsync(dto.Tags))
                return BadRequest("Invalid tags");

            /*
                        var validTags = await db.Tags.Where(x => dto.Tags.Contains(x.Slug)).ToListAsync();
                        var missingTags = dto.Tags.Except(validTags.Select(x => x.Slug)).ToList();
                        if (missingTags.Count != 0)
                        {
                            return BadRequest($"Invalid tags: {string.Join(", ", missingTags)}");
                        }
            */
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");

            if (userId is null || name is null) return BadRequest("Cannot get user details");

            var question = new Question
            {
                Title = dto.Title,
                Content = dto.Content,
                AskerId = userId,
                AskerDisplayName = name,
                CreatedAt = DateTime.UtcNow,
                TagSlugs = dto.Tags
            };

            db.Questions.Add(question);
            await db.SaveChangesAsync();

            await bus.PublishAsync(
                new QuestionCreated(question.Id, question.Title, question.Content, question.CreatedAt, question.TagSlugs)
            );

            return Created($"/questions/{question.Id}", question);
        }

        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
        {
            var query = db.Questions.AsQueryable();
            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(x => x.TagSlugs.Contains(tag));
            }

            return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(string id)
        {
            var question = await db.Questions
                .Include(q => q.Answers)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question is null) return NotFound();

            await db.Questions.Where(x => x.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ViewCount, x => x.ViewCount + 1));

            return question;
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != question.AskerId) return Forbid();

            if (!await tagService.AreTagsValidAsync(dto.Tags))
                return BadRequest("Invalid tags");
            /*
                        var validTags = await db.Tags.Where(x => dto.Tags.Contains(x.Slug)).ToListAsync();
                        var missingTags = dto.Tags.Except(validTags.Select(x => x.Slug)).ToList();
                        if (missingTags.Count != 0)
                        {
                            return BadRequest($"Invalid tags: {string.Join(", ", missingTags)}");
                        }
            */
            question.Title = dto.Title;
            question.Content = dto.Content;
            question.TagSlugs = dto.Tags;
            question.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await bus.PublishAsync(new QuestionUpdated(question.Id, question.Title, question.Content, question.TagSlugs.AsArray()));

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != question.AskerId) return Forbid();

            db.Questions.Remove(question);
            await db.SaveChangesAsync();
            await bus.PublishAsync(new QuestionDeleted(question.Id));
            return NoContent();
        }

        [Authorize]
        [HttpPost("{id}/answers")]
        public async Task<ActionResult<Answer>> CreateAnswer(string id, CreateAnswerDto dto)
        {
            // Find the related question
            var question = await db.Questions.FindAsync(id);
            if (question is null) return BadRequest("Cannot find the related question");
            // Get the given user information
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");
            if (userId is null || name is null) return BadRequest("Cannot get user details");

            // Build the Answer object
            var answer = new Answer
            {
                Content = dto.Content,
                UserId = userId,
                UserDisplayName = name,
                CreatedAt = DateTime.UtcNow,
                QuestionId = question.Id
            };
            // Add answer to Answers table 
            db.Answers.Add(answer);
            // Update AnswerCount in Question
            question.AnswerCount += 1;
            db.Questions.Update(question);
            await db.SaveChangesAsync();

            await bus.PublishAsync(
                new AnswerCountUpdated(question.Id, question.AnswerCount)
            );

            return Created($"/questions/{question.Id}/answers", answer);
        }

        [Authorize]
        [HttpPut("{questionId}/answers/{answerId}")]
        public async Task<ActionResult> UpdateAnswer(string questionId, string answerId, CreateAnswerDto dto)
        {
            var question = await db.Questions.FindAsync(questionId);
            if (question is null) return NotFound();

            var answer = await db.Answers.FindAsync(answerId);
            if (answer is null || answer.QuestionId != questionId) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != answer.UserId) return Forbid();

            answer.Content = dto.Content;
            answer.UpdatedAt = DateTime.UtcNow;
            db.Update(answer);
            await db.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}/answers/{answerId}")]
        public async Task<ActionResult> DeleteAnswer(string id, string answerId)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            var answer = await db.Answers.FindAsync(answerId);
            if (answer is null || answer.QuestionId != id) return NotFound();
            if (answer.Accepted) return BadRequest("Cannot delete an accepted answer.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != answer.UserId) return Forbid();

            db.Answers.Remove(answer);
            question.AnswerCount -= 1;
            db.Questions.Update(question);

            await db.SaveChangesAsync();

            await bus.PublishAsync(
                new AnswerCountUpdated(question.Id, question.AnswerCount)
            );
            // May also need to send an update to the HasAcceptedAnswer field in case the accepted answer is deleted
            // Can only one answer be accepted? If so, then we can just set HasAcceptedAnswer to false without needing 
            // to check which answer is deleted. If multiple answers can be accepted, then we need to check if the
            // deleted answer is accepted (and if any other answers have been accepted) before updating HasAcceptedAnswer

            return NoContent();
        }

        [Authorize]
        [HttpPost("{id}/answers/{answerId}/accept")]
        public async Task<ActionResult<Answer>> AcceptAnswer(string id, string answerId)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            if (question.HasAcceptedAnswer) return BadRequest("Question already has an accepted answer.");

            var answer = await db.Answers.FindAsync(answerId);
            if (answer is null || answer.QuestionId != id) return NotFound();

            if (answer.Accepted) return BadRequest("Answer has already been accepted.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != question.AskerId) return Forbid();

            answer.Accepted = true;
            answer.UpdatedAt = DateTime.UtcNow;
            question.HasAcceptedAnswer = true;
            question.UpdatedAt = DateTime.UtcNow;
            db.Update(answer);
            await db.SaveChangesAsync();

            await bus.PublishAsync(
                new AnswerAccepted(question.Id)
            );

            return NoContent();
        }

    }
}
