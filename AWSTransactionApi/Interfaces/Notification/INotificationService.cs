namespace AWSTransactionApi.Interfaces.Notification
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string type, object data);
    }
}
