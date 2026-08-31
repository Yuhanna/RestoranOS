using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class OrdersController(IWebApiExecuter api) : Controller
{
    private static readonly Regex ClockPattern = new(
        @"^\s*(?<h>\d{1,2})\s*[:\.]\s*(?<m>\d{1,2})\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            var orders = await api.InvokeGetAsync<List<OrderListItemViewModel>>(
                "/api/v1/management/orders/active",
                cancellationToken) ?? [];
            return View(orders);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new List<OrderListItemViewModel>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            var detail = await api.InvokeGetAsync<OrderDetailViewModel>(
                $"/api/v1/management/orders/{id}",
                cancellationToken);
            if (detail is null)
            {
                TempData["Message"] = "Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(detail);
        }
        catch (WebApiException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            TempData["Message"] = "Sipariş bulunamadı.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ChangeStatus(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            var model = await LoadChangeStatusModelAsync(id, cancellationToken);
            if (model is null)
            {
                TempData["Message"] = "Bu sipariş artık aktif değil (tamamlanmış veya iptal edilmiş olabilir).";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(ChangeOrderStatusViewModel model, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        if (!TryResolveEstimatedReady(model.EstimatedReadyInput, out var estimatedReadyAtUtc, out var etaError))
        {
            ModelState.AddModelError(nameof(model.EstimatedReadyInput), etaError!);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.InvokePutAsync<OrderListItemViewModel>(
                $"/api/v1/management/orders/{model.OrderId}/status",
                new
                {
                    status = model.Status,
                    expectedStatusChangedAtUtc = model.ExpectedStatusChangedAtUtc,
                    estimatedReadyAtUtc,
                },
                cancellationToken);
            TempData["Message"] = $"{model.DisplayNumber} siparişi «{StatusTr(model.Status)}» durumuna alındı.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception) when (exception.Error?.Code == "ORDER_CONCURRENCY_CONFLICT")
        {
            ModelState.AddModelError(
                string.Empty,
                "Sipariş başka bir ekranda güncellendi. Güncel durum yüklendi; tekrar kaydedebilirsiniz.");
            var refreshed = await TryLoadChangeStatusModelAsync(model.OrderId, cancellationToken);
            if (refreshed is null)
            {
                TempData["Message"] = $"{model.DisplayNumber} siparişi zaten kapanmış (tamamlandı veya iptal).";
                return RedirectToAction(nameof(Index));
            }

            refreshed.EstimatedReadyInput = model.EstimatedReadyInput;
            return View(refreshed);
        }
        catch (WebApiException exception) when (exception.Error?.Code == "INVALID_STATUS_TRANSITION")
        {
            var refreshed = await TryLoadChangeStatusModelAsync(model.OrderId, cancellationToken);
            if (refreshed is null)
            {
                TempData["Message"] = $"{model.DisplayNumber} siparişi zaten kapanmış (tamamlandı veya iptal).";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(
                string.Empty,
                $"«{StatusTr(model.Status)}» durumuna geçilemez. Mevcut durum: «{StatusTr(refreshed.CurrentStatus)}». " +
                "Yalnızca ileri adımlar veya iptal seçilebilir.");
            refreshed.EstimatedReadyInput = model.EstimatedReadyInput;
            return View(refreshed);
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    private async Task<ChangeOrderStatusViewModel?> LoadChangeStatusModelAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await FindActiveOrderAsync(orderId, cancellationToken);
        return order is null ? null : ToChangeStatusModel(order);
    }

    private async Task<ChangeOrderStatusViewModel?> TryLoadChangeStatusModelAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LoadChangeStatusModelAsync(orderId, cancellationToken);
        }
        catch (WebApiException)
        {
            return null;
        }
    }

    private async Task<OrderListItemViewModel?> FindActiveOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var orders = await api.InvokeGetAsync<List<OrderListItemViewModel>>(
            "/api/v1/management/orders/active",
            cancellationToken) ?? [];
        return orders.FirstOrDefault(order => order.Id == orderId);
    }

    private static ChangeOrderStatusViewModel ToChangeStatusModel(OrderListItemViewModel order) =>
        new()
        {
            OrderId = order.Id,
            DisplayNumber = order.DisplayNumber,
            CurrentStatus = order.Status,
            ExpectedStatusChangedAtUtc = order.StatusChangedAtUtc,
            CurrentEstimatedReadyAtUtc = order.EstimatedReadyAtUtc,
            Status = NextStatus(order.Status),
            EstimatedReadyInput = null,
        };

    private static string StatusTr(string status) => status.ToLowerInvariant() switch
    {
        "submitted" => "Alındı",
        "accepted" => "Onaylandı",
        "preparing" => "Hazırlanıyor",
        "ready" => "Hazır",
        "completed" => "Tamamlandı",
        "cancelled" => "İptal",
        _ => status,
    };

    private bool EnsureAuthenticated() => api.IsAuthenticated;

    private RedirectToActionResult ChallengeLogin() =>
        RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });

    private static string NextStatus(string current) => current.ToLowerInvariant() switch
    {
        "submitted" => "accepted",
        "accepted" => "preparing",
        "preparing" => "ready",
        "ready" => "completed",
        _ => "accepted",
    };

    /// <summary>
    /// Accepts minutes-from-now ("15") or local clock ("9:23" / "09:23").
    /// Clock times already past roll to the next calendar day.
    /// </summary>
    internal static bool TryResolveEstimatedReady(
        string? input,
        out DateTimeOffset? estimatedReadyAtUtc,
        out string? error)
    {
        estimatedReadyAtUtc = null;
        error = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return true;
        }

        var text = input.Trim();
        var nowLocal = DateTimeOffset.Now;

        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            && minutes is >= 1 and <= 24 * 60)
        {
            estimatedReadyAtUtc = nowLocal.AddMinutes(minutes).ToUniversalTime();
            return true;
        }

        var match = ClockPattern.Match(text);
        if (!match.Success
            || !int.TryParse(match.Groups["h"].Value, out var hour)
            || !int.TryParse(match.Groups["m"].Value, out var minute)
            || hour is < 0 or > 23
            || minute is < 0 or > 59)
        {
            error = "Geçersiz değer. Dakika yazın (örn. 15) veya saat yazın (örn. 9:23 / 09:23).";
            return false;
        }

        var candidate = new DateTimeOffset(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            hour,
            minute,
            0,
            nowLocal.Offset);
        if (candidate <= nowLocal)
        {
            candidate = candidate.AddDays(1);
        }

        estimatedReadyAtUtc = candidate.ToUniversalTime();
        return true;
    }
}
