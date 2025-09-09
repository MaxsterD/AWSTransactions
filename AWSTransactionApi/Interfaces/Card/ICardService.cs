namespace AWSTransactionApi.Interfaces.Card
{
    public interface ICardService
    {
        Task<CardDynamo> CreateCardAsync(string userId, string requestType);
        Task<CardDynamo> ActivateCardAsync(string userId);
        Task<TransactionDynamo> PurchaseAsync(string cardId, string merchant, decimal amount);
        Task<TransactionDynamo> SaveTransactionAsync(string cardId, string merchant, decimal amount);
        Task<TransactionDynamo> PayCreditCardAsync(string cardId, string merchant, decimal amount);
        Task<(string s3Key, string bucket)> GenerateReportAsync(string cardId, string startIso, string endIso);
    }
}
