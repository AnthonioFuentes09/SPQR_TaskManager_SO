using SPQR.Core.Estructuras;
using SPQR.Core.Memoria;

namespace SPQR.Core.Paginacion;

/// <summary>Contexto que la MMU entrega al algoritmo para que elija la víctima.</summary>
public sealed class ContextoReemplazo
{
    public required MarcoFisico[] Marcos { get; init; }
    public required int Instante { get; init; }

    /// <summary>
    /// Cadena de referencias futura, disponible porque la simulación se genera
    /// en dos pasadas. Es lo que hace implementable el algoritmo Óptimo.
    /// </summary>
    public required ListaEnlazada<(int ProcesoId, int Pagina)> Futuro { get; init; }
}

/// <summary>
/// Contrato de los seis algoritmos de reemplazo. Los seis se implementan;
/// cuál se usa se elige desde el menú, sin tocar el motor.
///
/// El ciclo de vida que ve el algoritmo es:
///   AlCargar     → una página acaba de ocupar un marco.
///   AlReferenciar→ una página residente fue accedida (acierto).
///   AlLiberar    → el marco quedó vacío tras un desalojo.
///   ElegirVictima→ no hay marcos libres: decidir a quién sacar.
/// </summary>
public interface IAlgoritmoPaginacion
{
    string Nombre { get; }

    /// <summary>Explicación corta para la pantalla de Ayuda y el informe.</summary>
    string Criterio { get; }

    void Reiniciar();
    void AlCargar(int marco, int instante);
    void AlReferenciar(int marco, int instante);
    void AlLiberar(int marco) { }

    /// <summary>Devuelve el número del marco que hay que desalojar.</summary>
    int ElegirVictima(ContextoReemplazo contexto);
}
