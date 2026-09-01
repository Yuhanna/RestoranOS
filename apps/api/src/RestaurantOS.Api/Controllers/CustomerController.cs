using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/customer")]
public sealed partial class CustomerController(ICustomerExperienceService service) : ControllerBase
{
    private CustomerClientContext ClientContext()
    {
        var deviceId = Request.Headers["X-Device-Id"].ToString();
        return new CustomerClientContext(
            string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim(),
            HttpContext.Connection.RemoteIpAddress?.ToString());
    }

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
            var result = await service.ResolveQrAsync(request.QrToken, request.Locale, cancellationToken, ClientContext());
            return Ok(new CustomerSessionResponse(
                result.SessionToken,
                result.RestaurantName,
                result.BranchName,
                result.TableLabel,
                result.Locale,
                result.Categories.Select(x => new MenuCategoryResponse(x.Id.ToString(), x.Name)).ToArray(),
                result.Products.Select(ToProductResponse).ToArray(),
                result.OpenServiceRequestTypes,
                result.ActiveOrders.Select(ToOrderResponse).ToArray(),
                ToSettingsResponse(result.CustomerMenuSettings)));
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
            || request.Lines.Any(x => !Guid.TryParse(x.ProductId, out _)))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Order request is invalid.");
        }

        try
        {
            var result = await service.CreateOrderAsync(
                request.SessionToken,
                idempotencyKey,
                request.Lines.Select(x => new CreateOrderLine(
                    Guid.Parse(x.ProductId),
                    x.Quantity,
                    x.Note,
                    x.ModifierOptionIds ?? [])).ToArray(),
                cancellationToken,
                ClientContext());
            var response = ToOrderResponse(result);
            return Created($"/api/v1/customer/orders/{result.Id}", response);
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "INVALID_SESSION" => StatusCodes.Status401Unauthorized,
                "ORDER_RATE_LIMITED" or "ORDER_BLOCKED" => StatusCodes.Status429TooManyRequests,
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
            return Ok(ToOrderResponse(result));
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

    private static CustomerOrderResponse ToOrderResponse(CustomerOrderResult order)
    {
        var total = new MoneyResponse(order.AmountMinor, order.Currency);
        var subtotalMinor = order.SubtotalAmountMinor > 0 ? order.SubtotalAmountMinor : order.AmountMinor;
        var discountMinor = order.DiscountAmountMinor;
        return new CustomerOrderResponse(
            order.Id.ToString(),
            order.DisplayNumber,
            order.Status,
            order.StatusChangedAtUtc,
            order.EstimatedReadyAtUtc,
            total,
            new MoneyResponse(subtotalMinor, order.Currency),
            discountMinor > 0 ? new MoneyResponse(discountMinor, order.Currency) : null);
    }
}
