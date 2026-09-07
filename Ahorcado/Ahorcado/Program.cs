using System;
using System.Collections.Generic;
using System.Linq;

namespace Ahorcado
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string palabraSecreta = "";
            bool palabraValida = false;

            while (!palabraValida){
                Console.Write("Ingrese la palabra secreta: ");
                palabraSecreta = Console.ReadLine().ToLower();

                palabraSecreta = QuitarTildes(palabraSecreta);

                if (palabraSecreta.Length < 3){
                    Console.WriteLine("La palabra debe tener un mínimo de 3 caracteres.");
                }
                else{
                    palabraValida = true;

                    for (int i = 0; i < palabraSecreta.Length; i++){
                        if (!char.IsLetter(palabraSecreta[i]))
                        {
                            palabraValida = false;
                        }
                    }

                    if (!palabraValida){
                        Console.WriteLine("Ingrese una palabra sin números ni signos.");
                    }
                }
            }

            char[] palabraOculta = new char[palabraSecreta.Length];
            List<char> letrasUsadas = new List<char>();

            int intentos = 6;

            InicializarPalabra(palabraOculta);

            Console.Clear();

            Console.WriteLine("JUEGO DEL AHORCADO");

            while (intentos > 0 && !PalabraCompleta(palabraOculta)){
                Console.WriteLine();

                MostrarPalabra(palabraOculta);

                Console.WriteLine("Intentos restantes: " + intentos);

                char letra = PedirLetra();

                if (char.IsLetter(letra))
                {
                    if (letrasUsadas.Contains(letra))
                    {
                        Console.WriteLine("Ya usaste esa letra.");
                    }
                    else
                    {
                        letrasUsadas.Add(letra);

                        if (palabraSecreta.Contains(letra))
                        {
                            Console.WriteLine("¡Letra correcta!");
                            ActualizarPalabra(palabraSecreta, palabraOculta, letra);
                        }
                        else
                        {
                            Console.WriteLine("Letra incorrecta.");
                            intentos--;
                        }
                    }
                } else
                {
                    Console.WriteLine("Letra incorrecta.");
                }
            }

            Console.WriteLine();

            if (PalabraCompleta(palabraOculta)){
                Console.WriteLine("¡GANASTE!");
                Console.WriteLine("La palabra era: " + palabraSecreta);
            }
            else{
                Console.WriteLine("¡PERDISTE!");
                Console.WriteLine("La palabra era: " + palabraSecreta);
            }
        }


        static void InicializarPalabra(char[] palabraOculta){
            for (int i = 0; i < palabraOculta.Length; i++)
            {
                palabraOculta[i] = '_';
            }
        }


        static void MostrarPalabra(char[] palabraOculta){
            Console.Write("Palabra: ");

            for (int i = 0; i < palabraOculta.Length; i++){
                Console.Write(palabraOculta[i] + " ");
            }
            Console.WriteLine();
        }


        static char PedirLetra(){
            Console.Write("Ingrese una letra: ");

            string entrada = Console.ReadLine().ToLower();

            return entrada[0];
        }


        static void ActualizarPalabra(
            string palabraSecreta,
            char[] palabraOculta,
            char letra
        ){
            for (int i = 0; i < palabraSecreta.Length; i++)
            {
                if (palabraSecreta[i] == letra){
                    palabraOculta[i] = letra;
                }
            }
        }


        static bool PalabraCompleta(char[] palabraOculta){
            for (int i = 0; i < palabraOculta.Length; i++)
            {
                if (palabraOculta[i] == '_'){
                    return false;
                }
            }
            return true;
        }

        static string QuitarTildes(string palabra){
            palabra = palabra.Replace("á", "a");
            palabra = palabra.Replace("é", "e");
            palabra = palabra.Replace("í", "i");
            palabra = palabra.Replace("ó", "o");
            palabra = palabra.Replace("ú", "u");
            palabra = palabra.Replace("ü", "u");

            return palabra;
        }
    }
}