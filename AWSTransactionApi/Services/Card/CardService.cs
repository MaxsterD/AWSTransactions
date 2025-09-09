using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.S3;
using Amazon.S3.Transfer;
using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Models;
using System.Globalization;
using System.Text;
using System.Transactions;

namespace AWSTransactionApi.Services.Card
{
    public class CardService : ICardService
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly List<CardModel> _cards = new();
        private readonly List<TransactionModel> _transactions = new();
        private readonly IAmazonS3 _s3;
        private readonly string _reportsBucket;

        public CardService(IDynamoDBContext db, IAmazonS3 s3, IConfiguration cfg)
        {
            _dbContext = db;
            _s3 = s3;
            _reportsBucket = cfg["ReportsBucket"] ?? throw new ArgumentNullException("ReportsBucket");
        }

        public async Task<CardDynamo> CreateCardAsync(string userId, string requestType)
        {

            // Validar request
            if (requestType != CardType.DEBIT.ToString() && requestType != CardType.CREDIT.ToString())
                throw new ArgumentException("Request type must be DEBIT or CREDIT");

            // Verificar si ya existe una tarjeta de ese tipo para el usuario
            var existingCards = await _dbContext.QueryAsync<CardDynamo>(userId).GetRemainingAsync();
            if (existingCards.Any(c => c.type == requestType))
                throw new Exception($"User already has a {requestType} card");

            // Crear tarjeta
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
                return card;
            }

            throw new Exception($"Not enough transactions: {purchaseCount}/10");
        }

        public async Task<TransactionDynamo> PurchaseAsync(string cardId, string merchant, decimal amount)
        {
            var card = await _dbContext.LoadAsync<CardDynamo>(cardId);
            if (card == null) throw new Exception("Card not found");
            if (card.type == "DEBIT")
            {
                if (card.balance < amount) throw new Exception("Insufficient balance");
                card.balance -= amount;
            }
            else // CREDIT
            {
                // For credit we assume 'balance' stores used balance. Need a limit — let's assume 5000 for now
                decimal limit = 5000m;
                if ((card.balance + amount) > limit) throw new Exception("Credit limit exceeded");
                card.balance += amount; // increase debt/used
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
            return tx;
        }

        public async Task<TransactionDynamo> SaveTransactionAsync(string cardId, string merchant, decimal amount)
        {
            var card = await _dbContext.LoadAsync<CardDynamo>(cardId);
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
            return tx;
        }

        public async Task<TransactionDynamo> PayCreditCardAsync(string cardId, string merchant, decimal amount)
        {
            var card = await _dbContext.LoadAsync<CardDynamo>(cardId);
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
            return tx;
        }

        public async Task<(string s3Key, string bucket)> GenerateReportAsync(string cardId, string startIso, string endIso)
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

            return (key, _reportsBucket);
        }

      
    }
}
