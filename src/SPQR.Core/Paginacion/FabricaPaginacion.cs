namespace SPQR.Core.Paginacion;

/// <summary>
/// Traduce la clave elegida en el menú a la implementación concreta.
///
/// Los SEIS se programan y uno solo se elige al correr. El docente avisó que
/// no se sabrá cuál hay que usar hasta el momento de la demostración, así que
/// los seis tienen que estar terminados. Y aclaró expresamente que segunda
/// oportunidad y reloj son algoritmos DISTINTOS: acá van como dos entradas
/// separadas, con dos clases separadas.
/// </summary>
public static class FabricaPaginacion
{
    public static readonly (string Clave, string Titulo)[] Disponibles =
    [
        ("Optimo",             "Óptimo"),
        ("Nru",                "NRU"),
        ("Fifo",               "FIFO"),
        ("SegundaOportunidad", "Segunda oportunidad"),
        ("Reloj",              "Reloj"),
        ("Lru",                "LRU"),
    ];

    public static IAlgoritmoPaginacion Crear(string clave) =>
        clave.ToLowerInvariant() switch
        {
            "optimo" or "óptimo" or "opt" => new OptimoPaginacion(),
            "nru"                         => new NruPaginacion(),
            "fifo"                        => new FifoPaginacion(),
            "segundaoportunidad" or "segunda" => new SegundaOportunidadPaginacion(),
            "reloj" or "clock"            => new RelojPaginacion(),
            "lru"                         => new LruPaginacion(),
            _ => throw new ArgumentException($"Algoritmo de paginación desconocido: {clave}", nameof(clave)),
        };
}
