namespace RestaurantOS.Domain;

public static class CustomerMenuThemes
{
    public static string? ResolveEffective(string? themeId, bool canUseMenuThemes) =>
        canUseMenuThemes ? themeId : null;
}
