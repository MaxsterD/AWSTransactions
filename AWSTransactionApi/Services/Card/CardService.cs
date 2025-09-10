using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.S3;
using Amazon.S3.Transfer;
using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Interfaces.Notification;
using AWSTransactionApi.Models;
using AWSTransactionApi.Models.DynamoModels;
using System.Globalization;
using System.Text;
using System.Transactions;

namespace AWSTransactionApi.Services.Card
{
    public class CardService : ICardService
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly List<CardModel> _cards = new();
        private readonly List<TransactionModel> _transactions = new();
        private readonly IAmazonS3 _s3;
        private readonly string _reportsBucket;

        public CardService(IDynamoDBContext db, IAmazonS3 s3, IConfiguration cfg, INotificationService notificationService)
        {
            _dbContext = db;
            _s3 = s3;
            _reportsBucket = cfg["ReportsBucket"] ?? throw new ArgumentNullException("ReportsBucket");
            _notificationService = notificationService;
        }

        private async Task LogErrorAsync(string cardId, Exception ex, string rawMessage = null)
        {
            var error = new CardErrorDynamo
            {
                cardId = cardId,
                errorMessage = ex.Message,
                rawMessage = rawMessage,
                createdAt = DateTime.UtcNow
            };
            await _dbContext.SaveAsync(error);
        }

        public async Task<CardDynamo> CreateCardAsync(string userId, string requestType)
        {
            requestType = string.IsNullOrWhiteSpace(requestType) ? "DEBIT" : requestType.ToUpper();


            var userScan = await _dbContext.ScanAsync<UserDynamo>(
                new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, userId) }
            ).GetRemainingAsync();

            if (!userScan.Any())
                throw new Exception($"User {userId} does not exist in users table");

            if (requestType != CardType.DEBIT.ToString() && requestType != CardType.CREDIT.ToString())
                throw new ArgumentException("Request type must be DEBIT or CREDIT");

            var conditions = new List<ScanCondition>
            {
                new ScanCondition("userId", ScanOperator.Equal, userId),
                new ScanCondition("type", ScanOperator.Equal, requestType)
            };

            var existingCards = await _dbContext
                .ScanAsync<CardDynamo>(conditions)
                .GetRemainingAsync();

            if (existingCards.Any())
            {
                var existingCardId = existingCards.First().uuid; 
                await LogErrorAsync(existingCardId, new Exception($"User already has a {requestType} card"),
                                   $"CreateCardAsync: requestType={requestType}");
                throw new Exception($"User already has a {requestType} card");
            }

            var card = new CardDynamo
            {
                uuid = Guid.NewGuid().ToString(),
                userId = userId,
                type = requestType,
                status = requestType == "CREDIT" ? "PENDING" : "ACTIVATED",
                balance = 0,
                createdAt = DateTime.UtcNow
            };


            
            if (requestType == CardType.CREDIT.ToString())
            {
                var random = new Random();
                var score = random.Next(0, 101);

                var amount = 100 + (score / 100.0m) * (10000000 - 100);

                card.balance = amount;

            }

            await _dbContext.SaveAsync(card);

            var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

            var user = users.FirstOrDefault();

            await _notificationService.SendNotificationAsync("CARD.CREATE", new
            {
                date = DateTime.UtcNow,
                type = card.type,
                amount = card.balance,
                userId = user.uuid,
                userEmail = user.email
            });

            return card;
        }

        public async Task<CardDynamo> ActivateCardAsync(string userId)
        {
            // Query cards by userId - DynamoDBContext.QueryAsync expects key value; we didn't set GSI so we scan
            var cards = await _dbContext.ScanAsync<CardDynamo>(new List<ScanCondition> { new ScanCondition("userId", ScanOperator.Equal, userId) }).GetRemainingAsync();
            var card = cards.FirstOrDefault();
            if (card == null) throw new Exception("Card not found for user");

            // Count PURCHASE transactions for that cardId
            var txs = await _dbContext.ScanAsync<TransactionDynamo>(new List<ScanCondition> { new ScanCondition("cardId", ScanOperator.Equal, card.uuid) }).GetRemainingAsync();
            var purchaseCount = txs.Count(t => t.type == "PURCHASE");

            if (purchaseCount >= 10)
            {
                card.status = "ACTIVATED";
                await _dbContext.SaveAsync(card);

                var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

                var user = users.FirstOrDefault();

                await _notificationService.SendNotificationAsync("CARD.ACTIVATE", new
                {
                    date = DateTime.UtcNow,
                    type = card.type,
                    amount = card.balance,
                    userId = user.uuid,
                    userEmail = user.email
                });

                return card;
            }

            throw new Exception($"Not enough transactions: {purchaseCount}/10");
        }

        public async Task<TransactionDynamo> PurchaseAsync(string cardId, string merchant, decimal amount)
        {
            try
            {
                var cards = await _dbContext.ScanAsync<CardDynamo>(
                new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, cardId) }
                ).GetRemainingAsync();

                var card = cards.FirstOrDefault();
                if (card == null) throw new Exception("Card not found");
                if (card.type == "DEBIT")
                {
                    if (card.balance < amount) throw new Exception("Insufficient balance");
                    card.balance -= amount;
                }
                else // CREDIT
                {
                    if (card.balance < amount) throw new Exception("Credit limit exceeded");
                    card.balance += amount;
                    await ActivateCardAsync(card.userId);
                }

                await _dbContext.SaveAsync(card);

                var tx = new TransactionDynamo
                {
                    uuid = Guid.NewGuid().ToString(),
                    cardId = card.uuid,
                    amount = amount,
                    merchant = merchant,
                    type = "PURCHASE",
                    createdAt = DateTime.UtcNow
                };

                await _dbContext.SaveAsync(tx);

                var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

                var user = users.FirstOrDefault();

                await _notificationService.SendNotificationAsync("TRANSACTION.PURCHASE", new
                {
                    date = DateTime.UtcNow,
                    merchant = merchant,
                    cardId = card.uuid,
                    amount = amount,
                    userId = user.uuid,
                    userEmail = user.email
                });

                return tx;
            }
            catch (Exception ex)
            {
                await LogErrorAsync(cardId, ex, $"PurchaseAsync: merchant={merchant}, amount={amount}");
                throw;
            }
            
        }

        public async Task<TransactionDynamo> SaveTransactionAsync(string cardId, string merchant, decimal amount)
        {
            try
            {
                var cards = await _dbContext.ScanAsync<CardDynamo>(
                new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, cardId) }
                ).GetRemainingAsync();

                var card = cards.FirstOrDefault();
                if (card == null) throw new Exception("Card not found");
                if (card.type != "DEBIT") throw new Exception("Only debit cards can save balance");

                card.balance += amount;
                await _dbContext.SaveAsync(card);

                var tx = new TransactionDynamo
                {
                    uuid = Guid.NewGuid().ToString(),
                    cardId = card.uuid,
                    amount = amount,
                    merchant = merchant,
                    type = "SAVING",
                    createdAt = DateTime.UtcNow
                };

                await _dbContext.SaveAsync(tx);

                var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

                var user = users.FirstOrDefault();

                await _notificationService.SendNotificationAsync("TRANSACTION.SAVE", new
                {
                    date = DateTime.UtcNow,
                    merchant = "SAVING",
                    amount = amount,
                    userId = user.uuid,
                    userEmail = user.email
                });

                return tx;
            }
            catch (Exception ex)
            {
                await LogErrorAsync(cardId, ex, $"SaveTransactionAsync: merchant={merchant}, amount={amount}");
                throw;
            }
            
        }

        public async Task<TransactionDynamo> PayCreditCardAsync(string cardId, string merchant, decimal amount)
        {
            try
            {
                var cards = await _dbContext.ScanAsync<CardDynamo>(
                new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, cardId) }
                ).GetRemainingAsync();

                var card = cards.FirstOrDefault();
                if (card == null) throw new Exception("Card not found");
                if (card.type != "CREDIT") throw new Exception("Only credit cards can be paid");

                // Decrease the used balance
                card.balance -= amount;
                if (card.balance < 0) card.balance = 0;
                await _dbContext.SaveAsync(card);

                var tx = new TransactionDynamo
                {
                    uuid = Guid.NewGuid().ToString(),
                    cardId = card.uuid,
                    amount = amount,
                    merchant = merchant,
                    type = "PAYMENT_BALANCE",
                    createdAt = DateTime.UtcNow
                };

                await _dbContext.SaveAsync(tx);

                

                var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

                var user = users.FirstOrDefault();

                await _notificationService.SendNotificationAsync("TRANSACTION.PAID", new
                {
                    date = DateTime.UtcNow,
                    merchant = merchant,
                    amount = amount,
                    userId = user.uuid,
                    userEmail = user.email
                });

                return tx;
            }
            catch (Exception ex)
            {
                await LogErrorAsync(cardId, ex, $"PayCreditCardAsync: merchant={merchant}, amount={amount}");
                throw;
            }
            
        }

        public async Task<(string s3Key, string bucket)> GenerateReportAsync(string cardId, string startIso, string endIso)
        {
            try
            {
                // parse dates
                var start = DateTime.Parse(startIso, null, DateTimeStyles.RoundtripKind);
                var end = DateTime.Parse(endIso, null, DateTimeStyles.RoundtripKind);

                // get transactions for cardId in range
                var txs = await _dbContext.ScanAsync<TransactionDynamo>(new List<ScanCondition> { new ScanCondition("cardId", ScanOperator.Equal, cardId) })
                            .GetRemainingAsync();
                var filtered = txs.Where(t =>
                {
                    var created = t.createdAt;
                    return created >= start && created <= end;
                }).OrderBy(t => t.createdAt).ToList();

                // build CSV
                var sb = new StringBuilder();
                sb.AppendLine("uuid,cardId,amount,merchant,type,createdAt");
                foreach (var t in filtered)
                {
                    sb.AppendLine($"\"{t.uuid}\",\"{t.cardId}\",\"{t.amount}\",\"{t.merchant}\",\"{t.type}\",\"{t.createdAt}\"");
                }

                var key = $"reports/{cardId}/{Guid.NewGuid()}.csv";
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())))
                {
                    var uploadRequest = new Amazon.S3.Transfer.TransferUtilityUploadRequest
                    {
                        InputStream = ms,
                        Key = key,
                        BucketName = _reportsBucket,
                        ContentType = "text/csv"
                    };
                    var tu = new TransferUtility(_s3);
                    await tu.UploadAsync(uploadRequest);
                }

                var fileUrl = $"https://{_reportsBucket}.s3.us-east-2.amazonaws.com/{key}";

                var cards = await _dbContext.ScanAsync<CardDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, cardId) }
                ).GetRemainingAsync();

                var card = cards.FirstOrDefault();

                var users = await _dbContext.ScanAsync<UserDynamo>(
                    new List<ScanCondition> { new ScanCondition("uuid", ScanOperator.Equal, card.userId) }
                ).GetRemainingAsync();

                var user = users.FirstOrDefault();

                await _notificationService.SendNotificationAsync("REPORT.ACTIVITY", new
                {
                    date = DateTime.UtcNow,
                    url = fileUrl,
                    userId = user.uuid,
                    userEmail = user.email
                });

                return (key, _reportsBucket);
            }
            catch (Exception ex)
            {
                await LogErrorAsync(cardId, ex, $"GenerateReportAsync: start={startIso}, end={endIso}");
                throw;
            }
            
        }

      
    }
}
