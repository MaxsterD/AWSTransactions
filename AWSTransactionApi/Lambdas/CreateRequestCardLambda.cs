using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using AWSTransactionApi.Models;
using AWSTransactionApi.Services;
using AWSTransactionApi.Services.Card;
using System.Text.Json;


namespace AWSTransactionApi.Lambdas
{
    public class CreateRequestCardLambda
    {
        private readonly CardService _svc;

        public CreateRequestCardLambda()
        {
            var client = new Amazon.DynamoDBv2.AmazonDynamoDBClient();
            var ctx = new Amazon.DynamoDBv2.DataModel.DynamoDBContext(client);
            var s3 = new Amazon.S3.AmazonS3Client();
            _svc = new CardService(ctx, s3, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { { "ReportsBucket", Environment.GetEnvironmentVariable("ReportsBucket") ?? "" } }).Build());
        }

        public async Task Handler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var record in evnt.Records)
            {
                try
                {
                    var body = JsonSerializer.Deserialize<CreateCardMessage>(record.Body);
                    if (body == null) continue;

                    // create card (DEBIT -> activated, CREDIT -> pending + score/amount stored)
                    var card = await _svc.CreateCardAsync(body.UserId, body.Request);
                    context.Logger.LogLine($"Created card {card.uuid} for user {body.UserId}");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error processing message: {ex.Message}");
                    throw;
                }
            }
        }
    }
}
