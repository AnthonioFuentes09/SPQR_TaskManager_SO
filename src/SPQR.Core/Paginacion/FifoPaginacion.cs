using SPQR.Core.Estructuras;

namespace SPQR.Core.Paginacion;

/// <summary>
/// FIFO de paginación. Lleva una lista enlazada con el orden en que se
/// cargaron los marcos y desaloja siempre la cabeza: la página que lleva más
/// tiempo en memoria, sin mirar si se está usando.
///
/// Es el único de los seis que sufre la ANOMALÍA DE BELADY: agregarle marcos
/// a la máquina puede hacer que aumenten los fallos en lugar de disminuir.
/// Ese es justamente el argumento de por qué los otros cinco existen.
/// </summary>
public sealed class FifoPaginacion : IAlgoritmoPaginacion
{
    private readonly ListaEnlazada<int> _ordenDeCarga = new();

    public string Nombre => "FIFO";
    public string Criterio => "Desaloja la página que lleva más tiempo cargada. Sufre la anomalía de Belady.";

    public void Reiniciar() => _ordenDeCarga.Limpiar();

    public void AlCargar(int marco, int instante) => _ordenDeCarga.AgregarAlFinal(marco);

    /// <summary>FIFO puro ignora los aciertos: un uso no cambia la antigüedad.</summary>
    public void AlReferenciar(int marco, int instante) { }

    public void AlLiberar(int marco) => _ordenDeCarga.Quitar(m => m == marco);

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;
        var cabeza = _ordenDeCarga.VerPrimero();
        return _ordenDeCarga.EstaVacia ? 0 : cabeza;
    }
}
