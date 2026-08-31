namespace RestaurantOS.Web.Data;

public sealed class ApiSessionScope
{
    public static ApiSessionScope Restaurant { get; } = new("RestaurantOS");
    public static ApiSessionScope Platform { get; } = new("RestaurantOS.Platform");

    private ApiSessionScope(string prefix) => Prefix = prefix;

    public string Prefix { get; }
    public string AccessTokenSessionKey => $"{Prefix}.AccessToken";
    public string RestaurantNameSessionKey => $"{Prefix}.RestaurantName";
    public string BranchNameSessionKey => $"{Prefix}.BranchName";
    public string ApiJarIdSessionKey => $"{Prefix}.ApiJarId";
}

public interface IPlatformWebApiExecuter : IWebApiExecuter
{
    Task LoginPlatformAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
