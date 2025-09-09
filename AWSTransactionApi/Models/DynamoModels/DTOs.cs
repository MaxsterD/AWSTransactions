namespace AWSTransactionApi.Models
{
    public class ActivateCardRequest { 
        public string userId { get; set; } = string.Empty; 
    }
    public class PurchaseRequest { 
        public string merchant { get; set; } = string.Empty;
        public string cardId { get; set; } = string.Empty;
        public decimal amount { get; set; } 
    }
    public class SaveBalanceRequest { 
        public string merchant { get; set; } = string.Empty; 
        public decimal amount { get; set; } 
    }
    public class PayCreditRequest {
        public string merchant { get; set; } = string.Empty;
        public decimal amount { get; set; } 
    }
    public class ReportRequest { 
        public string start { get; set; } = string.Empty; 
        public string end { get; set; } = string.Empty;
    }
}