using Typesense;

namespace SearchService.Data
{
    public static class SearchInitializer
    {
        public static async Task EnsureIndexExists(ITypesenseClient client, int timeoutSeconds = 60)
        {
			const string schemaName = "questions";
			var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);


			while (DateTime.UtcNow < deadline)
			{
				try
				{
					await client.RetrieveCollection(schemaName);
					Console.WriteLine($"Collection {schemaName} already exists.");
					return;
				}
				catch (TypesenseApiNotFoundException)
				{
					Console.WriteLine($"Collection {schemaName} does not exist.");
					break;
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Unexpected error checking collection: {ex.GetType().Name} - {ex.Message}, Retrying...");
					await Task.Delay(1000);
				}
			}

			var schema = new Schema(schemaName, new List<Field>
			{
				new("id", FieldType.String),
				new("title", FieldType.String),
				new("content", FieldType.String),
				new("tags", FieldType.StringArray),
				new("createdAt", FieldType.Int64),
				new("answerCount", FieldType.Int32),
				new("hasAcceptedAnswer", FieldType.Bool)
			})
			{
				DefaultSortingField = "createdAt"
			};

			await client.CreateCollection(schema);
			Console.WriteLine($"Collection {schemaName} has been created.");
        }
    }
}
