using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;
using SPQR.Core.Metricas;
using SPQR.Core.Paginacion;

namespace SPQR.Core.Memoria;

/// <summary>Lo que pasó al resolver un acceso a memoria.</summary>
public sealed record ResultadoAcceso
{
    public required bool Fallo { get; init; }
    public required int Marco { get; init; }
    public int? PaginaDesalojada { get; init; }
    public int? DuenioDeLaVictima { get; init; }
    public bool SeEscribioADisco { get; init; }
}

/// <summary>
/// Unidad de administración de memoria. Traduce el par (proceso, página) a un
/// marco físico y, cuando la página no está cargada, resuelve el fallo:
///
///   1. Busca un marco libre.
///   2. Si no hay, le pide una víctima al algoritmo configurado.
///   3. Si la víctima tiene el bit M encendido, la escribe al área de
///      intercambio antes de liberar el marco.
///   4. Carga la página pedida y actualiza la tabla de páginas del proceso.
///
/// La MMU no sabe QUÉ algoritmo la está asistiendo: solo conoce la interfaz.
/// Cambiar de LRU a reloj no toca una línea de esta clase.
/// </summary>
public sealed class Mmu
{
    private readonly ConfiguracionSO _config;
    private readonly IAlgoritmoPaginacion _algoritmo;

    public MarcoFisico[] Marcos { get; }
    public ListaEnlazada<int> MarcosLibres { get; } = new();
    public MetricasMmu Metricas { get; } = new();

    public Mmu(ConfiguracionSO config, IAlgoritmoPaginacion algoritmo)
    {
        _config = config;
        _algoritmo = algoritmo;
        _algoritmo.Reiniciar();

        Marcos = new MarcoFisico[config.MarcosFisicos];
        for (var i = 0; i < Marcos.Length; i++)
        {
            Marcos[i] = new MarcoFisico { Numero = i };
            MarcosLibres.AgregarAlFinal(i);
        }
    }

    public string NombreAlgoritmo => _algoritmo.Nombre;
    public int MarcosEnUso => Marcos.Length - MarcosLibres.Cantidad;

    /// <summary>
    /// Resuelve un acceso del proceso a una de sus páginas virtuales.
    /// <paramref name="futuro"/> son las referencias que faltan; solo el
    /// algoritmo Óptimo las mira.
    /// </summary>
    public ResultadoAcceso Acceder(
        Proceso proceso,
        Referencia referencia,
        int instante,
        ListaEnlazada<(int ProcesoId, int Pagina)> futuro)
    {
        Metricas.ReferenciasTotales++;

        var pagina = referencia.Pagina;
        if (pagina >= proceso.TablaDePaginas.Cantidad)
            pagina = proceso.TablaDePaginas.Cantidad == 0 ? 0 : pagina % proceso.TablaDePaginas.Cantidad;

        // --- acierto: la página ya está residente ---
        if (proceso.TablaDePaginas[pagina].Presente)
        {
            var marcoAcierto = proceso.TablaDePaginas[pagina].Marco;
            MarcarUso(proceso, pagina, referencia.EsEscritura, instante);
            _algoritmo.AlReferenciar(marcoAcierto, instante);
            return new ResultadoAcceso { Fallo = false, Marco = marcoAcierto };
        }

        // --- fallo de página ---
        Metricas.FallosDePagina++;

        int? paginaDesalojada = null, duenioVictima = null;
        var escribioADisco = false;

        int marco;
        if (!MarcosLibres.EstaVacia)
        {
            marco = MarcosLibres.QuitarDelInicio();
        }
        else
        {
            marco = _algoritmo.ElegirVictima(new ContextoReemplazo
            {
                Marcos = Marcos,
                Instante = instante,
                Futuro = futuro,
            });

            var victima = Marcos[marco];
            if (victima.Duenio is not null)
            {
                paginaDesalojada = victima.Pagina;
                duenioVictima = victima.Duenio.Id;

                ref var entradaVictima = ref victima.Duenio.TablaDePaginas[victima.Pagina];

                // El bit M decide el costo: si está sucia, va al área de intercambio.
                if (entradaVictima.Modificada)
                {
                    escribioADisco = true;
                    Metricas.EscriturasADisco++;
                }

                entradaVictima.Presente = false;
                entradaVictima.Marco = -1;
                entradaVictima.Referenciada = false;
                entradaVictima.Modificada = false;

                Metricas.Desalojos++;
            }
            victima.Liberar();
            _algoritmo.AlLiberar(marco);
        }

        // --- cargar la página pedida ---
        Marcos[marco].Duenio = proceso;
        Marcos[marco].Pagina = pagina;

        ref var entrada = ref proceso.TablaDePaginas[pagina];
        entrada.Presente = true;
        entrada.Marco = marco;
        entrada.InstanteDeCarga = instante;
        entrada.Referenciada = true;
        entrada.Modificada = referencia.EsEscritura;
        entrada.InstanteUltimoUso = instante;

        _algoritmo.AlCargar(marco, instante);

        return new ResultadoAcceso
        {
            Fallo = true,
            Marco = marco,
            PaginaDesalojada = paginaDesalojada,
            DuenioDeLaVictima = duenioVictima,
            SeEscribioADisco = escribioADisco,
        };
    }

    private static void MarcarUso(Proceso proceso, int pagina, bool escritura, int instante)
    {
        ref var e = ref proceso.TablaDePaginas[pagina];
        e.Referenciada = true;
        if (escritura) e.Modificada = true;
        e.InstanteUltimoUso = instante;
    }

    /// <summary>
    /// Apaga los bits R de todas las páginas residentes. El sistema operativo
    /// lo hace periódicamente para que «referenciada» signifique «usada hace
    /// poco» y no «usada alguna vez». NRU y reloj dependen de esto.
    /// </summary>
    public void ReiniciarBitsR()
    {
        foreach (var marco in Marcos)
        {
            if (marco.Duenio is null) continue;
            marco.Duenio.TablaDePaginas[marco.Pagina].Referenciada = false;
        }
    }

    /// <summary>Libera todos los marcos que pertenecían a un proceso que terminó.</summary>
    public void LiberarProceso(Proceso proceso)
    {
        foreach (var marco in Marcos)
        {
            if (marco.Duenio is null || marco.Duenio.Id != proceso.Id) continue;

            ref var entrada = ref proceso.TablaDePaginas[marco.Pagina];
            entrada.Presente = false;
            entrada.Marco = -1;

            marco.Liberar();
            _algoritmo.AlLiberar(marco.Numero);
            MarcosLibres.AgregarAlFinal(marco.Numero);
        }
    }

    public bool TocaLimpiarBitsR(int instante)
        => _config.IntervaloReinicioBitR > 0
        && instante > 0
        && instante % _config.IntervaloReinicioBitR == 0;
}
