using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.SQS;
using AWSTransactionApi.Models;
using AWSTransactionApi.Services.Card;
using AWSTransactionApi.Services.Notification;
using System.Text.Json;


namespace AWSTransactionApi
{
    public class CreateRequestCardLambda
    {
        private readonly CardService _svc;

        public CreateRequestCardLambda()
        {
            try
            {
                var dbClient = new Amazon.DynamoDBv2.AmazonDynamoDBClient();
                var ctx = new DynamoDBContext(dbClient);
                var s3 = new AmazonS3Client();
                var sqs = new AmazonSQSClient();

                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "NotificationQueueUrl", "https://sqs.us-east-2.amazonaws.com/346225465782/notification-email-sqs" },
                    { "ReportsBucket", "bucket-cardse3013575" }
                }).Build();

                var notificationService = new NotificationService(sqs, config);
                _svc = new CardService(ctx, s3, config, notificationService);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en constructor Lambda: " + ex);
                throw;
            }
        }

        // Entry point para SQS
        public async Task Handler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var record in evnt.Records)
            {
                try
                {
                    context.Logger.LogLine($"Processing message {record.Body}");
                    Console.WriteLine($"Processing message {record.Body}");
                    var body = JsonSerializer.Deserialize<CreateCardMessage>(record.Body);
                    if (body == null) continue;

                    var card = await _svc.CreateCardAsync(body.UserId, body.Request);
                    context.Logger.LogLine($"Created card {card.uuid} for user {body.UserId}");
                }
                catch (Exception ex)
                {
                    context.Logger.LogError($"Error processing message: {ex}");
                    throw;
                }
            }
        }
    }
}
