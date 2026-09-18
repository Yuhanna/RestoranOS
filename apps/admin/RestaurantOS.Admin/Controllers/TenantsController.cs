using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class TenantsController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, string? plan, int skip = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            return View(await BuildListAsync(q, plan, skip, cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new TenantsPageViewModel
            {
                RoleCode = api.CurrentToken?.RoleCode,
                Query = q,
                Plan = plan,
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildDetailAsync(id, form: null, cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id,
        [Bind(Prefix = "Form")] TenantSubscriptionFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteTenants(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Üye aboneliğini yalnızca Owner veya Billing değiştirir.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (!AdminLocalTime.TryParseOptional(form.ExpiresAtLocal, out var expiresAtUtc, out var expiresError))
        {
            ModelState.AddModelError("Form.ExpiresAtLocal", expiresError);
        }

        if (!ModelState.IsValid)
        {
            return View("Detail", await BuildDetailAsync(id, form, cancellationToken));
        }

        try
        {
            await api.PatchAsync<TenantDetailResponse>(
                $"/api/v1/platform/tenants/{id}/subscription",
                new
                {
                    planCode = form.PlanCode,
                    expiresAtUtc,
                    purchasedBranchAddonCount = form.PurchasedBranchAddonCount,
                    overrideMaxBranches = form.OverrideMaxBranches,
                    overrideMaxActiveUsers = form.OverrideMaxActiveUsers,
                    overrideMaxOrderHistoryHours = form.OverrideMaxOrderHistoryHours,
                    overrideMaxActiveQrCodes = form.OverrideMaxActiveQrCodes,
                    contractNote = string.IsNullOrWhiteSpace(form.ContractNote) ? null : form.ContractNote.Trim(),
                },
                cancellationToken);
            TempData["Message"] = "Abonelik ve sözleşme tavanları kaydedildi.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View("Detail", await BuildDetailAsync(id, form, cancellationToken));
        }
    }

    private async Task<TenantsPageViewModel> BuildListAsync(
        string? query,
        string? plan,
        int skip,
        CancellationToken cancellationToken)
    {
        skip = Math.Max(0, skip);
        const int take = 50;
        var parts = new List<string> { $"skip={skip}", $"take={take}" };
        if (!string.IsNullOrWhiteSpace(query))
        {
            parts.Add($"q={Uri.EscapeDataString(query.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(plan))
        {
            parts.Add($"plan={Uri.EscapeDataString(plan.Trim())}");
        }

        var page = await api.GetAsync<TenantListResponse>(
            "/api/v1/platform/tenants?" + string.Join('&', parts),
            cancellationToken)
            ?? new TenantListResponse();
        return new TenantsPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            Query = query,
            Plan = plan,
            Total = page.Total,
            Skip = page.Skip,
            Take = page.Take == 0 ? take : page.Take,
            Items = page.Items.Select(item => new TenantListItemViewModel
            {
                TenantId = item.TenantId,
                TenantName = item.TenantName,
                RestaurantName = item.RestaurantName,
                PlanCode = item.PlanCode,
                IsTrial = item.IsTrial,
                ExpiresAtUtc = item.ExpiresAtUtc,
                ActiveBranchCount = item.ActiveBranchCount,
                MaxBranches = item.MaxBranches,
                OpenQuoteCount = item.OpenQuoteCount,
            }).ToArray(),
        };
    }

    private async Task<TenantDetailPageViewModel> BuildDetailAsync(
        Guid tenantId,
        TenantSubscriptionFormViewModel? form,
        CancellationToken cancellationToken)
    {
        var detail = await api.GetAsync<TenantDetailResponse>(
            $"/api/v1/platform/tenants/{tenantId}",
            cancellationToken)
            ?? throw new WebApiException(StatusCodes.Status404NotFound, new ErrorResponse
            {
                Code = "TENANT_NOT_FOUND",
                Detail = "Üye bulunamadı.",
            });
        return new TenantDetailPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            TenantId = detail.TenantId,
            TenantName = detail.TenantName,
            RestaurantName = detail.RestaurantName,
            BillingEmail = detail.BillingEmail,
            IsTrial = detail.IsTrial,
            StartedAtUtc = detail.StartedAtUtc,
            ActiveBranchCount = detail.ActiveBranchCount,
            FrozenBranchCount = detail.FrozenBranchCount,
            ActiveUserCount = detail.ActiveUserCount,
            MaxBranches = detail.MaxBranches,
            MaxActiveUsers = detail.MaxActiveUsers,
            MaxOrderHistoryHours = detail.MaxOrderHistoryHours,
            MaxActiveQrCodes = detail.MaxActiveQrCodes,
            Form = form ?? new TenantSubscriptionFormViewModel
            {
                PlanCode = detail.PlanCode,
                ExpiresAtLocal = AdminLocalTime.ToInput(detail.ExpiresAtUtc),
                PurchasedBranchAddonCount = detail.PurchasedBranchAddonCount,
                OverrideMaxBranches = detail.OverrideMaxBranches,
                OverrideMaxActiveUsers = detail.OverrideMaxActiveUsers,
                OverrideMaxOrderHistoryHours = detail.OverrideMaxOrderHistoryHours,
                OverrideMaxActiveQrCodes = detail.OverrideMaxActiveQrCodes,
                ContractNote = detail.ContractNote,
            },
        };
    }
}
