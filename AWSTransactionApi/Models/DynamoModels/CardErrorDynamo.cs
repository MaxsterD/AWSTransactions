using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("card-table-error")]
public class CardErrorDynamo
{
    [DynamoDBHashKey]
    public string uuid { get; set; } = Guid.NewGuid().ToString();

    [DynamoDBProperty]
    public string cardId { get; set; }

    [DynamoDBProperty]
    public string rawMessage { get; set; }

    [DynamoDBProperty]
    public string errorMessage { get; set; }

    [DynamoDBProperty]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;
}