using System.Security.Claims;

namespace Diginsight.Analyzer.Repositories;

public interface ICallContextAccessor
{
    ClaimsPrincipal User { get; }

    IDictionary<object, object?> Items { get; }
}
