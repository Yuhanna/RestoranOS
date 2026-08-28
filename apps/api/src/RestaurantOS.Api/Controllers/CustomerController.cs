using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/customer")]
public sealed class CustomerController(ICustomerExperienceService service) : ControllerBase
{
    [HttpPost("sessions/resolve")]
    [ProducesResponseType(typeof(CustomerSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveQrAsync(
        [FromBody] ResolveQrRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.QrToken))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "QR token is required.");
        }

        try
        {
            var result = await service.ResolveQrAsync(request.QrToken, request.Locale, cancellationToken);
            return Ok(new CustomerSessionResponse(
                result.SessionToken,
                result.RestaurantName,
                result.BranchName,
                result.TableLabel,
                result.Locale,
                result.Categories.Select(x => new MenuCategoryResponse(x.Id.ToString(), x.Name)).ToArray(),
                result.Products.Select(x => new MenuProductResponse(
                    x.Id.ToString(),
                    x.CategoryId.ToString(),
                    x.Name,
                    x.Description,
                    new MoneyResponse(x.AmountMinor, x.Currency),
                    AbsolutizeMediaUrl(x.ImageUrl),
                    string.IsNullOrWhiteSpace(x.ImageAlt) ? x.Name : x.ImageAlt,
                    x.Available,
                    [],
                    [],
                    [])).ToArray(),
                result.OpenServiceRequestTypes,
                result.ActiveOrders.Select(order => new CustomerOrderResponse(
                    order.Id.ToString(),
                    order.DisplayNumber,
                    order.Status,
                    order.StatusChangedAtUtc,
                    order.EstimatedReadyAtUtc,
                    new MoneyResponse(order.AmountMinor, order.Currency))).ToArray()));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code == "INVALID_QR"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("orders")]
    [ProducesResponseType(typeof(CustomerOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CustomerOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOrderAsync(
        [FromBody] CreateCustomerOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        if (request is null
            || string.IsNullOrWhiteSpace(request.SessionToken)
            || request.Lines is null
            || request.Lines.Count == 0
            || request.Lines.Any(x =>
                !Guid.TryParse(x.ProductId, out _)
                || x.ModifierOptionIds is { Count: > 0 }))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Order request is invalid.");
        }

        try
        {
            var result = await service.CreateOrderAsync(
                request.SessionToken,
                idempotencyKey,
                request.Lines.Select(x => new CreateOrderLine(Guid.Parse(x.ProductId), x.Quantity, x.Note)).ToArray(),
                cancellationToken);
            var response = new CustomerOrderResponse(
                result.Id.ToString(),
                result.DisplayNumber,
                result.Status,
                result.StatusChangedAtUtc,
                result.EstimatedReadyAtUtc,
                new MoneyResponse(result.AmountMinor, result.Currency));
            return Created($"/api/v1/customer/orders/{result.Id}", response);
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "INVALID_SESSION" => StatusCodes.Status401Unauthorized,
                "IDEMPOTENCY_CONFLICT" or "ORDER_REJECTED" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpGet("orders/{orderId:guid}")]
    [ProducesResponseType(typeof(CustomerOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetOrderAsync(
                Request.Headers["X-Customer-Session"].ToString(),
                orderId,
                cancellationToken);
            return Ok(new CustomerOrderResponse(
                result.Id.ToString(),
                result.DisplayNumber,
                result.Status,
                result.StatusChangedAtUtc,
                result.EstimatedReadyAtUtc,
                new MoneyResponse(result.AmountMinor, result.Currency)));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code == "INVALID_SESSION"
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status404NotFound;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("service-requests")]
    [ProducesResponseType(typeof(CustomerServiceRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateServiceRequestAsync(
        [FromBody] CreateServiceRequestRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.SessionToken)
            || string.IsNullOrWhiteSpace(request.Type))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Session token and request type are required.");
        }

        try
        {
            var result = await service.CreateServiceRequestAsync(
                request.SessionToken,
                request.Type,
                request.Note,
                cancellationToken);
            return Created(
                $"/api/v1/customer/service-requests/{result.Id}",
                new CustomerServiceRequestResponse(
                    result.Id.ToString(),
                    result.TableId.ToString(),
                    result.TableLabel,
                    result.Type,
                    result.Status,
                    result.Note,
                    result.CreatedAtUtc));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "INVALID_SESSION" => StatusCodes.Status401Unauthorized,
                "SERVICE_REQUEST_OPEN" or "SERVICE_REQUEST_COOLDOWN" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    private string AbsolutizeMediaUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
        {
            return imageUrl;
        }

        if (!imageUrl.StartsWith('/'))
        {
            return imageUrl;
        }

        return $"{Request.Scheme}://{Request.Host}{imageUrl}";
    }
}
