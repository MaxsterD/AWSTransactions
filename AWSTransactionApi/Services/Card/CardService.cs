using AWSTransactionApi.Interfaces.Card;

namespace AWSTransactionApi.Services.Card
{
    public class CardService : ICardService
    {
        public CardService() { }

        public void activateCard()
        {
            Console.WriteLine("Card activated");
        }
    }
}
