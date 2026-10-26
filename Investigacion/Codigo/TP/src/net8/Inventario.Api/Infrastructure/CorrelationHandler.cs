namespace Inventario.Api.Infrastructure;

public sealed class CorrelationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_accessor.HttpContext?.Items["CorrelationId"] is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
