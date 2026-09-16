using SPQR.Core.Estructuras;
using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>
/// Segunda oportunidad. Es FIFO con una corrección: antes de desalojar la
/// cabeza de la fila, le mira el bit R.
///   · R = 1 → la página se usó hace poco. Se le apaga el bit, se la MUEVE al
///     final de la fila como si acabara de llegar y se sigue con la siguiente.
///   · R = 0 → esa es la víctima.
///
/// Si todas las páginas tienen R = 1, la vuelta completa las apaga todas y el
/// algoritmo degenera en FIFO puro, que es su peor caso.
///
/// IMPORTANTE — no es lo mismo que Reloj. Llegan al mismo criterio, pero
/// segunda oportunidad REORDENA la lista (saca de la cabeza y agrega al final)
/// mientras que reloj deja los marcos quietos y mueve una manecilla. El
/// docente lo aclaró expresamente en clase: son dos algoritmos distintos y
/// se programan por separado.
/// </summary>
public sealed class SegundaOportunidadPaginacion : IAlgoritmoPaginacion
{
    private readonly ListaEnlazada<int> _fila = new();

    public string Nombre => "Segunda oportunidad";
    public string Criterio => "FIFO que perdona a la cabeza si tiene el bit R encendido y la manda al final de la fila.";

    public void Reiniciar() => _fila.Limpiar();
    public void AlCargar(int marco, int instante) => _fila.AgregarAlFinal(marco);
    public void AlReferenciar(int marco, int instante) { }
    public void AlLiberar(int marco) => _fila.Quitar(m => m == marco);

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        // Como máximo una vuelta completa: en la peor de las vueltas se apagan
        // todos los bits R y la siguiente cabeza ya es víctima segura.
        for (var vueltas = 0; vueltas <= _fila.Cantidad; vueltas++)
        {
            if (_fila.EstaVacia) return 0;

            var candidato = _fila.QuitarDelInicio();
            var marco = c.Marcos[candidato];

            if (marco.Duenio is null)
                return candidato;

            ref var entrada = ref marco.Duenio.TablaDePaginas[marco.Pagina];

            if (!entrada.Referenciada)
            {
                _fila.AgregarAlInicio(candidato);   // la MMU lo sacará al desalojar
                return candidato;
            }

            entrada.Referenciada = false;           // se le da la segunda oportunidad
            _fila.AgregarAlFinal(candidato);        // y vuelve al final de la fila
        }
        return _fila.VerPrimero();
    }
}
