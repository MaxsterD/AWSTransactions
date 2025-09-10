using AWSTransactionApi.Interfaces.Card;
using AWSTransactionApi.Models;
using AWSTransactionApi.Services.Card;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("Transaction")]
public class CardController : ControllerBase
{
    private readonly ICardService _svc;
    public CardController(ICardService svc) { _svc = svc; }

    [HttpPost("card/create")]
    public async Task<IActionResult> Create([FromBody] CreateCardMessage req)
    {
        try
        {
            var requestType = string.IsNullOrWhiteSpace(req.Request) ? "DEBIT" : req.Request.ToUpper();

            var card = await _svc.CreateCardAsync(req.UserId, requestType);
            return Ok(card);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("card/activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateCardRequest req)
    {
        try
        {
            var card = await _svc.ActivateCardAsync(req.userId);
            return Ok(card);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transactions/purchase")]
    public async Task<IActionResult> Purchase([FromBody] PurchaseRequest req)
    {
        try
        {
            var tx = await _svc.PurchaseAsync(req.cardId, req.merchant, req.amount);
            return Ok(tx);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transactions/save/{cardId}")]
    public async Task<IActionResult> Save([FromRoute] string cardId, [FromBody] SaveBalanceRequest req)
    {
        try
        {
            var tx = await _svc.SaveTransactionAsync(cardId, req.merchant, req.amount);
            return Ok(tx);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }


    [HttpPost("card/paid/{cardId}")]
    public async Task<IActionResult> Pay([FromRoute] string cardId, [FromBody] PayCreditRequest req)
    {
        try
        {
            var tx = await _svc.PayCreditCardAsync(cardId, req.merchant, req.amount);
            return Ok(tx);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("card/{cardId}")]
    public async Task<IActionResult> GetReport([FromRoute] string cardId, [FromQuery] string start, [FromQuery] string end)
    {
        try
        {
            var (key, bucket) = await _svc.GenerateReportAsync(cardId, start, end);
            return Ok(new { s3Key = key, bucket });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
