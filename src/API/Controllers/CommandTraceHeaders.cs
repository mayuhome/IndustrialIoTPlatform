using Microsoft.Extensions.Primitives;

namespace API.Controllers;

internal static class CommandTraceHeaders
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string CausationHeader = "X-Causation-Id";

    public static (Guid CorrelationId, Guid? CausationId) Read(HttpRequest request)
    {
        var correlationId = TryReadGuid(request.Headers, CorrelationHeader) ?? Guid.NewGuid();
        var causationId = TryReadGuid(request.Headers, CausationHeader);
        return (correlationId, causationId);
    }

    private static Guid? TryReadGuid(IHeaderDictionary headers, string key)
    {
        if (!headers.TryGetValue(key, out StringValues value) || StringValues.IsNullOrEmpty(value))
        {
            return null;
        }

        return Guid.TryParse(value.ToString(), out var parsed)
            ? parsed
            : null;
    }
}
