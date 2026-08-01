using Microsoft.AspNetCore.Mvc;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;
using OuterloopLabApi.Services;

namespace OuterloopLabApi.Controllers;

[ApiController]
[Route("api/currency")]
public sealed class CurrencyController : ControllerBase
{
    private readonly CurrencyConversionService _service;

    public CurrencyController(CurrencyConversionService service)
    {
        _service = service;
    }

    [HttpGet("convert")]
    [ProducesResponseType(typeof(CurrencyConversionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CurrencyConversionResponseDto>> Convert(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] decimal amount,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ConvertAsync(from, to, amount, cancellationToken);
            return Ok(result);
        }
        catch (CurrencyProviderUnavailableException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Currency provider unavailable",
                detail: "The external currency rate provider failed to return a rate.");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid request",
                detail: ex.Message);
        }
    }

    [HttpGet("audit/{id}")]
    [ProducesResponseType(typeof(AuditRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditRecordResponseDto>> Audit([FromRoute] string id, CancellationToken cancellationToken)
    {
        var record = await _service.GetAuditAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        return Ok(record);
    }
}
