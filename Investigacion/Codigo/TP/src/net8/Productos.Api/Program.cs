using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Productos.Api.Data;
using Productos.Api.Infrastructure;
using Productos.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Productos API",
        Version = "1.0.0",
        Description = "Catálogo de categorías y productos. Las consultas públicas devuelven solo recursos activos."
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

builder.Services.AddDbContext<ProductosDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Productos")));

builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IProductoService, ProductoService>();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();

if (app.Configuration.GetValue("ApplyMigrations", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductosDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Database");
    await DatabaseBootstrap.ApplyAsync(db, logger);
    if (app.Configuration.GetValue("SeedDemoData", false))
        await DemoSeeder.SeedAsync(db);
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Productos API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();
app.Run();
