namespace Productos.Api.Services;

public sealed class ServiceResult<T>
{
    public int StatusCode { get; private init; }
    public T? Value { get; private init; }
    public string? Title { get; private init; }
    public string? Detail { get; private init; }
    public Dictionary<string, object?> Extensions { get; } = new();

    public static ServiceResult<T> Ok(T value) => new() { StatusCode = StatusCodes.Status200OK, Value = value };
    public static ServiceResult<T> Created(T value) => new() { StatusCode = StatusCodes.Status201Created, Value = value };
    public static ServiceResult<T> NoContent() => new() { StatusCode = StatusCodes.Status204NoContent };
    public static ServiceResult<T> BadRequest(string detail) => Error(StatusCodes.Status400BadRequest, "Solicitud inválida", detail);
    public static ServiceResult<T> NotFound(string detail) => Error(StatusCodes.Status404NotFound, "No encontrado", detail);
    public static ServiceResult<T> Conflict(string detail) => Error(StatusCodes.Status409Conflict, "Conflicto", detail);

    public ServiceResult<T> With(string key, object? value)
    {
        Extensions[key] = value;
        return this;
    }

    private static ServiceResult<T> Error(int status, string title, string detail) =>
        new() { StatusCode = status, Title = title, Detail = detail };
}
