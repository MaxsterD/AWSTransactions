namespace AWSTransactionApi.Models
{
    public enum TransactionType { PURCHASE, SAVING, PAYMENT_BALANCE }

    public class TransactionModel
    {
        public Guid Uuid { get; set; } = Guid.NewGuid();
        public Guid CardId { get; set; }
        public decimal Amount { get; set; }
        public string Merchant { get; set; }
        public TransactionType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
