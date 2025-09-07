using AWSTransactionApi.Services.Card;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class CardController : ControllerBase
{
    private readonly CardService _service;

    public CardController(CardService service)
    {
        _service = service;
    }

    [HttpPost("activate")]
    public IActionResult Activate([FromBody] dynamic request)
    {
        var userId = Guid.Parse((string)request.userId);
        var card = _service.ActivateCard(userId);
        return Ok(card);
    }

    [HttpPost("/transactions/purchase")]
    public IActionResult Purchase([FromBody] dynamic request)
    {
        var cardId = Guid.Parse((string)request.cardId);
        var merchant = (string)request.merchant;
        var amount = (decimal)request.amount;

        var tx = _service.Purchase(cardId, merchant, amount);
        return Ok(tx);
    }

    [HttpPost("/transactions/save/{cardId}")]
    public IActionResult SaveTransaction(Guid cardId, [FromBody] dynamic request)
    {
        var merchant = (string)request.merchant;
        var amount = (decimal)request.amount;
        var tx = _service.SaveTransaction(cardId, merchant, amount);
        return Ok(tx);
    }

    [HttpPost("/card/paid/{cardId}")]
    public IActionResult PayCreditCard(Guid cardId, [FromBody] dynamic request)
    {
        var merchant = (string)request.merchant;
        var amount = (decimal)request.amount;
        var tx = _service.PayCreditCard(cardId, merchant, amount);
        return Ok(tx);
    }

    [HttpGet("/card/{cardId}")]
    public IActionResult GetReport(Guid cardId, [FromQuery] DateTime start, [FromQuery] DateTime end)
    {
        var txs = _service.GetTransactions(cardId, start, end);
        return Ok(txs);
    }
}
