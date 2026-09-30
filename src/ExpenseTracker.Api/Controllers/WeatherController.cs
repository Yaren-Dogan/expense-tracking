using ExpenseTracker.Application.Features.Weather.Queries.GetWeather;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.Api.Controllers;

[ApiController]
[Route("api/weather")]
public sealed class WeatherController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WeatherDto>> Get([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await mediator.Send(new GetWeatherQuery(date), ct));
}
