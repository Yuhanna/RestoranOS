using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class QuotesController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string status = "Open", CancellationToken cancellationToken = default)
    {
        try
        {
            return View(await BuildListAsync(status, cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new QuotesPageViewModel
            {
                RoleCode = api.CurrentToken?.RoleCode,
                Status = status,
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
    public async Task<IActionResult> Accept(
        Guid id,
        [Bind(Prefix = "Decision")] QuoteDecisionFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanAcceptQuotes(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Enterprise sözleşmesini yalnızca Owner veya Billing onaylar.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (!AdminLocalTime.TryParseOptional(form.ExpiresAtLocal, out var expiresAtUtc, out var expiresError))
        {
            ModelState.AddModelError("Decision.ExpiresAtLocal", expiresError);
        }

        if (!ModelState.IsValid)
        {
            return View("Detail", await BuildDetailAsync(id, form, cancellationToken));
        }

        try
        {
            await api.PostAsync<QuoteDetailResponse>(
                $"/api/v1/platform/quotes/{id}/accept",
                new
                {
                    overrideMaxBranches = form.OverrideMaxBranches,
                    overrideMaxActiveUsers = form.OverrideMaxActiveUsers,
                    overrideMaxOrderHistoryHours = form.OverrideMaxOrderHistoryHours,
                    overrideMaxActiveQrCodes = form.OverrideMaxActiveQrCodes,
                    contractNote = string.IsNullOrWhiteSpace(form.ContractNote) ? null : form.ContractNote.Trim(),
                    expiresAtUtc,
                },
                cancellationToken);
            TempData["Message"] = "Teklif onaylandı; üye Enterprise sözleşmesine alındı.";
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        Guid id,
        [Bind(Prefix = "Decision")] QuoteDecisionFormViewModel form,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanRejectQuotes(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Teklifi Owner, Billing veya Support reddedebilir.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (!ModelState.IsValid)
        {
            return View("Detail", await BuildDetailAsync(id, form, cancellationToken));
        }

        try
        {
            await api.PostAsync<QuoteDetailResponse>(
                $"/api/v1/platform/quotes/{id}/reject",
                new { decisionNote = string.IsNullOrWhiteSpace(form.DecisionNote) ? null : form.DecisionNote.Trim() },
                cancellationToken);
            TempData["Message"] = "Teklif reddedildi.";
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

    private async Task<QuotesPageViewModel> BuildListAsync(string status, CancellationToken cancellationToken)
    {
        var filter = string.Equals(status, "all", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : string.IsNullOrWhiteSpace(status) ? "Open" : status.Trim();
        var path = string.IsNullOrEmpty(filter)
            ? "/api/v1/platform/quotes"
            : "/api/v1/platform/quotes?status=" + Uri.EscapeDataString(filter);
        var items = await api.GetAsync<List<QuoteListItemResponse>>(path, cancellationToken) ?? [];
        return new QuotesPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            Status = string.IsNullOrEmpty(filter) ? "all" : filter,
            Items = items.Select(item => new QuoteListItemViewModel
            {
                Id = item.Id,
                TenantId = item.TenantId,
                TenantName = item.TenantName,
                EstimatedBranchCount = item.EstimatedBranchCount,
                Status = item.Status,
                CreatedAtUtc = item.CreatedAtUtc,
            }).ToArray(),
        };
    }

    private async Task<QuoteDetailPageViewModel> BuildDetailAsync(
        Guid quoteId,
        QuoteDecisionFormViewModel? form,
        CancellationToken cancellationToken)
    {
        var detail = await api.GetAsync<QuoteDetailResponse>(
            $"/api/v1/platform/quotes/{quoteId}",
            cancellationToken)
            ?? throw new WebApiException(StatusCodes.Status404NotFound, new ErrorResponse
            {
                Code = "QUOTE_NOT_FOUND",
                Detail = "Teklif bulunamadı.",
            });
        return new QuoteDetailPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            Id = detail.Id,
            TenantId = detail.TenantId,
            TenantName = detail.TenantName,
            ContactName = detail.ContactName,
            Email = detail.Email,
            Phone = detail.Phone,
            EstimatedBranchCount = detail.EstimatedBranchCount,
            Note = detail.Note,
            Status = detail.Status,
            CreatedAtUtc = detail.CreatedAtUtc,
            ReviewedAtUtc = detail.ReviewedAtUtc,
            DecisionNote = detail.DecisionNote,
            Decision = form ?? new QuoteDecisionFormViewModel
            {
                OverrideMaxBranches = detail.EstimatedBranchCount,
                ContractNote = detail.Note,
            },
        };
    }
}
