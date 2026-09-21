using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>
/// Segunda oportunidad, tal como la corrigió el docente en la clase del 3 de
/// septiembre (ejemplo 1, 2, 3, 1, 4, 2, 5 con 3 marcos: 6 fallos, 1 acierto,
/// marcos finales 2, 4, 5). Él la resumió como «FIFO + R».
///
///   · La página ENTRA CON R = 0. «Si hablamos de segunda oportunidad, cuando
///     entra este bit está en 0; cambia hasta que hace una reutilización. Y
///     en el reloj, desde que entra, le coloca el bit en 1.» Esa es la
///     diferencia entre los dos algoritmos, y es la que hace que den
///     resultados distintos. (Antes las dos cargaban con R = 1 y por eso
///     daban idéntico.)
///
///   · Un acierto le pone R = 1: la página «gana una vida».
///
///   · Al reemplazar se recorren las páginas en orden de llegada (FIFO). La
///     que tiene R = 1 «gasta su vida»: se le apaga el bit y se la SALTA, pero
///     NO cambia de lugar en la fila. La primera con R = 0 es la víctima. En
///     el ejemplo, con el 4 el 1 gasta su vida y sale el 2; con el siguiente
///     fallo el 1 sigue siendo el más viejo, ya sin vida, y sale él.
///
///   · Si todas tenían R = 1, la vuelta las apaga a todas y sale la más vieja.
///
/// La página nueva ocupa el marco de la víctima y queda última en la fila.
/// </summary>
public sealed class SegundaOportunidadPaginacion : IAlgoritmoPaginacion
{
    public string Nombre => "Segunda oportunidad";
    public string Criterio =>
        "FIFO + bit R. Entra con R = 0; un acierto le da una vida (R = 1). Al reemplazar, a la que tiene vida se le quita y se la salta; sale la primera con R = 0.";

    public bool BitRAlCargar => false;

    public void Reiniciar() { }
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        // La fila FIFO es el orden de carga, que se lee de la tabla de páginas:
        // así no hay una segunda estructura que se pueda desincronizar. Se
        // recorre de la más vieja a la más nueva buscando en cada vuelta la
        // más vieja que todavía no se visitó (con pocos marcos, es trivial).
        var masVieja = -1;
        var desde = int.MinValue;
        for (var visitadas = 0; visitadas < c.Marcos.Length; visitadas++)
        {
            var siguiente = SiguienteEnLlegar(c.Marcos, desde);
            if (siguiente is null) break;
            if (masVieja < 0) masVieja = siguiente.Numero;

            ref var entrada = ref siguiente.Duenio!.TablaDePaginas[siguiente.Pagina];
            if (!entrada.Referenciada) return siguiente.Numero;   // sin vida: sale
            entrada.Referenciada = false;                           // gasta su vida y se la salta
            desde = entrada.InstanteDeCarga;
        }

        // Todas tenían vida: ya se la gastaron todas, sale la más vieja.
        return masVieja < 0 ? 0 : masVieja;
    }

    /// <summary>El marco ocupado que se cargó primero después del instante <paramref name="despuesDe"/>.</summary>
    private static MarcoFisico? SiguienteEnLlegar(MarcoFisico[] marcos, int despuesDe)
    {
        MarcoFisico? mejor = null;
        foreach (var marco in marcos)
        {
            if (marco.Duenio is null) continue;
            var carga = CargadaEn(marco);
            if (carga <= despuesDe) continue;
            if (mejor is null || carga < CargadaEn(mejor)) mejor = marco;
        }
        return mejor;
    }

    private static int CargadaEn(MarcoFisico marco)
        => marco.Duenio!.TablaDePaginas[marco.Pagina].InstanteDeCarga;
}
