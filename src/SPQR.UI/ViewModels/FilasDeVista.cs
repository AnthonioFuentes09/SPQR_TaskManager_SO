using SPQR.Core.Dominio;

namespace SPQR.UI.ViewModels;

/// <summary>Una fila del administrador de tareas (pantalla 4).</summary>
public sealed class FilaTarea : BaseViewModel
{
    private string _estado = "";
    private int _cpu, _marcos, _memoriaKb;

    public required int Id { get; init; }
    public required string Nombre { get; init; }

    public string Estado { get => _estado; set => Asignar(ref _estado, value); }
    public int Cpu { get => _cpu; set => Asignar(ref _cpu, value); }
    public int Marcos { get => _marcos; set { if (Asignar(ref _marcos, value)) Notificar(nameof(MarcosTexto)); } }
    public int MemoriaKb { get => _memoriaKb; set => Asignar(ref _memoriaKb, value); }

    public string MarcosTexto => _marcos == 0 ? "—" : _marcos.ToString();
}

/// <summary>Un marco físico, tal como se dibuja en el mapa de memoria.</summary>
public sealed class CeldaMarco : BaseViewModel
{
    private string _contenido = "libre", _bits = "—";
    private bool _esVictima;

    public required int Numero { get; init; }
    public string Etiqueta => $"marco {Numero}";
    public string Contenido { get => _contenido; set => Asignar(ref _contenido, value); }
    public string Bits { get => _bits; set => Asignar(ref _bits, value); }
    public bool EsVictima { get => _esVictima; set => Asignar(ref _esVictima, value); }
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
