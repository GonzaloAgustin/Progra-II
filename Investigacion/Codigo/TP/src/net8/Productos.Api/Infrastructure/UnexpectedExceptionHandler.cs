using Microsoft.AspNetCore.Diagnostics;

namespace Productos.Api.Infrastructure;

public sealed class UnexpectedExceptionHandler : IExceptionHandler
{
    private readonly ILogger<UnexpectedExceptionHandler> _logger;

    public UnexpectedExceptionHandler(ILogger<UnexpectedExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Error no controlado en Productos.");

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno",
            Detail = "Ocurrió un error inesperado.",
            Instance = httpContext.Request.Path,
            Type = "https://httpstatuses.com/500"
        };

        if (httpContext.Items["CorrelationId"] is string correlationId)
            problem.Extensions["traceId"] = correlationId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
