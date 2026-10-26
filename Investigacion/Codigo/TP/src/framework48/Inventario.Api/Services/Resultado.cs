namespace Inventario.Api.Services;

public sealed class Resultado<T>
{
    public int StatusCode { get; private set; }
    public T Value { get; private set; }
    public string Title { get; private set; }
    public string Detail { get; private set; }

    public static Resultado<T> Ok(T value) => new Resultado<T> { StatusCode = 200, Value = value };
    public static Resultado<T> Created(T value) => new Resultado<T> { StatusCode = 201, Value = value };
    public static Resultado<T> BadRequest(string detail) => Error(400, "Solicitud inválida", detail);
    public static Resultado<T> NotFound(string detail) => Error(404, "No encontrado", detail);
    public static Resultado<T> Conflict(string detail) => Error(409, "Conflicto", detail);
    public static Resultado<T> Unavailable(string detail) => Error(503, "Dependencia no disponible", detail);

    private static Resultado<T> Error(int status, string title, string detail) =>
        new Resultado<T> { StatusCode = status, Title = title, Detail = detail };
}
