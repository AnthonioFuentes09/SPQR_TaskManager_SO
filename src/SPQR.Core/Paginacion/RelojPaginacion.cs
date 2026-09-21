namespace SPQR.Core.Paginacion;

/// <summary>
/// Reloj. Los marcos se ven como una lista CIRCULAR fija y hay una manecilla
/// que apunta a uno de ellos. Al necesitar una víctima:
///   · si el marco apuntado tiene R = 0 → es la víctima, y la manecilla avanza
///     una posición;
///   · si tiene R = 1 → se le apaga el bit y la manecilla avanza, sin mover
///     el marco de lugar.
///
/// Mientras haya marcos libres la manecilla no se mueve: «el puntero
/// permanece en su posición» (exposición del 3 de septiembre). Solo camina
/// cuando hay que reemplazar.
///
/// Diferencia con segunda oportunidad, según el docente: «en el reloj, desde
/// que entra, le coloca el bit en 1»; en segunda oportunidad entra en 0. Por
/// eso esta clase deja el bit R = 1 al cargar (el valor por defecto de
/// <see cref="IAlgoritmoPaginacion.BitRAlCargar"/>) y segunda oportunidad no.
///
/// Ejemplo de clase: 1,2,3,4,1,2,5,1,2,3,4,5 con 4 marcos → 10 fallos,
/// 2 aciertos, rendimiento 17 %.
/// </summary>
public sealed class RelojPaginacion : IAlgoritmoPaginacion
{
    private int _manecilla;

    public string Nombre => "Reloj";
    public string Criterio =>
        "Entra con R = 1. Manecilla circular: si apunta a R = 1 lo apaga y avanza; si apunta a R = 0, ese sale y la manecilla avanza.";

    public void Reiniciar() => _manecilla = 0;
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        var n = c.Marcos.Length;
        if (n == 0) return 0;

        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        // A lo sumo dos vueltas: la primera apaga bits, la segunda encuentra víctima.
        for (var pasos = 0; pasos < n * 2; pasos++)
        {
            var actual = c.Marcos[_manecilla % n];
            var indice = _manecilla % n;
            _manecilla = (_manecilla + 1) % n;

            if (actual.Duenio is null) return indice;

            ref var entrada = ref actual.Duenio.TablaDePaginas[actual.Pagina];
            if (!entrada.Referenciada) return indice;

            entrada.Referenciada = false;   // segunda oportunidad, sin reordenar
        }
        return _manecilla % n;
    }
}
