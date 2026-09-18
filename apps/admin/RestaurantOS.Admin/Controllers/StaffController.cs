using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class StaffController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageAsync(new InviteStaffViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new StaffPageViewModel
            {
                RoleCode = api.CurrentToken?.RoleCode,
                CurrentUserId = api.CurrentToken?.UserId ?? Guid.Empty,
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(
        [Bind(Prefix = "Invite")] InviteStaffViewModel model,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanManageStaff(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Kadro yalnızca Owner tarafından yönetilir.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }

        try
        {
            await api.PostAsync<StaffResponse>(
                "/api/v1/platform/staff",
                new
                {
                    email = model.Email.Trim(),
                    roleCode = model.RoleCode,
                    password = string.IsNullOrWhiteSpace(model.Password) ? null : model.Password,
                },
                cancellationToken);
            TempData["Message"] = "Operatör eklendi.";
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
    public async Task<IActionResult> ChangeRole(Guid userId, string roleCode, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanManageStaff(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Kadro yalnızca Owner tarafından yönetilir.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PatchAsync<StaffResponse>(
                $"/api/v1/platform/staff/{userId}",
                new { roleCode },
                cancellationToken);
            TempData["Message"] = "Rol güncellendi.";
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid userId, bool isActive, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanManageStaff(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Kadro yalnızca Owner tarafından yönetilir.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PatchAsync<StaffResponse>(
                $"/api/v1/platform/staff/{userId}",
                new { isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Operatör açıldı." : "Operatör pasifleştirildi.";
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

    private async Task<StaffPageViewModel> BuildPageAsync(InviteStaffViewModel invite, CancellationToken cancellationToken)
    {
        var members = await api.GetAsync<List<StaffResponse>>("/api/v1/platform/staff", cancellationToken)
            ?? [];
        return new StaffPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            CurrentUserId = api.CurrentToken?.UserId ?? Guid.Empty,
            Invite = invite,
            Members = members.Select(member => new StaffViewModel
            {
                UserId = member.UserId,
                Email = member.Email,
                RoleCode = member.RoleCode,
                IsActive = member.IsActive,
                GrantedAtUtc = member.GrantedAtUtc,
            }).ToArray(),
            ActiveOwnerCount = members.Count(member =>
                member.IsActive && string.Equals(member.RoleCode, "Owner", StringComparison.OrdinalIgnoreCase)),
        };
    }
}
