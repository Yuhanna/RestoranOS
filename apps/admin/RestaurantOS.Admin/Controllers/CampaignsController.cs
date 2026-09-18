using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class CampaignsController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageAsync(new CreateCampaignViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new CampaignsPageViewModel { RoleCode = api.CurrentToken?.RoleCode });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateCampaignViewModel model,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteCampaigns(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol kampanya yazamaz.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }

        try
        {
            await api.PostAsync<CampaignResponse>(
                "/api/v1/platform/subscription-offers",
                new
                {
                    audience = "non_pro",
                    targetPlanCode = "Pro",
                    discountPercent = model.DiscountPercent,
                    durationMonths = model.DurationMonths,
                    title = model.Title.Trim(),
                    body = model.Body.Trim(),
                    startsAtUtc = DateTimeOffset.UtcNow,
                    isActive = true,
                },
                cancellationToken);
            TempData["Message"] = "Kampanya oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteCampaigns(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol kampanya yazamaz.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PatchAsync<CampaignResponse>(
                $"/api/v1/platform/subscription-offers/{id}",
                new { isActive = !isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Kampanya durduruldu." : "Kampanya açıldı.";
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CampaignsPageViewModel> BuildPageAsync(CreateCampaignViewModel create, CancellationToken cancellationToken)
    {
        var offers = await api.GetAsync<List<CampaignResponse>>("/api/v1/platform/subscription-offers", cancellationToken)
            ?? [];
        return new CampaignsPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            Create = create,
            Offers = offers.Select(offer => new CampaignViewModel
            {
                Id = offer.Id,
                Title = offer.Title,
                TargetPlanCode = offer.TargetPlanCode,
                DiscountPercent = offer.DiscountPercent,
                DurationMonths = offer.DurationMonths,
                IsActive = offer.IsActive,
            }).ToArray(),
        };
    }
}
