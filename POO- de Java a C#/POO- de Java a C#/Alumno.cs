using System;
using System.Collections.Generic;
using System.Text;

public class Alumno : Persona
{
    public Alumno(string nombre) : base(nombre)
    {
    }

    public override void Saludar()
    {
        Console.WriteLine("Hola, soy el alumno " + Nombre);
    }
}