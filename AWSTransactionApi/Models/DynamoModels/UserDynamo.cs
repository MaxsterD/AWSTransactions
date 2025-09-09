using Amazon.DynamoDBv2.DataModel;

namespace AWSTransactionApi.Models.DynamoModels
{

    [DynamoDBTable("users")]
    public class UserDynamo
    {
        [DynamoDBHashKey] // Primary Key
        public string uuid { get; set; }

        [DynamoDBRangeKey]
        public string document { get; set; }
    }
}
