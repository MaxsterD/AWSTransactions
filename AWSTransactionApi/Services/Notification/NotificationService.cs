using Amazon.S3;
using Amazon.SQS;
using Amazon.SQS.Model;
using AWSTransactionApi.Interfaces.Notification;
using System.Text.Json;

namespace AWSTransactionApi.Services.Notification
{
    public class NotificationService : INotificationService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly string _queueUrl;

        public NotificationService(IAmazonSQS sqsClient, IConfiguration configuration)
        {
            _sqsClient = sqsClient;
            _queueUrl = configuration["NotificationQueueUrl"];
        }

        public async Task SendNotificationAsync(string type, object data)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Notification type is required", nameof(type));

            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var body = new
            {
                type = type,
                data = data
            };

            var messageBody = JsonSerializer.Serialize(body);

            var request = new SendMessageRequest
            {
                QueueUrl = _queueUrl,
                MessageBody = messageBody
            };

            try
            {
                await _sqsClient.SendMessageAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending SQS notification: {ex.Message}");
                throw;
            }
        }
    }
}
