namespace AWSTransactionApi.Models
{
    public enum CardType { DEBIT, CREDIT }
    public enum CardStatus { PENDING, ACTIVATED }

    public class CardModel
    {
        public Guid Uuid { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public CardType Type { get; set; }
        public CardStatus Status { get; set; } = CardStatus.PENDING;
        public decimal Balance { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CreateCardMessage
    {
        public string UserId { get; set; }
        public string Request { get; set; } // "DEBIT" o "CREDIT"
    }
}
