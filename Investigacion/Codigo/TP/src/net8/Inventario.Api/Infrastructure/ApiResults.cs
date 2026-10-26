using Inventario.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Infrastructure;

public static class ApiResults
{
    public static IActionResult From<T>(ControllerBase controller, ServiceResult<T> result, Func<T, IActionResult>? created = null)
    {
        if (result.StatusCode == StatusCodes.Status201Created && result.Value is not null && created is not null)
            return created(result.Value);
        if (result.StatusCode == StatusCodes.Status200OK && result.Value is not null)
            return controller.Ok(result.Value);

        var problem = new ProblemDetails
        {
            Status = result.StatusCode,
            Title = result.Title,
            Detail = result.Detail,
            Instance = controller.HttpContext.Request.Path,
            Type = $"https://httpstatuses.com/{result.StatusCode}"
        };
        if (controller.HttpContext.Items["CorrelationId"] is string correlationId)
            problem.Extensions["traceId"] = correlationId;
        foreach (var extension in result.Extensions)
            problem.Extensions[extension.Key] = extension.Value;

        return new ObjectResult(problem) { StatusCode = result.StatusCode };
    }
}
