using System;
using System.Configuration;
using System.Threading;
using System.Web.Http;
using Microsoft.Owin.Hosting;
using Owin;

namespace Inventario.Api;

public static class Program
{
    public static void Main()
    {
        var cadena = ConfigurationManager.ConnectionStrings["Inventario"].ConnectionString;
        var url = ConfigurationManager.AppSettings["Url"] ?? "http://localhost:5012/";
        using (var aplicacion = Composicion.Crear(cadena, null))
        using (WebApp.Start(url, builder => builder.UseWebApi(aplicacion.Configuracion)))
        {
            Console.WriteLine("Inventario API en " + url);
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
}
