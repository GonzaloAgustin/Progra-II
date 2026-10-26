namespace PasajeDatosMVC.Models
{
    /// <summary>
    /// ViewModel utilizado por la pantalla de inscripción.
    /// Representa únicamente los datos necesarios para la vista.
    /// </summary>
    public class InscripcionViewModel
    {
        public string NombreAlumno { get; set; } = string.Empty;

        public string Legajo { get; set; } = string.Empty;

        public string TallerSeleccionado { get; set; } = string.Empty;

        public DateTime FechaInscripcion { get; set; }
    }
}