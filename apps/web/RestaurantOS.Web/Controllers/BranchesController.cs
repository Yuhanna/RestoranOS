using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class BranchesController(IWebApiExecuter api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(Guid? branchId, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = new BranchesPageViewModel();
        try
        {
            var workspace = await api.GetWorkspaceAsync(cancellationToken);
            model.Workspace = workspace;
            model.CanUseMultiBranch = workspace?.Entitlements?.CanUseMultiBranch == true;
            model.MaxBranches = workspace?.Entitlements?.MaxBranches;

            var memberships = await api.InvokeGetAsync<List<MembershipScopeViewModel>>(
                "/api/v1/management/auth/memberships",
                cancellationToken) ?? [];
            model.Memberships = memberships;
            model.CanManageBranches = memberships.Any(x =>
                x.BranchId == (workspace?.BranchId ?? Guid.Empty) && x.CanManageBranches);
            model.CanManageMembers = workspace?.CanManageMembers == true
                || memberships.Any(x =>
                    x.BranchId == (workspace?.BranchId ?? Guid.Empty) && x.CanManageMembers);

            if (model.CanManageBranches)
            {
                try
                {
                    model.Billing = await api.InvokeGetAsync<BranchBillingPreviewViewModel>(
                        "/api/v1/management/branches/billing-preview",
                        cancellationToken);
                }
                catch (WebApiException)
                {
                }

                var branches = await api.InvokeGetAsync<List<BranchListItemViewModel>>(
                    "/api/v1/management/branches",
                    cancellationToken) ?? [];
                model.Branches = branches;

                try
                {
                    model.Network = await api.InvokeGetAsync<NetworkSummaryViewModel>(
                        "/api/v1/management/branches/network-summary",
                        cancellationToken);
                }
                catch (WebApiException)
                {
                }

                var selectedId = branchId
                    ?? branches.FirstOrDefault(x => x.IsCurrent)?.Id
                    ?? branches.FirstOrDefault()?.Id;
                model.SelectedBranchId = selectedId;
                if (selectedId is Guid id)
                {
                    model.InviteMember.BranchId = id;
                    try
                    {
                        model.SelectedMembers = await api.InvokeGetAsync<List<BranchMemberViewModel>>(
                            $"/api/v1/management/branches/{id}/members",
                            cancellationToken) ?? [];
                    }
                    catch (WebApiException exception)
                    {
                        TempData["Error"] = exception.Message;
                    }
                }
            }
            else
            {
                model.Branches = memberships
                    .Where(x => x.TenantId == workspace?.TenantId)
                    .Select(x => new BranchListItemViewModel
                    {
                        Id = x.BranchId,
                        RestaurantId = x.RestaurantId,
                        Name = x.BranchName,
                        IsCurrent = x.BranchId == workspace?.BranchId,
                    })
                    .ToArray();
                model.SelectedBranchId = workspace?.BranchId
                    ?? model.Branches.FirstOrDefault(x => x.IsCurrent)?.Id
                    ?? (model.Branches.Count > 0 ? model.Branches[0].Id : null);

                if (model.CanManageMembers && workspace?.BranchId is Guid managerBranchId)
                {
                    model.SelectedBranchId = managerBranchId;
                    model.InviteMember.BranchId = managerBranchId;
                    model.InviteMember.RoleKey = "staff";
                    try
                    {
                        model.SelectedMembers = await api.InvokeGetAsync<List<BranchMemberViewModel>>(
                            $"/api/v1/management/branches/{managerBranchId}/members",
                            cancellationToken) ?? [];
                    }
                    catch (WebApiException exception)
                    {
                        TempData["Error"] = exception.Message;
                    }
                }
            }
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "CreateBranch")] CreateBranchViewModel createBranch,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Şube adı gerekli.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.InvokePostAsync<BranchListItemViewModel>(
                "/api/v1/management/branches",
                new
                {
                    name = createBranch.Name,
                    confirmAddonPurchase = createBranch.ConfirmAddonPurchase,
                },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = createBranch.ConfirmAddonPurchase
                ? "Ek şube koltuğu onaylandı ve şube oluşturuldu."
                : "Yeni şube oluşturuldu. Merkez hesabınız bu şubeye de eklendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            if (string.Equals(exception.Error?.Code, "BRANCH_ADDON_REQUIRED", StringComparison.OrdinalIgnoreCase))
            {
                TempData["AddonRequired"] = "1";
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpgradePro(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/subscription/checkout",
                new { planCode = "Pro" },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Pro plana geçildi. Dahil 2 şube aktif; ek şubeler ücretlidir.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestEnterpriseQuote(
        EnterpriseQuoteViewModel enterpriseQuote,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/subscription/enterprise-quote",
                new
                {
                    contactName = enterpriseQuote.ContactName,
                    email = enterpriseQuote.Email,
                    phone = enterpriseQuote.Phone,
                    estimatedBranchCount = enterpriseQuote.EstimatedBranchCount,
                    note = enterpriseQuote.Note,
                },
                cancellationToken);
            TempData["Message"] = "Enterprise teklif talebiniz alındı. Satış ekibi sizinle iletişime geçecek.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(RenameBranchViewModel rename, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePatchAsync<BranchListItemViewModel>(
                $"/api/v1/management/branches/{rename.BranchId}",
                new { name = rename.Name },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Şube adı güncellendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId = rename.BranchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(
        [Bind(Prefix = "InviteMember")] InviteBranchMemberViewModel invite,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (invite.BranchId == Guid.Empty)
        {
            TempData["Error"] = "Hesap eklemek için önce bir şube seçin.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.InvokePostAsync<BranchMemberViewModel>(
                $"/api/v1/management/branches/{invite.BranchId}/members",
                new
                {
                    email = invite.Email,
                    displayName = invite.DisplayName,
                    phone = string.IsNullOrWhiteSpace(invite.Phone) ? null : invite.Phone,
                    password = string.IsNullOrWhiteSpace(invite.Password) ? null : invite.Password,
                    roleKey = invite.RoleKey,
                },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Şube hesabı hazır. Kullanıcı e-posta ve şifresiyle giriş yapabilir.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId = invite.BranchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateMember(
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                $"/api/v1/management/branches/{branchId}/members/{membershipId}/deactivate",
                null,
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Üyelik pasifleştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateMember(
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                $"/api/v1/management/branches/{branchId}/members/{membershipId}/activate",
                null,
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Üyelik yeniden aktifleştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMember(
        UpdateBranchMemberViewModel update,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePatchAsync<BranchMemberViewModel>(
                $"/api/v1/management/branches/{update.BranchId}/members/{update.MembershipId}",
                new
                {
                    displayName = update.DisplayName,
                    phone = string.IsNullOrWhiteSpace(update.Phone) ? null : update.Phone,
                    roleKey = string.IsNullOrWhiteSpace(update.RoleKey) ? null : update.RoleKey,
                },
                cancellationToken);
            TempData["Message"] = "Hesap güncellendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId = update.BranchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetMemberPassword(
        ResetBranchMemberPasswordViewModel reset,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                $"/api/v1/management/branches/{reset.BranchId}/members/{reset.MembershipId}/reset-password",
                new { newPassword = reset.NewPassword },
                cancellationToken);
            TempData["Message"] = "Şifre güncellendi. Kullanıcı yeni şifreyle giriş yapmalı.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index), new { branchId = reset.BranchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch(Guid branchId, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.SwitchBranchAsync(branchId, cancellationToken);
            TempData["Message"] = "Aktif şube değiştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index), new { branchId });
    }
}
