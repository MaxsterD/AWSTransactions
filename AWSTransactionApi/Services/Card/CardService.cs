using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Models;
using System.Transactions;

namespace AWSTransactionApi.Services.Card
{
    public class CardService : ICardService
    {
        private readonly IDynamoDBContext _dbContext;
        private readonly List<CardModel> _cards = new();
        private readonly List<TransactionModel> _transactions = new();

        public async Task<CardDynamo> ActivateCardAsync(Guid userId, bool isCreditCard)
        {
            var userIdStr = userId.ToString();

            // Buscar tarjeta existente
            var cards = await _dbContext.QueryAsync<CardDynamo>(userIdStr).GetRemainingAsync();
            var card = cards.FirstOrDefault();

            if (card == null)
            {
                card = new CardDynamo
                {
                    uuid = Guid.NewGuid().ToString(),
                    userId = userIdStr,
                    type = isCreditCard ? CardType.CREDIT.ToString() : CardType.DEBIT.ToString(),
                    status = isCreditCard ? CardStatus.PENDING.ToString() : CardStatus.ACTIVATED.ToString(),
                    balance = 0,
                    createdAt = DateTime.UtcNow
                };

                await _dbContext.SaveAsync(card);

                if (isCreditCard)
                {
                    // Generar score aleatorio 0-100
                    var random = new Random();
                    var score = random.Next(0, 101);

                    // Calcular amount
                    var amount = 100 + (score / 100.0m) * (10000000 - 100);

                    // Guardar en tabla de solicitudes de credit card (puede ser otra tabla o la misma)
                    var pendingCredit = new PendingCreditDynamo
                    {
                        uuid = Guid.NewGuid().ToString(),
                        cardId = card.uuid,
                        score = score,
                        amount = amount,
                        status = CardStatus.PENDING.ToString(),
                        createdAt = DateTime.UtcNow
                    };

                    await _dbContext.SaveAsync(pendingCredit);
                }
            }

            // Para debit card existente: revisar si tiene >=10 transacciones PURCHASE para activar
            if (!isCreditCard)
            {
                var transactions = await _dbContext.ScanAsync<TransactionDynamo>(
                    new List<ScanCondition> { new ScanCondition("cardId", ScanOperator.Equal, card.uuid) }
                ).GetRemainingAsync();

                var txCount = transactions.Count(t => t.type == TransactionType.PURCHASE.ToString());

                if (txCount >= 10 && card.status != CardStatus.ACTIVATED.ToString())
                {
                    card.status = CardStatus.ACTIVATED.ToString();
                    await _dbContext.SaveAsync(card);
                }
            }

            return card;
        }
        public CardService(IDynamoDBContext dbContext)
        {
            _dbContext = dbContext;
        }
        public CardModel ActivateCard(Guid userId)
        {
            var card = _cards.FirstOrDefault(c => c.UserId == userId);
            if (card == null)
            {
                card = new CardModel { UserId = userId, Type = CardType.DEBIT };
                _cards.Add(card);
            }

            // Si tiene >= 10 transacciones lo activamos
            var txCount = _transactions.Count(t => t.CardId == card.Uuid && t.Type == TransactionType.PURCHASE);
            if (txCount >= 10)
                card.Status = CardStatus.ACTIVATED;

            return card;
        }

        public CardModel GetCard(Guid cardId) => _cards.FirstOrDefault(c => c.Uuid == cardId);

        public TransactionModel Purchase(Guid cardId, string merchant, decimal amount)
        {
            var card = _cards.FirstOrDefault(c => c.Uuid == cardId);
            if (card == null) throw new Exception("Card not found");

            if (card.Type == CardType.DEBIT)
            {
                if (card.Balance < amount) throw new Exception("Insufficient balance");
                card.Balance -= amount;
            }
            else // CREDIT
            {
                if (card.Balance + amount > 5000) // supongamos límite crédito
                    throw new Exception("Credit limit exceeded");
                card.Balance += amount;
            }

            var tx = new Transaction
            {
                CardId = card.Uuid,
                Amount = amount,
                Merchant = merchant,
                Type = TransactionType.PURCHASE
            };
            _transactions.Add(tx);
            return tx;
        }

        public TransactionModel SaveTransaction(Guid cardId, string merchant, decimal amount)
        {
            var card = _cards.FirstOrDefault(c => c.Uuid == cardId);
            if (card == null) throw new Exception("Card not found");
            if (card.Type != CardType.DEBIT) throw new Exception("Only debit cards can save balance");

            card.Balance += amount;

            var tx = new Transaction
            {
                CardId = card.Uuid,
                Amount = amount,
                Merchant = merchant,
                Type = TransactionType.SAVING
            };
            _transactions.Add(tx);
            return tx;
        }

        public TransactionModel PayCreditCard(Guid cardId, string merchant, decimal amount)
        {
            var card = _cards.FirstOrDefault(c => c.Uuid == cardId);
            if (card == null) throw new Exception("Card not found");
            if (card.Type != CardType.CREDIT) throw new Exception("Only credit cards can pay");

            card.Balance -= amount;

            var tx = new Transaction
            {
                CardId = card.Uuid,
                Amount = amount,
                Merchant = merchant,
                Type = TransactionType.PAYMENT_BALANCE
            };
            _transactions.Add(tx);
            return tx;
        }

        public List<TransactionModel> GetTransactions(Guid cardId, DateTime start, DateTime end)
        {
            return _transactions
                .Where(t => t.CardId == cardId && t.CreatedAt >= start && t.CreatedAt <= end)
                .ToList();
        }
    }
}
