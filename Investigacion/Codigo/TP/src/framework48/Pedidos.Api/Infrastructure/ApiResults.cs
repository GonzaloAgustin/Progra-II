using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Pedidos.Api.Services;

namespace Pedidos.Api.Infrastructure;

public static class ApiResults
{
    public static IHttpActionResult Desde<T>(ApiController controller, Resultado<T> resultado, Func<T, IHttpActionResult> creado = null)
    {
        if (resultado.StatusCode == 201 && resultado.Value != null && creado != null)
            return creado(resultado.Value);
        if (resultado.StatusCode == 200 && resultado.Value != null)
            return new ObjetoResult(controller.Request, HttpStatusCode.OK, resultado.Value);
        var cuerpo = Errores.Crear(resultado.StatusCode, resultado.Title, resultado.Detail, controller.Request, resultado.PedidoId, resultado.Estado);
        return new ObjetoResult(controller.Request, (HttpStatusCode)resultado.StatusCode, cuerpo);
    }
}

public sealed class ObjetoResult : IHttpActionResult
{
    private readonly HttpRequestMessage _request;
    private readonly HttpStatusCode _status;
    private readonly object _cuerpo;

    public ObjetoResult(HttpRequestMessage request, HttpStatusCode status, object cuerpo)
    {
        _request = request;
        _status = status;
        _cuerpo = cuerpo;
    }

    public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_request.CreateResponse(_status, _cuerpo));
}
