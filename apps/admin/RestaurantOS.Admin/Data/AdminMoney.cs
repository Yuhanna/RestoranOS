using System.Globalization;

namespace RestaurantOS.Admin.Data;

public static class AdminMoney
{
    public static bool TryToMinorUnits(string? productCode, string? amountText, out long amountMinor, out string? error)
    {
        amountMinor = 0;
        error = null;
        if (string.Equals(productCode, "Free", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(amountText))
        {
            error = "Geçerli bir tutar girin.";
            return false;
        }

        var trimmed = amountText.Trim();
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        decimal lira;
        if (trimmed.Contains(',', StringComparison.Ordinal))
        {
            if (!decimal.TryParse(trimmed, NumberStyles.Number, turkish, out lira)
                && !decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out lira))
            {
                error = "Geçerli bir tutar girin.";
                return false;
            }
        }
        else if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out lira))
        {
            error = "Geçerli bir tutar girin.";
            return false;
        }

        if (lira < 0)
        {
            error = "Geçerli bir tutar girin.";
            return false;
        }

        amountMinor = (long)Math.Round(lira * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
