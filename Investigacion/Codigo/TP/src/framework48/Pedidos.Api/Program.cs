using System;
using System.Configuration;
using System.Threading;
using System.Web.Http;
using Autofac;
using Microsoft.Owin.Hosting;
using Owin;
using Pedidos.Api.Services;

namespace Pedidos.Api;

public static class Program
{
    public static void Main()
    {
        var cadena = ConfigurationManager.ConnectionStrings["Pedidos"].ConnectionString;
        var url = ConfigurationManager.AppSettings["Url"] ?? "http://localhost:5013/";
        using (var aplicacion = Composicion.Crear(cadena, null, null))
        using (WebApp.Start(url, builder => builder.UseWebApi(aplicacion.Configuracion)))
        using (IniciarReconciliacion(aplicacion.Contenedor))
        {
            Console.WriteLine("Pedidos API en " + url);
            Console.WriteLine("Swagger: " + url + "swagger");
            EsperarCierre();
        }
    }

    private static void EsperarCierre()
    {
        Console.WriteLine("ENTER para salir.");
        if (!Console.IsInputRedirected)
        {
            Console.ReadLine();
            return;
        }

        var cierre = new ManualResetEvent(false);
        Console.CancelKeyPress += (sender, args) =>
        {
            args.Cancel = true;
            cierre.Set();
        };
        cierre.WaitOne();
    }

    private static Timer IniciarReconciliacion(IContainer contenedor)
    {
        var habilitada = !string.Equals(ConfigurationManager.AppSettings["Reconciliacion:Habilitada"], "false", StringComparison.OrdinalIgnoreCase);
        if (!habilitada)
            return new Timer(_ => { });

        var segundos = 30;
        int.TryParse(ConfigurationManager.AppSettings["Reconciliacion:IntervaloSegundos"], out segundos);
        if (segundos < 5) segundos = 5;
        return new Timer(_ =>
        {
            try
            {
                using (var scope = contenedor.BeginLifetimeScope("AutofacWebRequest"))
                {
                    var servicio = scope.Resolve<IPedidoService>();
                    servicio.ReconciliarAsync(CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Falló la reconciliación: " + ex.Message);
            }
        }, null, TimeSpan.FromSeconds(segundos), TimeSpan.FromSeconds(segundos));
    }
}
