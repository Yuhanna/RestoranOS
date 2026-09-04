namespace RestaurantOS.Infrastructure;

/// <summary>
/// Masa operasyon durumu (MÜSAİT / DOLU) kuralları.
/// </summary>
public static class TableOccupancyPolicy
{
    /// <summary>QR müşteri oturumu üst sınırı — tüm gün açık ekran için 16 saat.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(16);

    /// <summary>Hesap talebi garsonca tamamlandıktan sonra hareketsizlik süresi.</summary>
    public static readonly TimeSpan PostBillInactivityGrace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Aktif sipariş yokken oturum masayı DOLU sayılır mı?
    /// </summary>
    public static bool CountsAsOccupied(
        DateTimeOffset now,
        DateTimeOffset sessionExpiresAtUtc,
        DateTimeOffset lastSessionActivityUtc,
        DateTimeOffset? lastBillCompletedAtUtc)
    {
        if (sessionExpiresAtUtc <= now)
        {
            return false;
        }

        if (now > lastSessionActivityUtc.Add(SessionLifetime))
        {
            return false;
        }

        if (lastBillCompletedAtUtc is null)
        {
            return true;
        }

        var billCompleted = lastBillCompletedAtUtc.Value;
        if (lastSessionActivityUtc > billCompleted)
        {
            return true;
        }

        return now <= billCompleted.Add(PostBillInactivityGrace);
    }
}
