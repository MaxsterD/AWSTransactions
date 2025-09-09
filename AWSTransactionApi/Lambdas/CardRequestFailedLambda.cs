using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using AWSTransactionApi.Models;

namespace AWSTransactionApi.Lambdas
{
    public class CardRequestFailedLambda
    {
        private readonly IDynamoDBContext _dbContext;

        public CardRequestFailedLambda()
        {
            var client = new AmazonDynamoDBClient();
            _dbContext = new DynamoDBContext(client);
        }

        public async Task Handler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var record in evnt.Records)
            {
                try
                {
                    var errorEntry = new CardErrorDynamo
                    {
                        rawMessage = record.Body,
                        errorMessage = "Failed to process message from SQS",
                        createdAt = DateTime.UtcNow
                    };

                    await _dbContext.SaveAsync(errorEntry);

                    context.Logger.LogLine($"[DLQ] Error persisted in card-table-error: {errorEntry.uuid}");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"[DLQ] Failed to save error message: {ex.Message}");
                    throw; 
                }
            }
        }
    }
}
