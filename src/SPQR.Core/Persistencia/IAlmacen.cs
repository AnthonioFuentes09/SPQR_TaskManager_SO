using SPQR.Core.Dominio;
using SPQR.Core.Simulacion;

namespace SPQR.Core.Persistencia;

/// <summary>Una corrida guardada, tal como la devuelve la vista ResumenDeCorridas.</summary>
public sealed record ResumenDeCorrida
{
    public required int Id { get; init; }
    public required DateTime EjecutadaEl { get; init; }
    public required string AlgoritmoPlanificacion { get; init; }
    public required string AlgoritmoPaginacion { get; init; }
    public required int MarcosFisicos { get; init; }
    public required int ReferenciasTotales { get; init; }
    public required int FallosDePagina { get; init; }
    public required int Desalojos { get; init; }
    public required int EscriturasADisco { get; init; }

    public double F => ReferenciasTotales == 0 ? 0 : Math.Round((double)FallosDePagina / ReferenciasTotales, 4);
    public double RendimientoPorcentual => Math.Round((1 - F) * 100, 2);
    public string Fecha => EjecutadaEl.ToString("dd/MM/yyyy HH:mm");
}

/// <summary>
/// Cómo se guarda el trabajo. El núcleo declara el contrato; quién lo
/// implementa —SQLite, JSON, lo que venga— es problema de otra capa.
///
/// Por eso esta interfaz vive en SPQR.Core y la implementación con SQLite
/// vive en SPQR.Persistence: el motor de simulación no arrastra ninguna
/// dependencia de base de datos, y se puede probar sin tocar un archivo.
///
/// La regla que no se rompe: ninguna clase de SPQR.UI escribe SQL. La
/// pantalla habla con esta interfaz; la implementación habla con la base.
/// </summary>
public interface IAlmacen
{
    /// <summary>Extensión propia de este almacén, para los diálogos de archivo.</summary>
    string Extension { get; }

    /// <summary>Filtro del cuadro de diálogo de Windows.</summary>
    string FiltroDeArchivo { get; }

    void GuardarEscenario(Escenario escenario, string ruta);

    Escenario AbrirEscenario(string ruta);

    /// <summary>Anota una corrida terminada. Devuelve su identificador.</summary>
    int RegistrarCorrida(string ruta, ResultadoSimulacion resultado, ConfiguracionSO config);

    /// <summary>El historial completo, de la más reciente a la más vieja.</summary>
    List<ResumenDeCorrida> Historial(string ruta);

    /// <summary>La bitácora de una corrida guardada.</summary>
    List<(int Instante, string Proceso, string Detalle)> BitacoraDe(string ruta, int corridaId);

    void BorrarHistorial(string ruta);
}
