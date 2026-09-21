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
/// El ciclo de vida que ve el algoritmo en cada FALLO es, en este orden:
///   ElegirVictima → solo si no hay marcos libres: decidir a quién sacar.
///   AlLiberar     → el marco de la víctima quedó vacío.
///   AlFallar      → hubo un fallo (con o sin víctima). NRU limpia bits acá.
///   AlCargar      → la página pedida ocupó su marco.
/// Y en cada ACIERTO:
///   AlReferenciar → una página residente fue accedida.
///
/// Las reglas de cada algoritmo siguen las que el docente resolvió en clase
/// (1 y 3 de septiembre); cada clase dice de qué ejemplo sale su regla.
/// </summary>
public interface IAlgoritmoPaginacion
{
    string Nombre { get; }

    /// <summary>Explicación corta para la pantalla de Ayuda y el informe.</summary>
    string Criterio { get; }

    /// <summary>
    /// Cómo queda el bit R de una página recién cargada. En casi todos es 1,
    /// pero en segunda oportunidad el docente lo deja en 0: la página recién
    /// entra y todavía no ganó ninguna «vida» («en segunda oportunidad, cuando
    /// entra, este bit está en 0… en el reloj, desde que entra, le coloca el
    /// bit en 1», clase del 3 de septiembre).
    /// </summary>
    bool BitRAlCargar => true;

    /// <summary>
    /// Si la carga por fallo cuenta como modificación. Solo NRU lo usa: «el
    /// fallo significa que hubo una modificación en el marco de página»
    /// (clase del 1 de septiembre).
    /// </summary>
    bool BitMAlCargar => false;

    void Reiniciar();
    void AlCargar(int marco, int instante);
    void AlReferenciar(int marco, int instante);
    void AlLiberar(int marco) { }

    /// <summary>
    /// Se llama en cada fallo, después de desalojar (si hizo falta) y antes de
    /// cargar la página nueva.
    /// </summary>
    void AlFallar(MarcoFisico[] marcos) { }

    /// <summary>Devuelve el número del marco que hay que desalojar.</summary>
    int ElegirVictima(ContextoReemplazo contexto);
}
