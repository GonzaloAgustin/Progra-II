using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace PasajeDatosMVC.Extensions
{
    /// <summary>
    /// Métodos de extensión para almacenar y recuperar
    /// objetos complejos dentro de ISession utilizando JSON.
    /// </summary>
    public static class SessionExtensions
    {
        public static void Set<T>(
            this ISession session,
            string clave,
            T valor)
        {
            string json = JsonSerializer.Serialize(valor);

            session.SetString(clave, json);
        }

        public static T? Get<T>(
            this ISession session,
            string clave)
        {
            string? valor = session.GetString(clave);

            if (valor == null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(valor);
        }
    }
}