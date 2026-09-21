namespace SPQR.Core.Dominio;

public enum Severidad { Error, Aviso }

/// <summary>Un problema encontrado en el escenario antes de simular.</summary>
public sealed record Problema(Severidad Severidad, string Donde, string Mensaje)
{
    public override string ToString()
        => $"{(Severidad == Severidad.Error ? "ERROR" : "AVISO")} · {Donde}: {Mensaje}";
}

/// <summary>
/// Revisa el escenario ANTES de simular.
///
/// Los setters del dominio ya acotan los valores imposibles —una ráfaga nunca
/// baja de 1, un porcentaje vive entre 1 y 100—, pero hay incoherencias que
/// ningún setter puede ver solo: una cadena que referencia una página que el
/// programa no tiene, un proceso parado en una cola que nadie definió, o una
/// configuración con tantos marcos que no habrá un solo reemplazo y los seis
/// algoritmos empatarán.
///
/// Sin esto, el único aviso era una excepción cruda en la barra de estado.
/// </summary>
public static class Validador
{
    public static List<Problema> Revisar(Escenario e)
    {
        var problemas = new List<Problema>();

        // ---------- catálogo ----------
        if (e.Catalogo.EstaVacia)
            problemas.Add(new Problema(Severidad.Error, "Catálogo",
                "No hay ningún programa definido."));

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in e.Catalogo)
        {
            var donde = $"Programa {p.Id}";

            if (!vistos.Add(p.Id))
                problemas.Add(new Problema(Severidad.Error, donde,
                    $"El identificador «{p.Id}» está repetido en el catálogo."));

            if (string.IsNullOrWhiteSpace(p.Nombre))
                problemas.Add(new Problema(Severidad.Error, donde, "El nombre está vacío."));

            if (p.CadenaDeReferencias.EstaVacia)
                problemas.Add(new Problema(Severidad.Aviso, donde,
                    "No tiene cadena de referencias: se recorrerán sus páginas en orden."));

            else if (p.PaginaMasAltaReferenciada >= p.TamanioEnPaginas)
                problemas.Add(new Problema(Severidad.Error, donde,
                    $"La cadena referencia la página {p.PaginaMasAltaReferenciada}, " +
                    $"pero el programa solo tiene {p.TamanioEnPaginas} " +
                    $"(páginas válidas: 0 a {p.TamanioEnPaginas - 1})."));
        }

        // ---------- lista de ejecución ----------
        if (e.ListaDeEjecucion.EstaVacia)
            problemas.Add(new Problema(Severidad.Error, "Lista de ejecución",
                "No hay ningún proceso para simular."));

        var usaColas = e.Config.UsaMultiplesColas;
        foreach (var proceso in e.ListaDeEjecucion)
        {
            var donde = $"Proceso {proceso.Id} ({proceso.Programa.Nombre})";

            if (e.BuscarPrograma(proceso.Programa.Id) is null)
                problemas.Add(new Problema(Severidad.Error, donde,
                    "Apunta a un programa que ya no está en el catálogo."));

            if (usaColas && !e.Config.ExisteCola(proceso.Cola))
                problemas.Add(new Problema(Severidad.Error, donde,
                    $"Está en la cola {proceso.Cola}, que no existe. " +
                    "Definila en «Reglas por cola» o movelo a una cola existente."));

            // La ráfaga manda: cada tick de CPU dispara exactamente un acceso a
            // memoria. Que la cadena se repita NO es un problema —un programa
            // que da dos vueltas a su bucle referencia dos veces las mismas
            // páginas, que es lo normal—; el problema es cortarla a la mitad,
            // porque entonces hay páginas que el programa declara y nunca toca,
            // y el rendimiento se mide sobre una cadena que no es la suya.
            //
            // Por eso la condición es el RESTO, no la igualdad: la ráfaga tiene
            // que cerrar un número entero de vueltas.
            var largo = proceso.Programa.CadenaDeReferencias.Cantidad;
            if (largo > 0 && proceso.Rafaga % largo != 0)
            {
                var vueltas = proceso.Rafaga / largo;
                var sueltas = proceso.Rafaga % largo;

                var detalle = vueltas == 0
                    ? $"solo se van a ver las primeras {sueltas} de {largo} referencias, y " +
                      (largo - sueltas == 1
                          ? "la restante no se toca nunca"
                          : $"las {largo - sueltas} restantes no se tocan nunca")
                    : $"se dan {vueltas} vuelta{(vueltas == 1 ? "" : "s")} completa" +
                      $"{(vueltas == 1 ? "" : "s")} y {sueltas} referencia" +
                      $"{(sueltas == 1 ? "" : "s")} más, cortando la cadena por la mitad";

                var sugerido = (vueltas + 1) * largo;
                var comoAjustar = sugerido == largo
                    ? $"Poné la ráfaga en {largo}, o en un múltiplo de {largo}"
                    : $"Poné la ráfaga en {sugerido} —el múltiplo de {largo} más cercano hacia arriba—";

                problemas.Add(new Problema(Severidad.Aviso, donde,
                    $"La ráfaga es {proceso.Rafaga} y la cadena del programa tiene {largo} " +
                    $"referencias: {detalle}. {comoAjustar} y el rendimiento se va a medir " +
                    "sobre la cadena completa."));
            }
        }

        // ---------- configuración ----------
        var paginasDelCatalogo = 0;
        foreach (var p in e.Catalogo) paginasDelCatalogo += p.TamanioEnPaginas;

        if (e.Config.MarcosFisicos >= paginasDelCatalogo && paginasDelCatalogo > 0)
            problemas.Add(new Problema(Severidad.Aviso, "Configuración SO",
                $"Hay {e.Config.MarcosFisicos} marcos para {paginasDelCatalogo} páginas. " +
                "Sobra memoria: no va a haber ni un reemplazo y los seis algoritmos " +
                "de la MMU van a dar exactamente el mismo resultado."));

        if (e.Config.IntervaloReinicioBitR == 0)
            problemas.Add(new Problema(Severidad.Aviso, "Configuración SO",
                "El reinicio del bit R está apagado. NRU, reloj y segunda oportunidad " +
                "van a degenerar: con todos los bits R encendidos dejan de distinguir páginas."));

        if (e.Config.AlgoritmoPlanificacion.Equals("Garantizada", StringComparison.OrdinalIgnoreCase))
        {
            var suma = 0;
            foreach (var p in e.Catalogo) suma += p.PorcentajeAsignacion;
            if (suma != 100)
                problemas.Add(new Problema(Severidad.Aviso, "Planificación garantizada",
                    $"Los porcentajes de asignación suman {suma} % y no 100 %. " +
                    "El reparto va a funcionar, pero las promesas no cierran."));
        }

        if (usaColas && e.Config.Colas.EstaVacia)
            problemas.Add(new Problema(Severidad.Error, "Reglas por cola",
                "El algoritmo es «múltiples colas» pero no hay ninguna cola definida."));

        return problemas;
    }

    public static bool HayErrores(List<Problema> problemas)
    {
        foreach (var p in problemas) if (p.Severidad == Severidad.Error) return true;
        return false;
    }
}
