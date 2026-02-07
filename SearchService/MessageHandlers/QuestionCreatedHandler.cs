using Contracts;
using SearchService.Models;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MessageHandlers
{
    public class QuestionCreatedHandler(ITypesenseClient client)
    {
        public async Task HandleAsync(QuestionCreated message)
        {
            var created = new DateTimeOffset(message.Created).ToUnixTimeSeconds();

            Console.WriteLine(message.Content);

            var doc = new SearchQuestion
            {
                Id = message.QuestionId,
                Title = message.Title,
                Content = StripHtml(message.Content),
                Tags = message.Tags?.ToArray() ?? Array.Empty<string>(),
                CreatedAt = created
            };
            await client.CreateDocument("questions", doc);

            Console.WriteLine($"Created question with id {message.QuestionId}");
        }

        private static string StripHtml(string content)
        {
            var result = Regex.Replace(content, "<[^<>]*>", string.Empty);
            Console.WriteLine(result);
            return result;
        }
    }
}
