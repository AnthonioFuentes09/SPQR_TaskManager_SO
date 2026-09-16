using System.Collections.ObjectModel;
using System.Windows.Threading;
using SPQR.Core.Dominio;
using SPQR.Core.Paginacion;
using SPQR.Core.Planificacion;
using SPQR.Core.Simulacion;

namespace SPQR.UI.ViewModels;

/// <summary>
/// El cerebro de la interfaz. Sostiene el escenario, manda a simular y luego
/// REPRODUCE el resultado: el reloj de reproducción no vuelve a llamar al
/// motor, solo avanza un índice sobre la lista de eventos y sobre las fotos
/// de memoria que el motor ya dejó calculadas.
/// </summary>
public sealed class PrincipalViewModel : BaseViewModel
{
    private readonly DispatcherTimer _reloj = new();
    private ResultadoSimulacion? _resultado;

    private int _pantalla;
    private int _instante;
    private bool _reproduciendo;
    private double _velocidad = 1;
    private string _mensaje = "Escenario de ejemplo cargado. Revisá la configuración y presioná «Simular».";
    private Proceso? _procesoObservado;

    public PrincipalViewModel()
    {
        Escenario = SPQR.Core.Dominio.Escenario.PorDefecto();

        foreach (var p in Escenario.Catalogo) Programas.Add(p);
        foreach (var p in Escenario.ListaDeEjecucion) Procesos.Add(p);
        foreach (var c in Config.Colas) Colas.Add(c);

        for (var i = 0; i < Config.MarcosFisicos; i++) Marcos.Add(new CeldaMarco { Numero = i });

        _reloj.Tick += (_, _) => Paso();
        AjustarVelocidad();

        Simular = new Comando(EjecutarSimulacion);
        Reproducir = new Comando(() => Reproduciendo = !Reproduciendo, () => _resultado is not null);
        Avanzar = new Comando(Paso, () => _resultado is not null);
        Retroceder = new Comando(() => Instante--, () => _resultado is not null);
        AlPrincipio = new Comando(() => Instante = 0, () => _resultado is not null);
        Comparar = new Comando(EjecutarComparacion);
    }

    // ---------- datos del escenario ----------
    public Escenario Escenario { get; }
    public ConfiguracionSO Config => Escenario.Config;

    public ObservableCollection<Programa> Programas { get; } = [];
    public ObservableCollection<Proceso> Procesos { get; } = [];
    public ObservableCollection<ReglaDeCola> Colas { get; } = [];

    public IReadOnlyList<string> AlgoritmosPlanificacion { get; } =
        FabricaPlanificadores.Disponibles.Select(a => a.Clave).ToList();
    public IReadOnlyList<string> AlgoritmosPaginacion { get; } =
        FabricaPaginacion.Disponibles.Select(a => a.Clave).ToList();
    public IReadOnlyList<string> AlgoritmosDeCola { get; } =
        ["RoundRobin", "Sjf", "Fifo", "Prioridad"];

    // ---------- resultado y reproducción ----------
    public ObservableCollection<FilaTarea> Tareas { get; } = [];
    public ObservableCollection<CeldaMarco> Marcos { get; } = [];
    public ObservableCollection<FilaPagina> TablaDePaginas { get; } = [];
    public ObservableCollection<FilaLog> Log { get; } = [];
    public ObservableCollection<FilaProceso> Informe { get; } = [];
    public ObservableCollection<FilaComparacion> Comparacion { get; } = [];

    public Comando Simular { get; }
    public Comando Reproducir { get; }
    public Comando Avanzar { get; }
    public Comando Retroceder { get; }
    public Comando AlPrincipio { get; }
    public Comando Comparar { get; }

    public int Pantalla { get => _pantalla; set => Asignar(ref _pantalla, value); }

    public string Mensaje { get => _mensaje; set => Asignar(ref _mensaje, value); }

    public int InstanteFinal => _resultado?.InstanteFinal ?? 0;
    public bool HaySimulacion => _resultado is not null;

    public int Instante
    {
        get => _instante;
        set
        {
            var acotado = Math.Clamp(value, 0, Math.Max(0, InstanteFinal - 1));
            if (!Asignar(ref _instante, acotado)) return;
            Notificar(nameof(PosicionTexto));
            Notificar(nameof(Progreso));
            Refrescar();
        }
    }

    public string PosicionTexto => $"t = {Instante} / {Math.Max(0, InstanteFinal - 1)}";
    public double Progreso => InstanteFinal <= 1 ? 0 : 100.0 * Instante / (InstanteFinal - 1);

    public bool Reproduciendo
    {
        get => _reproduciendo;
        set
        {
            if (!Asignar(ref _reproduciendo, value)) return;
            Notificar(nameof(TextoBotonReproducir));
            if (value) _reloj.Start(); else _reloj.Stop();
        }
    }

    public string TextoBotonReproducir => Reproduciendo ? "Pausar" : "Reproducir";

    public double Velocidad
    {
        get => _velocidad;
        set { if (Asignar(ref _velocidad, value)) AjustarVelocidad(); }
    }

    public Proceso? ProcesoObservado
    {
        get => _procesoObservado;
        set { if (Asignar(ref _procesoObservado, value)) RefrescarTablaDePaginas(); }
    }

    // ---------- indicadores de la pantalla de emulación ----------
    private string _enCpu = "—", _paginaRef = "—", _fallosTexto = "—", _rendimientoTexto = "—", _marcosTexto = "—";
    public string EnCpu { get => _enCpu; private set => Asignar(ref _enCpu, value); }
    public string PaginaRef { get => _paginaRef; private set => Asignar(ref _paginaRef, value); }
    public string FallosTexto { get => _fallosTexto; private set => Asignar(ref _fallosTexto, value); }
    public string RendimientoTexto { get => _rendimientoTexto; private set => Asignar(ref _rendimientoTexto, value); }
    public string MarcosTexto { get => _marcosTexto; private set => Asignar(ref _marcosTexto, value); }

    // ---------- acciones ----------
    private void AjustarVelocidad()
        => _reloj.Interval = TimeSpan.FromMilliseconds(Math.Max(40, 450 / Math.Max(0.25, _velocidad)));

    private void Paso()
    {
        if (_resultado is null) return;
        if (Instante >= InstanteFinal - 1) { Reproduciendo = false; return; }
        Instante++;
    }

    public void EjecutarSimulacion()
    {
        Reproduciendo = false;

        // El escenario vive en listas enlazadas; la interfaz trabaja con
        // colecciones observables. Acá se vuelcan los cambios de la pantalla
        // a las estructuras del núcleo antes de simular.
        Escenario.ListaDeEjecucion.Limpiar();
        foreach (var p in Procesos) Escenario.ListaDeEjecucion.AgregarAlFinal(p);
        Escenario.Config.Colas.Limpiar();
        foreach (var c in Colas) Escenario.Config.Colas.AgregarAlFinal(c);

        try
        {
            _resultado = Simulador.Ejecutar(Config, Escenario.ListaDeEjecucion);
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo simular: {ex.Message}";
            return;
        }

        ReconstruirMarcos();
        ArmarLog();
        ArmarTareas();
        ArmarInforme();

        ProcesoObservado ??= Procesos.FirstOrDefault();

        Notificar(nameof(InstanteFinal));
        Notificar(nameof(HaySimulacion));
        _instante = -1;
        Instante = 0;

        Mensaje = $"Simulación lista: {_resultado.NombreAlgoritmoPlanificacion} + {_resultado.NombreAlgoritmoPaginacion}. " +
                  $"{_resultado.Mmu.FallosDePagina} fallos sobre {_resultado.Mmu.ReferenciasTotales} referencias · " +
                  $"rendimiento {_resultado.Mmu.RendimientoPorcentual} %.";

        Reproducir.Revisar(); Avanzar.Revisar(); Retroceder.Revisar(); AlPrincipio.Revisar();
        Pantalla = 3;
    }

    private void EjecutarComparacion()
    {
        Escenario.ListaDeEjecucion.Limpiar();
        foreach (var p in Procesos) Escenario.ListaDeEjecucion.AgregarAlFinal(p);

        Comparacion.Clear();
        foreach (var fila in ComparadorAlgoritmos.CompararPaginacion(Config, Escenario.ListaDeEjecucion))
            Comparacion.Add(fila);

        // La comparación deja los procesos en el estado de la última corrida:
        // se vuelve a simular con el algoritmo elegido para dejar todo coherente.
        EjecutarSimulacion();
        Pantalla = 4;
    }

    private void ReconstruirMarcos()
    {
        Marcos.Clear();
        for (var i = 0; i < Config.MarcosFisicos; i++) Marcos.Add(new CeldaMarco { Numero = i });
    }

    private void ArmarTareas()
    {
        Tareas.Clear();
        foreach (var p in Procesos)
            Tareas.Add(new FilaTarea { Id = p.Id, Nombre = p.Nombre });
    }

    private void ArmarInforme()
    {
        Informe.Clear();
        if (_resultado is null) return;
        foreach (var p in _resultado.Procesos) Informe.Add(FilaProceso.De(p));
    }

    private void ArmarLog()
    {
        Log.Clear();
        if (_resultado is null) return;
        foreach (var e in _resultado.Eventos)
        {
            if (e.Observacion is null) continue;
            Log.Add(new FilaLog { Instante = e.Instante, Proceso = e.ProcesoNombre, Detalle = e.Observacion });
        }
    }

    /// <summary>Pinta el instante actual. No recalcula: lee lo que el motor dejó.</summary>
    private void Refrescar()
    {
        if (_resultado is null) return;

        // --- mapa de marcos ---
        FotoMemoria? foto = null;
        foreach (var f in _resultado.Memoria) if (f.Instante == Instante) { foto = f; break; }

        if (foto is not null)
        {
            for (var i = 0; i < Marcos.Count && i < foto.Marcos.Length; i++)
            {
                Marcos[i].Contenido = foto.Marcos[i].Contenido;
                Marcos[i].Bits = foto.Marcos[i].Bits;
                Marcos[i].EsVictima = foto.MarcoVictima == i;
            }
            MarcosTexto = $"{foto.EnUso}/{foto.Marcos.Length}";
        }

        // --- procesos y CPU ---
        EnCpu = "—"; PaginaRef = "—";
        var fallosHasta = 0; var referenciasHasta = 0;

        foreach (var e in _resultado.Eventos)
        {
            if (e.Instante > Instante) break;
            if (e.PaginaReferenciada is not null) referenciasHasta++;
            if (e.HuboFalloDePagina) fallosHasta++;

            if (e.Instante != Instante) continue;

            foreach (var fila in Tareas)
            {
                if (fila.Id != e.ProcesoId) continue;
                fila.Estado = e.Estado.ToString();
                fila.Cpu = e.Estado == EstadoProceso.Ejecutando ? 100 : 0;
            }

            if (e.Estado == EstadoProceso.Ejecutando && e.PaginaReferenciada is not null)
            {
                EnCpu = e.ProcesoNombre;
                PaginaRef = $"{e.PaginaReferenciada} → marco {e.MarcoAsignado}";
            }
        }

        // Marcos por proceso, contados sobre la foto.
        if (foto is not null)
        {
            foreach (var fila in Tareas)
            {
                var n = 0;
                foreach (var m in foto.Marcos) if (m.ProcesoId == fila.Id) n++;
                fila.Marcos = n;
                fila.MemoriaKb = n * Config.TamanioPaginaKb;
            }
        }

        var f2 = referenciasHasta == 0 ? 0 : (double)fallosHasta / referenciasHasta;
        FallosTexto = $"{fallosHasta} de {referenciasHasta}";
        RendimientoTexto = $"{Math.Round((1 - f2) * 100, 1)} %";

        RefrescarTablaDePaginas();
    }

    private void RefrescarTablaDePaginas()
    {
        TablaDePaginas.Clear();
        if (_resultado is null || ProcesoObservado is null) return;

        FotoMemoria? foto = null;
        foreach (var f in _resultado.Memoria) if (f.Instante == Instante) { foto = f; break; }
        if (foto is null) return;

        for (var pagina = 0; pagina < ProcesoObservado.Programa.TamanioEnPaginas; pagina++)
        {
            FotoMarco? residente = null;
            foreach (var m in foto.Marcos)
                if (m.ProcesoId == ProcesoObservado.Id && m.Pagina == pagina) { residente = m; break; }

            TablaDePaginas.Add(new FilaPagina
            {
                Pagina = pagina,
                Presente = residente is not null,
                Marco = residente?.Numero.ToString() ?? "—",
                BitR = residente?.BitR == true ? 1 : 0,
                BitM = residente?.BitM == true ? 1 : 0,
                Clase = (residente?.BitR == true ? 2 : 0) + (residente?.BitM == true ? 1 : 0),
                UltimoUso = residente is null ? "—" : $"t={Instante}",
            });
        }
    }
}
