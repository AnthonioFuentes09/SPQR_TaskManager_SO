using System.Windows.Media;
using SPQR.Core.Dominio;

namespace SPQR.UI.ViewModels;

/// <summary>
/// Los colores de la emulación, en un solo lugar.
///
/// La regla que hace legible la pantalla: un proceso tiene SIEMPRE el mismo
/// color, en el mapa de marcos, en la línea de tiempo y en la lista de
/// tareas. Así se ve de un vistazo qué marcos son de quién sin leer una sola
/// etiqueta, que es justamente lo que se pierde si todos los marcos son
/// blancos.
/// </summary>
public static class Paleta
{
    private static readonly (string Fondo, string Borde, string Tinta)[] PorProceso =
    [
        ("#DCF0E3", "#9AD4AE", "#2F8F52"),   // verde
        ("#DDE7F7", "#A9C2E8", "#2F5590"),   // azul
        ("#E8E3F5", "#B8ADDE", "#5B4B9E"),   // violeta
        ("#D9EEF3", "#9FCEDB", "#0F6F84"),   // cian
        ("#FBE8D3", "#E6BE92", "#A85C10"),   // naranja
        ("#FBF0C4", "#ECD98A", "#A07A05"),   // ámbar
    ];

    public static readonly Brush Libre     = Pincel("#F6F8FA");
    public static readonly Brush LibreBorde = Pincel("#D7DEE6");
    public static readonly Brush LibreTinta = Pincel("#8896A4");

    public static readonly Brush VictimaFondo = Pincel("#F8D8DC");
    public static readonly Brush VictimaBorde = Pincel("#B32B3C");
    public static readonly Brush VictimaTinta = Pincel("#B32B3C");

    public static readonly Brush Navy  = Pincel("#1F3864");
    public static readonly Brush Ink2  = Pincel("#5A6875");
    public static readonly Brush Ink3  = Pincel("#8896A4");
    public static readonly Brush Ok    = Pincel("#2F8F52");
    public static readonly Brush OkBg  = Pincel("#DCF0E3");
    public static readonly Brush Warn  = Pincel("#A07A05");
    public static readonly Brush WarnBg = Pincel("#FBF0C4");
    public static readonly Brush Bad   = Pincel("#B32B3C");
    public static readonly Brush BadBg = Pincel("#F8D8DC");
    public static readonly Brush Vacio = Brushes.Transparent;

    private static Brush Pincel(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();                      // congelado: se comparte entre hilos y no se recrea
        return b;
    }

    public static Brush FondoDe(int procesoId) => Pincel(PorProceso[Indice(procesoId)].Fondo);
    public static Brush BordeDe(int procesoId) => Pincel(PorProceso[Indice(procesoId)].Borde);
    public static Brush TintaDe(int procesoId) => Pincel(PorProceso[Indice(procesoId)].Tinta);

    private static int Indice(int procesoId) => Math.Abs(procesoId - 1) % PorProceso.Length;

    /// <summary>Colores de la celda de la línea de tiempo según la letra del estado.</summary>
    public static (Brush Fondo, Brush Tinta) DeLaLetra(string letra) => letra switch
    {
        "E" => (OkBg, Ok),
        "B" => (WarnBg, Warn),
        "F" => (BadBg, Bad),
        _   => (Vacio, Ink3),
    };

    public static Brush DelEstado(string estado) => estado switch
    {
        nameof(EstadoProceso.Ejecutando) => Ok,
        nameof(EstadoProceso.Bloqueado)  => Warn,
        nameof(EstadoProceso.Finalizado) => Bad,
        nameof(EstadoProceso.Listo)      => Ink2,
        _ => Ink3,
    };
}

/// <summary>Una fila del administrador de tareas (pantalla 4).</summary>
public sealed class FilaTarea : BaseViewModel
{
    private string _estado = "";
    private int _cpu, _marcos, _memoriaKb;
    private double _uso;

    public required int Id { get; init; }
    public required string Programa { get; init; }
    public required string Nombre { get; init; }

    /// <summary>El color propio de este proceso, el mismo que en el mapa de marcos.</summary>
    public Brush Color => Paleta.TintaDe(Id);

    public string Etiqueta => $"#{Id}";

    public string Estado
    {
        get => _estado;
        set { if (Asignar(ref _estado, value)) Notificar(nameof(TintaEstado)); }
    }

    public Brush TintaEstado => Paleta.DelEstado(_estado);

    public int Cpu { get => _cpu; set => Asignar(ref _cpu, value); }

    public int Marcos
    {
        get => _marcos;
        set { if (Asignar(ref _marcos, value)) Notificar(nameof(MarcosTexto)); }
    }

    public int MemoriaKb { get => _memoriaKb; set => Asignar(ref _memoriaKb, value); }

    /// <summary>Porcentaje de la memoria física que ocupa este proceso, para la barra.</summary>
    public double Uso { get => _uso; set => Asignar(ref _uso, value); }

    public string MarcosTexto => _marcos == 0 ? "—" : _marcos.ToString();
}

/// <summary>Un marco físico, tal como se dibuja en el mapa de memoria.</summary>
public sealed class CeldaMarco : BaseViewModel
{
    private string _contenido = "libre", _bits = "—";
    private bool _esVictima;
    private int _procesoId;

    public required int Numero { get; init; }
    public string Etiqueta => $"marco {Numero}";

    public string Contenido { get => _contenido; set => Asignar(ref _contenido, value); }
    public string Bits { get => _bits; set => Asignar(ref _bits, value); }

    /// <summary>0 = libre. Determina el color del marco.</summary>
    public int ProcesoId
    {
        get => _procesoId;
        set { if (Asignar(ref _procesoId, value)) Repintar(); }
    }

    public bool EsVictima
    {
        get => _esVictima;
        set { if (Asignar(ref _esVictima, value)) Repintar(); }
    }

    public Brush Fondo => _esVictima ? Paleta.VictimaFondo
                        : _procesoId == 0 ? Paleta.Libre
                        : Paleta.FondoDe(_procesoId);

    public Brush Borde => _esVictima ? Paleta.VictimaBorde
                        : _procesoId == 0 ? Paleta.LibreBorde
                        : Paleta.BordeDe(_procesoId);

    public Brush Tinta => _esVictima ? Paleta.VictimaTinta
                        : _procesoId == 0 ? Paleta.LibreTinta
                        : Paleta.TintaDe(_procesoId);

    private void Repintar()
    {
        Notificar(nameof(Fondo));
        Notificar(nameof(Borde));
        Notificar(nameof(Tinta));
    }
}

/// <summary>Una celda de la línea de tiempo: un proceso en un instante.</summary>
public sealed class CeldaTiempo : BaseViewModel
{
    private bool _esActual;

    public required int Instante { get; init; }
    public required string Letra { get; init; }

    public Brush Fondo => Paleta.DeLaLetra(Letra).Fondo;
    public Brush Tinta => Paleta.DeLaLetra(Letra).Tinta;

    /// <summary>Marca la columna del instante que se está reproduciendo.</summary>
    public bool EsActual { get => _esActual; set => Asignar(ref _esActual, value); }
}

/// <summary>Una fila de la línea de tiempo: la vida entera de un proceso.</summary>
public sealed class FilaTiempo
{
    public required int ProcesoId { get; init; }
    public required string Proceso { get; init; }
    public List<CeldaTiempo> Celdas { get; } = [];
    public Brush Color => Paleta.TintaDe(ProcesoId);
}

/// <summary>Una fila de la tabla de páginas del proceso observado.</summary>
public sealed class FilaPagina
{
    public required int Pagina { get; init; }
    public required bool Presente { get; init; }
    public required string Marco { get; init; }
    public required int BitR { get; init; }
    public required int BitM { get; init; }
    public required int Clase { get; init; }
    public required string UltimoUso { get; init; }

    public string PresenteTexto => Presente ? "Sí" : "No";
    public Brush TintaPresente => Presente ? Paleta.Ok : Paleta.Bad;
}

/// <summary>Una fila del informe del planificador.</summary>
public sealed class FilaProceso
{
    public required string Proceso { get; init; }
    public required int Rafaga { get; init; }
    public required int Llegada { get; init; }
    public required string Fin { get; init; }
    public required string Retorno { get; init; }
    public required string Espera { get; init; }
    public required string Respuesta { get; init; }

    public static FilaProceso De(Proceso p) => new()
    {
        Proceso = p.Nombre,
        Rafaga = p.Rafaga,
        Llegada = p.TiempoLlegada,
        Fin = p.InstanteFinalizacion?.ToString() ?? "—",
        Retorno = p.TiempoRetorno?.ToString() ?? "—",
        Espera = p.TiempoEspera?.ToString() ?? "—",
        Respuesta = p.TiempoRespuesta?.ToString() ?? "—",
    };
}

/// <summary>Una línea del log de eventos.</summary>
public sealed class FilaLog
{
    public required int Instante { get; init; }
    public required string Proceso { get; init; }
    public required string Detalle { get; init; }
}
