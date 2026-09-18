using System.Collections.Concurrent;
using System.Net;

namespace RestaurantOS.Admin.Data;

public sealed class ApiCookieJarStore
{
    private readonly ConcurrentDictionary<string, CookieContainer> _jars = new(StringComparer.Ordinal);

    public CookieContainer GetOrCreate(string sessionId) =>
        _jars.GetOrAdd(sessionId, _ => new CookieContainer());

    public void Remove(string sessionId) => _jars.TryRemove(sessionId, out _);
}
