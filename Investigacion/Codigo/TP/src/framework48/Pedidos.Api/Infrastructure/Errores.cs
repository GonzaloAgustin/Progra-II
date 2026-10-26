using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Pedidos.Api.Infrastructure;

public static class Errores
{
    public static Dictionary<string, object> Crear(int status, string title, string detail, HttpRequestMessage request, Guid? pedidoId = null, string estado = null)
    {
        var error = new Dictionary<string, object>
        {
            ["type"] = "https://httpstatuses.com/" + status,
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["instance"] = request.RequestUri.AbsolutePath
        };
        if (request.Properties.ContainsKey("CorrelationId"))
            error["traceId"] = request.Properties["CorrelationId"];
        if (pedidoId.HasValue)
            error["pedidoId"] = pedidoId.Value;
        if (!string.IsNullOrWhiteSpace(estado))
            error["estado"] = estado;
        return error;
    }
}

public static class Correlacion
{
    public static readonly AsyncLocal<string> Actual = new AsyncLocal<string>();
}

public sealed class CorrelationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = request.Headers.Contains("X-Correlation-Id")
            ? request.Headers.GetValues("X-Correlation-Id").FirstOrDefault()
            : Guid.NewGuid().ToString("N");
        request.Properties["CorrelationId"] = id;
        Correlacion.Actual.Value = id;
        var response = await base.SendAsync(request, cancellationToken);
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", id);
        return response;
    }
}

public sealed class ErrorFilter : ExceptionFilterAttribute
{
    public override void OnException(HttpActionExecutedContext context)
    {
        if (context.Exception is TaskCanceledException || context.Exception is OperationCanceledException)
            return;
        Trace.TraceError("Error no controlado en Pedidos: {0}", context.Exception);
        context.Response = context.Request.CreateResponse(HttpStatusCode.InternalServerError, Errores.Crear(500, "Error interno", "Ocurrió un error inesperado.", context.Request));
    }
}

public sealed class ValidacionFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(HttpActionContext actionContext)
    {
        if (actionContext.ModelState.IsValid)
            return;
        var detalle = string.Join(" ", actionContext.ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m)));
        if (string.IsNullOrWhiteSpace(detalle))
            detalle = "La solicitud no supera la validación.";
        actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.BadRequest, Errores.Crear(400, "Solicitud inválida", detalle, actionContext.Request));
    }
}
