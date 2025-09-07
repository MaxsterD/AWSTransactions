using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("transactions")]
public class TransactionDynamo
{
    [DynamoDBHashKey]
    public string uuid { get; set; }

    [DynamoDBProperty]
    public string cardId { get; set; }

    [DynamoDBProperty]
    public decimal amount { get; set; }

    [DynamoDBProperty]
    public string merchant { get; set; }

    [DynamoDBProperty]
    public string type { get; set; } // PURCHASE, SAVING, PAYMENT_BALANCE

    [DynamoDBProperty]
    public DateTime createdAt { get; set; }
}