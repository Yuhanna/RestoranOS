using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
public sealed class SystemController(TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SystemInfoResponse), StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> Get() =>
        Ok(
            new SystemInfoResponse(
                "restaurant-os-api",
                "healthy",
                typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                timeProvider.GetUtcNow()));
}
