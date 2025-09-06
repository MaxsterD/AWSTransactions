using AWSTransactionApi.Interfaces.Card;
using Microsoft.AspNetCore.Mvc;

namespace AWSTransactionApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TransactionController : ControllerBase
    {

        private readonly ILogger<TransactionController> _logger;
        private readonly ICardService _cardService;

        public TransactionController(ILogger<TransactionController> logger,ICardService cardService)
        {
            _logger = logger;
            _cardService = cardService;
        }

        [HttpPost("card/activate")]
        public IActionResult activate()
        {
            _logger.LogInformation("Se ejecutó el endpoint /card/activate");
            _cardService.activateCard();
            return Ok("Check CloudWatch Logs");
        }

        [HttpGet("hola")]
        public IActionResult Get()
        {
            return Ok("Hola Mundo desde .NET API 🚀");
        }

        [HttpGet("saludo")]
        public IActionResult Saludo()
        {
            return Ok(new { mensaje = "Este es otro endpoint de saludo 👋" });
        }
    }
}
