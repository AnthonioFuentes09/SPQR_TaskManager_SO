using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>
/// NRU · no usada recientemente, tal como la resolvió el docente en la clase
/// del 1 de septiembre (ejemplo 1,2,3,3,5,1,2,2,6,2,1,5,7,6,3 con 4 marcos:
/// 7 fallos, marcos finales 7, 3, 6, 5).
///
/// Cada página tiene dos bits y la clase sale de combinarlos:
///   clase 0 · R0 M0   ni referenciada ni modificada
///   clase 1 · R0 M1   modificada, no referenciada
///   clase 2 · R1 M0   referenciada, no modificada
///   clase 3 · R1 M1   referenciada y modificada
///
/// Las tres reglas que usó en la pizarra:
///
///   1 · «El fallo significa que hubo una modificación en el marco de página».
///       La página que entra por fallo queda con R = 1 y M = 1 (clase 3). Un
///       acierto solo enciende R.
///
///   2 · «No usada recientemente» se mira desde el último fallo: los bits de
///       todas las páginas se limpian en cada fallo, y la clase de cada una
///       cuenta solo lo que le pasó desde entonces. Es la lectura que
///       reproduce su pizarra: al llegar el 6, el 3 queda en clase 0 porque
///       «cuando entra [el 5] no ha sido ni referenciado ni modificado»,
///       mientras que el 1 y el 2, que se volvieron a usar después, quedan
///       en clase 2 y el 5, recién cargado, en clase 3.
///
///   3 · Empate dentro de la clase más baja: sale la que entró primero a
///       memoria («el primero que se le [asignó] el marco de página»). Con el
///       7 empatan 1, 2 y 5 en clase 2 y sale el 1; con el 3 sale el 2.
///
/// Antes esta clase desempataba al azar, así que el resultado no podía
/// coincidir con la pizarra salvo por casualidad.
/// </summary>
public sealed class NruPaginacion : IAlgoritmoPaginacion
{
    public string Nombre => "NRU";
    public string Criterio =>
        "Clase = 2R + M, contando desde el último fallo (el fallo cuenta como modificación). Sale la de menor clase; si empatan, la que entró primero.";

    public bool BitMAlCargar => true;

    public void Reiniciar() { }
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    /// <summary>Regla 2: en cada fallo se limpian R y M de todas las residentes.</summary>
    public void AlFallar(MarcoFisico[] marcos)
    {
        foreach (var marco in marcos)
        {
            if (marco.Duenio is null) continue;
            ref var entrada = ref marco.Duenio.TablaDePaginas[marco.Pagina];
            entrada.Referenciada = false;
            entrada.Modificada = false;
        }
    }

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        var victima = 0;
        var mejorClase = int.MaxValue;
        var mejorCarga = int.MaxValue;

        foreach (var marco in c.Marcos)
        {
            if (marco.Duenio is null) continue;
            var entrada = marco.Duenio.TablaDePaginas[marco.Pagina];

            // menor clase primero; a igual clase, la que entró antes (regla 3)
            if (entrada.Clase < mejorClase
                || (entrada.Clase == mejorClase && entrada.InstanteDeCarga < mejorCarga))
            {
                mejorClase = entrada.Clase;
                mejorCarga = entrada.InstanteDeCarga;
                victima = marco.Numero;
            }
        }
        return victima;
    }
}
