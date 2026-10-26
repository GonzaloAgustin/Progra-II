using Microsoft.AspNetCore.Mvc;
using PasajeDatosMVC.Extensions;
using PasajeDatosMVC.Models;

namespace PasajeDatosMVC.Controllers
{
    public class InscripcionesController : Controller
    {
        // GET: /Inscripciones/Index
        public IActionResult Index()
        {
            // ViewBag:
            // dato dinámico disponible únicamente durante este request.
            ViewBag.Titulo = "Inscripción de Estudiantes a Talleres Académicos";

            // ViewData:
            // enviamos al View una colección de talleres disponibles.
            ViewData["Talleres"] = new List<string>
            {
                "Introducción a ASP.NET Core",
                "Programación Web Avanzada",
                "Bases de Datos",
                "Inteligencia Artificial",
                "Arquitectura de Software"
            };

            // Verificamos si existe un alumno procesado
            // durante la sesión actual.
            var ultimoProcesado =
                HttpContext.Session
                    .Get<InscripcionViewModel>("UltimoProcesado");

            ViewBag.HayUltimoProcesado = ultimoProcesado != null;

            // ViewModel inicial para la vista.
            var model = new InscripcionViewModel
            {
                FechaInscripcion = DateTime.Now
            };

            return View(model);
        }

        // POST: /Inscripciones/Registrar
        [HttpPost]
        public IActionResult Registrar(InscripcionViewModel model)
        {
            // Model Binding:
            // ASP.NET Core construye automáticamente este objeto
            // utilizando los campos enviados por el formulario.

            if (!string.IsNullOrWhiteSpace(model.NombreAlumno))
            {
                // Si no vino una fecha válida, utilizamos la actual.
                if (model.FechaInscripcion == default)
                {
                    model.FechaInscripcion = DateTime.Now;
                }

                // Guardar objeto completo en Session.
                HttpContext.Session.Set(
                    "UltimoProcesado",
                    model
                );

                // TempData sobrevivirá a la redirección.
                TempData["MensajeResultado"] =
                    $"Inscripción registrada correctamente para " +
                    $"{model.NombreAlumno} - Legajo {model.Legajo}.";
            }
            else
            {
                TempData["MensajeResultado"] =
                    "No se pudo realizar la inscripción.";
            }

            // Patrón POST-Redirect-GET.
            return RedirectToAction("Index");
        }
    }
}