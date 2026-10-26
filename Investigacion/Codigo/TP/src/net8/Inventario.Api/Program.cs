using System.Reflection;
using Inventario.Api.Clients;
using Inventario.Api.Data;
using Inventario.Api.Infrastructure;
using Inventario.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Inventario API",
        Version = "1.0.0",
        Description = "Stock, reservas atómicas y liberaciones idempotentes. No lee la base de Productos."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
        options.IncludeXmlComments(xml);
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Solicitud inválida",
            Instance = context.HttpContext.Request.Path
        };
        if (context.HttpContext.Items["CorrelationId"] is string correlationId)
            problem.Extensions["traceId"] = correlationId;
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
    };
});

builder.Services.AddDbContext<InventarioDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Inventario")));

var timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Servicios:TimeoutSegundos", 5));
builder.Services.AddTransient<CorrelationHandler>();
builder.Services.AddHttpClient<IProductosClient, ProductosClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Servicios:Productos"] ?? "http://localhost:5001");
    client.Timeout = timeout;
}).AddHttpMessageHandler<CorrelationHandler>();

builder.Services.AddScoped<IInventarioService, InventarioService>();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();

if (app.Configuration.GetValue("ApplyMigrations", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    await DatabaseBootstrap.ApplyAsync(db, logger);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventario API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
