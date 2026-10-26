var builder = WebApplication.CreateBuilder(args);

// Agregar servicios de MVC al contenedor
builder.Services.AddControllersWithViews();

// 1. Configurar la caché distribuida en memoria para respaldar la Sesión
builder.Services.AddDistributedMemoryCache();

// 2. Configurar el servicio de Sesión (ISession)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(15); // Tiempo de inactividad predeterminado (20 min)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 3. Habilitar el middleware de Sesión (debe colocarse estrictamente después de UseRouting)
app.UseSession();

app.UseAuthorization();

// Enrutamiento convencional predeterminado
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Empleados}/{action=Index}/{id?}");

app.Run();