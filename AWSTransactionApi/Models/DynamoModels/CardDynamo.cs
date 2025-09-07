using Amazon.DynamoDBv2.DataModel;

[DynamoDBTable("cards")]
public class CardDynamo
{
    [DynamoDBHashKey] // Primary Key
    public string uuid { get; set; }

    [DynamoDBProperty]
    public string userId { get; set; }

    [DynamoDBProperty]
    public string type { get; set; } // DEBIT o CREDIT

    [DynamoDBProperty]
    public string status { get; set; } // PENDING, ACTIVATED

    [DynamoDBProperty]
    public decimal balance { get; set; }

    [DynamoDBProperty]
    public DateTime createdAt { get; set; }
}