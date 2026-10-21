using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LogisticsAPI.Services;

namespace LogisticsAPI.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = "Manager,Driver")]
public class RoutingController : ApiControllerBase
{
    private readonly RouteService _routes;
    public RoutingController(RouteService routes) => _routes = routes;

    /// <summary>Geocodes both places and returns the real driving distance and duration.</summary>
    [HttpGet("distance")]
    public async Task<IActionResult> Distance([FromQuery] string origin, [FromQuery] string destination)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            return BadRequest(new { message = "Origin and destination are required." });

        var result = await _routes.CalculateAsync(origin, destination);
        if (result == null)
            return UnprocessableEntity(new
            {
                message = "We could not resolve that route automatically. Check the place names or enter the distance manually."
            });

        return Ok(result);
    }
}
