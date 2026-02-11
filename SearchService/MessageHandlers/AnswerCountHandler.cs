using Contracts;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MessageHandlers
{
    public class AnswerCountHandler(ITypesenseClient client)
    {
        public async Task HandleAsync(AnswerCountUpdated message)
        {
            // Don't use the SearchQuestion object here.  If you do, you will override 
            // all of the properties in the search index record.  Instead use an anonymous structure
            // containing only those properties you wish to update.
            await client.UpdateDocument("questions", message.QuestionId, new
            {
                message.AnswerCount
            });

        }

    }
}
