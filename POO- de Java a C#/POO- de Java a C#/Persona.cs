using System;
using System.Collections.Generic;
using System.Text;

public class Persona
{
    public string Nombre { get; set; }

    public Persona(string nombre)
    {
        Nombre = nombre;
    }

    public virtual void Saludar()
    {
        Console.WriteLine("Hola " + Nombre);
    }
}