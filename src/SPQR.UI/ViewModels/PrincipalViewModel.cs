using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using SPQR.Core.Dominio;
using SPQR.Core.Paginacion;
using SPQR.Core.Persistencia;
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
    /// <summary>
    /// Cómo se guarda el trabajo. La pantalla solo conoce la interfaz: quién
    /// la implementa —hoy SQLite— se decide una sola vez, al construir.
    /// Ninguna clase de SPQR.UI escribe SQL.
    /// </summary>
    private readonly IAlmacen _almacen;

    private readonly DispatcherTimer _reloj = new();
    private ResultadoSimulacion? _resultado;

    private Escenario _escenario = null!;
    private int _pantalla;
    private int _instante;
    private bool _reproduciendo;
    private double _velocidad = 1;
    private string _mensaje = "Escenario de ejemplo cargado. Revisá la configuración y presioná «Simular».";
    private string _archivoActual = "";
    private Proceso? _procesoObservado;
    private Programa? _programaSeleccionado;

    public PrincipalViewModel(IAlmacen almacen)
    {
        _almacen = almacen;
        _reloj.Tick += (_, _) => Paso();
        AjustarVelocidad();

        Simular = new Comando(SimularDesdeLaPantalla);
        Reproducir = new Comando(() => Reproduciendo = !Reproduciendo, () => _resultado is not null);
        Avanzar = new Comando(Paso, () => _resultado is not null);
        Retroceder = new Comando(() => Instante--, () => _resultado is not null);
        AlPrincipio = new Comando(() => Instante = 0, () => _resultado is not null);
        Comparar = new Comando(EjecutarComparacion);
        BorrarHistorial = new Comando(LimpiarHistorial, () => !string.IsNullOrWhiteSpace(ArchivoActual));

        // Todo valor derivado que se muestre —la suma de ráfagas, sobre todo—
        // depende tanto de qué filas hay como de qué dice cada fila. Hay que
        // escuchar las dos cosas: la colección y cada elemento.
        Procesos.CollectionChanged += AlCambiarLaLista;
        Colas.CollectionChanged += (_, _) => Notificar(nameof(NumerosDeCola));

        Cargar(Escenario.PorDefecto());
    }

    // ---------- datos del escenario ----------
    public Escenario Escenario
    {
        get => _escenario;
        private set => Asignar(ref _escenario, value);
    }

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

    /// <summary>Los números de cola que existen, para el desplegable de la columna COLA.</summary>
    public IReadOnlyList<int> NumerosDeCola => Colas.Select(c => c.Numero).ToList();

    /// <summary>Se actualiza al agregar, quitar o editar la ráfaga de un proceso.</summary>
    public int SumaDeRafagas
    {
        get { var s = 0; foreach (var p in Procesos) s += p.Rafaga; return s; }
    }

    public Programa? ProgramaSeleccionado
    {
        get => _programaSeleccionado;
        set => Asignar(ref _programaSeleccionado, value);
    }

    public string ArchivoActual
    {
        get => _archivoActual;
        private set { if (Asignar(ref _archivoActual, value)) Notificar(nameof(TituloDelEscenario)); }
    }

    public string TituloDelEscenario => string.IsNullOrWhiteSpace(ArchivoActual)
        ? "Escenario sin guardar"
        : Path.GetFileName(ArchivoActual);

    // ---------- resultado y reproducción ----------
    public ObservableCollection<FilaTarea> Tareas { get; } = [];
    public ObservableCollection<CeldaMarco> Marcos { get; } = [];
    public ObservableCollection<FilaPagina> TablaDePaginas { get; } = [];
    public ObservableCollection<FilaLog> Log { get; } = [];

    /// <summary>Una fila por proceso; cada fila, una celda por instante.</summary>
    public ObservableCollection<FilaTiempo> LineaDeTiempo { get; } = [];

    /// <summary>Los rótulos de la cabecera de la línea de tiempo: 0, 1, 2, …</summary>
    public ObservableCollection<int> Tiempos { get; } = [];

    // ---------- historial de paginación (la cuadrícula de la pizarra) ----------

    /// <summary>Encabezado: la referencia que se pidió en cada instante («A0», «B2», …).</summary>
    public ObservableCollection<CeldaReferencia> Referencias { get; } = [];

    /// <summary>Una fila por marco físico; cada fila, una celda por instante.</summary>
    public ObservableCollection<FilaHistorial> HistorialPaginacion { get; } = [];

    /// <summary>Pie: «x» si la referencia falló, «//» si acertó.</summary>
    public ObservableCollection<CeldaReferencia> Marcas { get; } = [];
    public ObservableCollection<FilaProceso> Informe { get; } = [];
    public ObservableCollection<FilaComparacion> Comparacion { get; } = [];

    public Comando Simular { get; }
    public Comando Reproducir { get; }
    public Comando Avanzar { get; }
    public Comando Retroceder { get; }
    public Comando AlPrincipio { get; }
    public Comando Comparar { get; }
    public Comando BorrarHistorial { get; }

    /// <summary>Las corridas guardadas en el archivo actual, de la más nueva a la más vieja.</summary>
    public ObservableCollection<ResumenDeCorrida> Historial { get; } = [];

    public string ExtensionDelAlmacen => _almacen.Extension;
    public string FiltroDelAlmacen => _almacen.FiltroDeArchivo;

    public int Pantalla { get => _pantalla; set => Asignar(ref _pantalla, value); }
    public string Mensaje { get => _mensaje; set => Asignar(ref _mensaje, value); }

    public int InstanteFinal => _resultado?.InstanteFinal ?? 0;

    /// <summary>
    /// El último instante que se puede mostrar. El deslizador se enlaza acá y
    /// no a <see cref="InstanteFinal"/>: si se enlazara al total, arrastrarlo
    /// hasta el extremo derecho saltaría siempre un tick para atrás.
    /// </summary>
    public int InstanteMaximo => Math.Max(0, InstanteFinal - 1);

    public bool HaySimulacion => _resultado is not null;

    public int Instante
    {
        get => _instante;
        set
        {
            var acotado = Math.Clamp(value, 0, InstanteMaximo);
            if (!Asignar(ref _instante, acotado)) return;
            Notificar(nameof(PosicionTexto));
            Notificar(nameof(Progreso));
            Refrescar();
        }
    }

    public string PosicionTexto => $"t = {Instante} / {InstanteMaximo}";
    public double Progreso => InstanteMaximo == 0 ? 0 : 100.0 * Instante / InstanteMaximo;

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

    // ---------- el resultado FINAL, que no depende del instante que se mire ----------
    //
    // Los indicadores de arriba son acumulados hasta el instante reproducido:
    // sirven para ver cómo se va formando el número, pero no son «el» número.
    // Estos tres son los de la corrida completa, que son los que se comparan
    // contra la tabla del pizarrón.

    private string _fallosFinal = "—", _rendimientoFinal = "—", _referenciasFinal = "—";
    public string FallosFinalTexto { get => _fallosFinal; private set => Asignar(ref _fallosFinal, value); }
    public string RendimientoFinalTexto { get => _rendimientoFinal; private set => Asignar(ref _rendimientoFinal, value); }
    public string ReferenciasFinalTexto { get => _referenciasFinal; private set => Asignar(ref _referenciasFinal, value); }

    // ---------- carga y guardado ----------

    /// <summary>Reemplaza el escenario entero y vuelve a llenar las colecciones de la pantalla.</summary>
    public void Cargar(Escenario escenario)
    {
        Reproduciendo = false;
        _resultado = null;

        foreach (var p in Procesos) p.PropertyChanged -= AlCambiarUnProceso;

        Escenario = escenario;

        Programas.Clear();
        foreach (var p in escenario.Catalogo) Programas.Add(p);

        Procesos.Clear();
        foreach (var p in escenario.ListaDeEjecucion) Procesos.Add(p);

        Colas.Clear();
        foreach (var c in escenario.Config.Colas) Colas.Add(c);

        ReconstruirMarcos();
        Tareas.Clear(); TablaDePaginas.Clear(); Log.Clear(); Informe.Clear(); Comparacion.Clear();
        LineaDeTiempo.Clear(); Tiempos.Clear();
        Referencias.Clear(); HistorialPaginacion.Clear(); Marcas.Clear();
        FallosFinalTexto = "—"; RendimientoFinalTexto = "—"; ReferenciasFinalTexto = "—";

        ProgramaSeleccionado = Programas.FirstOrDefault();
        ProcesoObservado = Procesos.FirstOrDefault();

        Notificar(nameof(Config));
        Notificar(nameof(SumaDeRafagas));
        Notificar(nameof(NumerosDeCola));
        Notificar(nameof(InstanteFinal));
        Notificar(nameof(InstanteMaximo));
        Notificar(nameof(HaySimulacion));

        Reproducir.Revisar(); Avanzar.Revisar(); Retroceder.Revisar(); AlPrincipio.Revisar();
    }

    public void GuardarEn(string ruta)
    {
        VolcarALasListasDelNucleo();
        _almacen.GuardarEscenario(Escenario, ruta);
        ArchivoActual = ruta;
        RefrescarHistorial();
        Mensaje = $"Escenario guardado en {ruta}";
    }

    public void AbrirDesde(string ruta)
    {
        Cargar(_almacen.AbrirEscenario(ruta));
        ArchivoActual = ruta;
        RefrescarHistorial();
        Mensaje = $"Escenario abierto desde {ruta}";
    }

    /// <summary>Exportar a JSON, para revisar el escenario a ojo o mandarlo por correo.</summary>
    public void ExportarJson(string ruta)
    {
        VolcarALasListasDelNucleo();
        AlmacenJson.Guardar(Escenario, ruta);
        Mensaje = $"Escenario exportado a {ruta}";
    }

    public void ImportarJson(string ruta)
    {
        Cargar(AlmacenJson.Abrir(ruta));
        ArchivoActual = "";
        Historial.Clear();
        Mensaje = $"Escenario importado de {ruta}. Guardalo como base para conservar el historial.";
    }

    public void RefrescarHistorial()
    {
        Historial.Clear();
        if (string.IsNullOrWhiteSpace(ArchivoActual)) return;

        try
        {
            foreach (var c in _almacen.Historial(ArchivoActual)) Historial.Add(c);
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo leer el historial: {ex.Message}";
        }
        BorrarHistorial.Revisar();
    }

    public List<(int Instante, string Proceso, string Detalle)> BitacoraDe(int corridaId)
        => string.IsNullOrWhiteSpace(ArchivoActual)
            ? []
            : _almacen.BitacoraDe(ArchivoActual, corridaId);

    private void LimpiarHistorial()
    {
        if (string.IsNullOrWhiteSpace(ArchivoActual)) return;
        _almacen.BorrarHistorial(ArchivoActual);
        RefrescarHistorial();
        Mensaje = "Historial de corridas borrado. El escenario quedó intacto.";
    }

    public void NuevoEscenarioDeEjemplo()
    {
        Cargar(Escenario.PorDefecto());
        ArchivoActual = "";
        Mensaje = "Escenario de ejemplo recargado.";
    }

    // ---------- acciones ----------
    private void AjustarVelocidad()
        => _reloj.Interval = TimeSpan.FromMilliseconds(Math.Max(40, 450 / Math.Max(0.25, _velocidad)));

    private void Paso()
    {
        if (_resultado is null) return;
        if (Instante >= InstanteMaximo) { Reproduciendo = false; return; }
        Instante++;
    }

    private void AlCambiarLaLista(object? _, NotifyCollectionChangedEventArgs e)
    {
        foreach (var viejo in e.OldItems?.OfType<Proceso>() ?? []) viejo.PropertyChanged -= AlCambiarUnProceso;
        foreach (var nuevo in e.NewItems?.OfType<Proceso>() ?? []) nuevo.PropertyChanged += AlCambiarUnProceso;
        Notificar(nameof(SumaDeRafagas));
    }

    private void AlCambiarUnProceso(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Proceso.Rafaga)) Notificar(nameof(SumaDeRafagas));
    }

    /// <summary>
    /// La pantalla trabaja con colecciones observables y el núcleo con listas
    /// enlazadas. Acá se vuelca lo primero sobre lo segundo.
    /// </summary>
    private void VolcarALasListasDelNucleo()
    {
        Escenario.Catalogo.Limpiar();
        foreach (var p in Programas) Escenario.Catalogo.AgregarAlFinal(p);

        Escenario.ListaDeEjecucion.Limpiar();
        foreach (var p in Procesos) Escenario.ListaDeEjecucion.AgregarAlFinal(p);

        Escenario.Config.Colas.Limpiar();
        foreach (var c in Colas) Escenario.Config.Colas.AgregarAlFinal(c);
    }

    /// <summary>
    /// Revisa el escenario y devuelve los problemas. Si hay errores no se
    /// simula: es preferible un mensaje que diga qué campo está mal a una
    /// excepción cruda en la barra de estado.
    /// </summary>
    public List<Problema> Revisar()
    {
        VolcarALasListasDelNucleo();
        return Validador.Revisar(Escenario);
    }

    /// <summary>
    /// Si hay problemas, esta función los devuelve y no simula; le toca a la
    /// pantalla decidir si los muestra y si insiste.
    /// </summary>
    public List<Problema> EjecutarSimulacion(bool ignorarAvisos = true)
    {
        Reproduciendo = false;

        var problemas = Revisar();
        if (Validador.HayErrores(problemas))
        {
            Mensaje = "No se simuló: hay errores en el escenario.";
            return problemas;
        }

        try
        {
            _resultado = Simulador.Ejecutar(Config, Escenario.ListaDeEjecucion);
        }
        catch (Exception ex)
        {
            Mensaje = $"No se pudo simular: {ex.Message}";
            return [new Problema(Severidad.Error, "Motor", ex.Message)];
        }

        ReconstruirMarcos();
        ArmarLog();
        ArmarTareas();
        ArmarInforme();
        ArmarLineaDeTiempo();
        ArmarHistorialDePaginacion();

        if (ProcesoObservado is null || !Procesos.Contains(ProcesoObservado))
            ProcesoObservado = Procesos.FirstOrDefault();

        Notificar(nameof(InstanteFinal));
        Notificar(nameof(InstanteMaximo));
        Notificar(nameof(HaySimulacion));
        _instante = -1;
        Instante = 0;

        Mensaje = $"Simulación lista: {_resultado.NombreAlgoritmoPlanificacion} + {_resultado.NombreAlgoritmoPaginacion}. " +
                  $"{_resultado.Mmu.FallosDePagina} fallos sobre {_resultado.Mmu.ReferenciasTotales} referencias · " +
                  $"rendimiento {_resultado.Mmu.RendimientoPorcentual} %.";

        // La corrida queda anotada en el archivo. Es lo que una base de datos
        // aporta frente a un archivo de configuración: poder abrir el .db la
        // semana que viene y comparar la corrida de hoy con la del lunes.
        if (!string.IsNullOrWhiteSpace(ArchivoActual))
        {
            try
            {
                _almacen.RegistrarCorrida(ArchivoActual, _resultado, Config);
                RefrescarHistorial();
            }
            catch (Exception ex)
            {
                Mensaje += $"  (no se pudo anotar la corrida: {ex.Message})";
            }
        }

        Reproducir.Revisar(); Avanzar.Revisar(); Retroceder.Revisar(); AlPrincipio.Revisar();
        Pantalla = 3;

        return ignorarAvisos ? [] : problemas;
    }

    private void SimularDesdeLaPantalla()
    {
        var problemas = EjecutarSimulacion(ignorarAvisos: false);
        if (problemas.Count > 0) AvisarSiHayProblemas?.Invoke(problemas);
    }

    /// <summary>La pantalla engancha acá para mostrar los problemas al usuario.</summary>
    public Action<List<Problema>>? AvisarSiHayProblemas { get; set; }

    private void EjecutarComparacion()
    {
        var problemas = Revisar();
        if (Validador.HayErrores(problemas))
        {
            Mensaje = "No se comparó: hay errores en el escenario.";
            AvisarSiHayProblemas?.Invoke(problemas);
            return;
        }

        Comparacion.Clear();
        foreach (var fila in ComparadorAlgoritmos.CompararPaginacion(Config, Escenario.ListaDeEjecucion))
            Comparacion.Add(fila);

        // La comparación deja los procesos en el estado de la última corrida:
        // se vuelve a simular con el algoritmo elegido para dejar todo coherente.
        EjecutarSimulacion(ignorarAvisos: true);

        // ...y recién entonces se salta al informe, porque EjecutarSimulacion
        // deja la pantalla en la de emulación. Sin esta línea la tabla de
        // comparación se arma y no se muestra.
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
            Tareas.Add(new FilaTarea
            {
                Id = p.Id,
                Programa = p.Programa.Id,
                Nombre = p.Programa.Nombre,
            });
    }

    /// <summary>
    /// Arma la cuadrícula de estados: una fila por proceso, una columna por
    /// instante. Se construye UNA vez al terminar de simular, leyendo los
    /// eventos que el motor ya dejó; reproducir después solo mueve el recuadro
    /// de la columna actual.
    /// </summary>
    private void ArmarLineaDeTiempo()
    {
        LineaDeTiempo.Clear();
        Tiempos.Clear();
        _columnaActual = -1;
        if (_resultado is null) return;

        var porProceso = new Dictionary<int, FilaTiempo>();
        var enOrden = new List<FilaTiempo>();

        foreach (var e in _resultado.Eventos)
        {
            if (!porProceso.TryGetValue(e.ProcesoId, out var fila))
            {
                var nombre = e.ProcesoNombre;
                var parentesis = nombre.IndexOf(" (", StringComparison.Ordinal);
                if (parentesis > 0) nombre = nombre[..parentesis];

                fila = new FilaTiempo { ProcesoId = e.ProcesoId, Proceso = $"{e.ProcesoId} · {nombre}" };
                porProceso[e.ProcesoId] = fila;
                enOrden.Add(fila);
            }

            var letra = e.Letra == ' ' ? "" : e.Letra.ToString();
            fila.Celdas.Add(new CeldaTiempo { Instante = e.Instante, Letra = letra });
        }

        // Las filas se publican RECIÉN cuando ya tienen todas sus celdas.
        // Celdas es una lista común, sin avisos de cambio: si la fila se
        // agregara primero y se llenara después, la pantalla dibujaría la fila
        // vacía y no se enteraría nunca de las celdas que llegaron luego.
        foreach (var fila in enOrden) LineaDeTiempo.Add(fila);

        for (var t = 0; t < InstanteFinal; t++) Tiempos.Add(t);
    }

    /// <summary>
    /// Arma el historial de paginación: la cuadrícula que el docente dibuja en
    /// el pizarrón. Una columna por referencia —no por proceso—, una fila por
    /// marco físico, y abajo la fila de «x» y «//».
    ///
    /// Es la comprobación a mano del rendimiento: contar las «x», dividir entre
    /// el total de columnas y restarle eso a uno. Si el número del recuadro no
    /// coincide con ese conteo, el error está a la vista y no escondido en el
    /// motor, que era justamente el problema que tuvimos.
    ///
    /// Igual que la línea de tiempo, se construye UNA sola vez al terminar de
    /// simular: reproducir después solo mueve el recuadro de la columna actual.
    /// </summary>
    private void ArmarHistorialDePaginacion()
    {
        Referencias.Clear();
        HistorialPaginacion.Clear();
        Marcas.Clear();
        _columnaHistorial = -1;
        if (_resultado is null) return;

        var filas = new List<FilaHistorial>();
        for (var i = 0; i < Config.MarcosFisicos; i++) filas.Add(new FilaHistorial { Marco = i });

        // Con un solo proceso —el caso de un ejercicio de clase, una cadena y
        // N marcos— la cuadrícula se rotula como en la pizarra: «1, 2, 3…», no
        // «A1, A2, A3…». Con varios procesos hace falta la letra para saber de
        // quién es cada página.
        var procesoUnico = -1;
        var variosProcesos = false;
        foreach (var foto in _resultado.Memoria)
        {
            if (!foto.HuboAcceso) continue;
            if (procesoUnico < 0) procesoUnico = foto.ProcesoIdEnCpu;
            else if (foto.ProcesoIdEnCpu != procesoUnico) { variosProcesos = true; break; }
        }

        var fallos = 0;
        var referencias = 0;

        foreach (var foto in _resultado.Memoria)
        {
            if (foto.HuboAcceso)
            {
                referencias++;
                if (foto.Fallo == true) fallos++;
            }

            Referencias.Add(new CeldaReferencia
            {
                Instante = foto.Instante,
                Texto = variosProcesos || !foto.HuboAcceso ? foto.Etiqueta : foto.PaginaReferenciada.ToString(),
                ProcesoId = foto.ProcesoIdEnCpu,
                Fallo = null,                      // el encabezado se pinta por proceso
            });

            Marcas.Add(new CeldaReferencia
            {
                Instante = foto.Instante,
                Texto = foto.Marca,
                ProcesoId = foto.ProcesoIdEnCpu,
                Fallo = foto.Fallo,
            });

            for (var i = 0; i < filas.Count; i++)
            {
                var m = i < foto.Marcos.Length ? foto.Marcos[i] : null;
                filas[i].Celdas.Add(new CeldaHistorial
                {
                    Instante = foto.Instante,
                    ProcesoId = m?.ProcesoId ?? 0,
                    Pagina = m?.Pagina ?? -1,
                    BitR = m?.BitR == true ? 1 : 0,
                    Texto = m is null || m.Libre ? ""
                          : variosProcesos ? $"{m.Proceso}{m.Pagina}"
                          : m.Pagina.ToString(),
                    RecienCargada = foto.Fallo == true && foto.MarcoReferenciado == i,
                });
            }
        }

        // Las filas se publican recién cuando ya tienen todas sus celdas: Celdas
        // es una lista común y no avisa de los cambios (ver ArmarLineaDeTiempo).
        foreach (var fila in filas) HistorialPaginacion.Add(fila);

        // Rendimiento = 1 − F, con F = fallos ÷ referencias. Se muestra la cuenta
        // entera, igual que en la pizarra: «F = 7/15 = 0.4667 → 53.33 %».
        var f = referencias == 0 ? 0 : (double)fallos / referencias;
        ReferenciasFinalTexto = referencias.ToString();
        FallosFinalTexto = $"F = {fallos}/{referencias} = {f:0.0000}";
        RendimientoFinalTexto = $"{(1 - f) * 100:0.00} %";
    }

    private int _columnaActual = -1;
    private int _columnaHistorial = -1;

    /// <summary>Mueve el recuadro del instante actual, sin redibujar la cuadrícula.</summary>
    private void MarcarColumna()
    {
        if (_columnaActual != Instante)
        {
            foreach (var fila in LineaDeTiempo)
            {
                if (_columnaActual >= 0 && _columnaActual < fila.Celdas.Count)
                    fila.Celdas[_columnaActual].EsActual = false;
                if (Instante < fila.Celdas.Count)
                    fila.Celdas[Instante].EsActual = true;
            }
            _columnaActual = Instante;
        }

        if (_columnaHistorial == Instante) return;

        Apagar(Referencias, _columnaHistorial); Encender(Referencias, Instante);
        Apagar(Marcas, _columnaHistorial);      Encender(Marcas, Instante);

        foreach (var fila in HistorialPaginacion)
        {
            if (_columnaHistorial >= 0 && _columnaHistorial < fila.Celdas.Count)
                fila.Celdas[_columnaHistorial].EsActual = false;
            if (Instante < fila.Celdas.Count)
                fila.Celdas[Instante].EsActual = true;
        }
        _columnaHistorial = Instante;

        static void Apagar(ObservableCollection<CeldaReferencia> fila, int i)
        {
            if (i >= 0 && i < fila.Count) fila[i].EsActual = false;
        }

        static void Encender(ObservableCollection<CeldaReferencia> fila, int i)
        {
            if (i >= 0 && i < fila.Count) fila[i].EsActual = true;
        }
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

        FotoMemoria? foto = null;
        foreach (var f in _resultado.Memoria) if (f.Instante == Instante) { foto = f; break; }

        if (foto is not null)
        {
            for (var i = 0; i < Marcos.Count && i < foto.Marcos.Length; i++)
            {
                Marcos[i].Contenido = foto.Marcos[i].Contenido;
                Marcos[i].Bits = foto.Marcos[i].Bits;
                Marcos[i].ProcesoId = foto.Marcos[i].ProcesoId;
                Marcos[i].EsVictima = foto.MarcoVictima == i;
            }
            MarcosTexto = $"{foto.EnUso}/{foto.Marcos.Length}";
        }

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

        if (foto is not null)
        {
            foreach (var fila in Tareas)
            {
                var n = 0;
                foreach (var m in foto.Marcos) if (m.ProcesoId == fila.Id) n++;
                fila.Marcos = n;
                fila.MemoriaKb = n * Config.TamanioPaginaKb;
                fila.Uso = foto.Marcos.Length == 0 ? 0 : 100.0 * n / foto.Marcos.Length;
            }
        }

        var f2 = referenciasHasta == 0 ? 0 : (double)fallosHasta / referenciasHasta;
        FallosTexto = $"{fallosHasta} de {referenciasHasta}";
        RendimientoTexto = $"{Math.Round((1 - f2) * 100, 1)} %";

        MarcarColumna();
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
