using System.Text.Json;
using System.Text.Json.Serialization;
using SPQR.Core.Dominio;

namespace SPQR.Core.Persistencia;

// ---------- formato en disco ----------
// Son tipos aparte, a propósito. Si el archivo guardara las clases del dominio
// tal cual, cualquier refactor del núcleo rompería los archivos ya guardados.
// Estos registros son el CONTRATO del archivo y cambian solo cuando se decide
// cambiar el formato, no cuando se renombra una propiedad.

public sealed record ProgramaGuardado
{
    public string Id { get; init; } = "";
    public string Nombre { get; init; } = "";
    public int TamanioEnPaginas { get; init; }
    public string CadenaTexto { get; init; } = "";
    public int Prioridad { get; init; }
    public int Boletos { get; init; }
    public int PorcentajeAsignacion { get; init; }
}

public sealed record ProcesoGuardado
{
    public int Id { get; init; }
    public string ProgramaId { get; init; } = "";
    public int Rafaga { get; init; }
    public int TiempoLlegada { get; init; }
    public int Cola { get; init; }
}

public sealed record ColaGuardada
{
    public int Numero { get; init; }
    public string Etiqueta { get; init; } = "";
    public string Algoritmo { get; init; } = "";
    public int Quantum { get; init; }
}

public sealed record ConfiguracionGuardada
{
    public int Quantum { get; init; }
    public int MemoriaRamKb { get; init; }
    public int PaginasVirtuales { get; init; }
    public int MarcosFisicos { get; init; }
    public int TamanioPaginaKb { get; init; }
    public int DiscosDuros { get; init; }
    public int AreaDeIntercambio { get; init; }
    public int IntervaloReinicioBitR { get; init; }
    public int SemillaSorteo { get; init; }
    public string AlgoritmoPlanificacion { get; init; } = "";
    public string AlgoritmoPaginacion { get; init; } = "";
}

public sealed record EscenarioGuardado
{
    /// <summary>Versión del formato del archivo, para poder migrarlo más adelante.</summary>
    public int Version { get; init; } = 1;
    public string Descripcion { get; init; } = "Escenario de SPQR Task Manager";
    public DateTime GuardadoEl { get; init; } = DateTime.Now;

    public List<ProgramaGuardado> Catalogo { get; init; } = [];
    public List<ProcesoGuardado> ListaDeEjecucion { get; init; } = [];
    public List<ColaGuardada> Colas { get; init; } = [];
    public ConfiguracionGuardada Config { get; init; } = new();
}

/// <summary>
/// Exportar e importar escenarios como JSON.
///
/// NO hay base de datos, y es a propósito: lo que hay que persistir es una
/// configuración chica que se abre entera o no se abre, escrita por una sola
/// persona a la vez. Un archivo de texto hace eso mejor que un motor de base
/// de datos, se puede versionar con el resto del proyecto, se lee a ojo y no
/// agrega ninguna dependencia al entregable.
///
/// El formato es JSON con la extensión <c>.spqr</c>. Se guarda la CONFIGURACIÓN,
/// no el resultado: los eventos, las métricas y el informe se vuelven a generar
/// al simular, así que guardarlos sería guardar algo que ya se sabe calcular.
/// </summary>
public static class AlmacenJson
{
    public const string Extension = ".json";
    public const string FiltroDeArchivo = "JSON (*.json)|*.json|Todos|*.*";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Guardar(Escenario escenario, string ruta)
    {
        var dto = new EscenarioGuardado
        {
            Catalogo = [.. Volcar(escenario)],
            ListaDeEjecucion = [.. VolcarProcesos(escenario)],
            Colas = [.. VolcarColas(escenario)],
            Config = new ConfiguracionGuardada
            {
                Quantum = escenario.Config.Quantum,
                MemoriaRamKb = escenario.Config.MemoriaRamKb,
                PaginasVirtuales = escenario.Config.PaginasVirtuales,
                MarcosFisicos = escenario.Config.MarcosFisicos,
                TamanioPaginaKb = escenario.Config.TamanioPaginaKb,
                DiscosDuros = escenario.Config.DiscosDuros,
                AreaDeIntercambio = escenario.Config.AreaDeIntercambio,
                IntervaloReinicioBitR = escenario.Config.IntervaloReinicioBitR,
                SemillaSorteo = escenario.Config.SemillaSorteo,
                AlgoritmoPlanificacion = escenario.Config.AlgoritmoPlanificacion,
                AlgoritmoPaginacion = escenario.Config.AlgoritmoPaginacion,
            },
        };

        File.WriteAllText(ruta, JsonSerializer.Serialize(dto, Opciones), System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Lee un escenario del disco. Si el archivo está corrupto o es de otro
    /// formato, lanza <see cref="InvalidDataException"/> con un mensaje que se
    /// le puede mostrar al usuario tal cual.
    /// </summary>
    public static Escenario Abrir(string ruta)
    {
        EscenarioGuardado? dto;
        try
        {
            dto = JsonSerializer.Deserialize<EscenarioGuardado>(File.ReadAllText(ruta), Opciones);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"El archivo no tiene el formato de un escenario de SPQR. Detalle: {ex.Message}");
        }

        if (dto is null)
            throw new InvalidDataException("El archivo está vacío.");

        if (dto.Version > 1)
            throw new InvalidDataException(
                $"El archivo fue guardado con la versión {dto.Version} del formato y esta " +
                "versión del emulador solo entiende la 1.");

        var escenario = new Escenario();

        foreach (var g in dto.Catalogo)
        {
            var p = new Programa
            {
                Id = string.IsNullOrWhiteSpace(g.Id) ? escenario.SiguienteIdDePrograma() : g.Id,
                Nombre = g.Nombre,
                TamanioEnPaginas = g.TamanioEnPaginas,
                Prioridad = g.Prioridad,
                Boletos = g.Boletos,
                PorcentajeAsignacion = g.PorcentajeAsignacion,
            };
            p.CadenaTexto = g.CadenaTexto;
            escenario.Catalogo.AgregarAlFinal(p);
        }

        foreach (var g in dto.ListaDeEjecucion)
        {
            var programa = escenario.BuscarPrograma(g.ProgramaId);
            if (programa is null) continue;   // instancia huérfana: se descarta

            var proceso = new Proceso
            {
                Id = g.Id > 0 ? g.Id : escenario.SiguienteIdDeProceso(),
                Programa = programa,
                Rafaga = g.Rafaga,
                TiempoLlegada = g.TiempoLlegada,
                Cola = g.Cola,
            };
            proceso.Reiniciar();
            escenario.ListaDeEjecucion.AgregarAlFinal(proceso);
        }

        foreach (var g in dto.Colas)
            escenario.Config.Colas.AgregarAlFinal(new ReglaDeCola
            {
                Numero = g.Numero,
                Etiqueta = g.Etiqueta,
                Algoritmo = g.Algoritmo,
                Quantum = g.Quantum,
            });

        if (escenario.Config.Colas.EstaVacia) escenario.Config.ColasPorDefecto();

        var c = dto.Config;
        escenario.Config.Quantum = c.Quantum;
        escenario.Config.MemoriaRamKb = c.MemoriaRamKb;
        escenario.Config.PaginasVirtuales = c.PaginasVirtuales;
        escenario.Config.MarcosFisicos = c.MarcosFisicos;
        escenario.Config.TamanioPaginaKb = c.TamanioPaginaKb;
        escenario.Config.DiscosDuros = c.DiscosDuros;
        escenario.Config.AreaDeIntercambio = c.AreaDeIntercambio;
        escenario.Config.IntervaloReinicioBitR = c.IntervaloReinicioBitR;
        escenario.Config.SemillaSorteo = c.SemillaSorteo;
        if (!string.IsNullOrWhiteSpace(c.AlgoritmoPlanificacion))
            escenario.Config.AlgoritmoPlanificacion = c.AlgoritmoPlanificacion;
        if (!string.IsNullOrWhiteSpace(c.AlgoritmoPaginacion))
            escenario.Config.AlgoritmoPaginacion = c.AlgoritmoPaginacion;

        return escenario;
    }

    private static IEnumerable<ProgramaGuardado> Volcar(Escenario e)
    {
        foreach (var p in e.Catalogo)
            yield return new ProgramaGuardado
            {
                Id = p.Id,
                Nombre = p.Nombre,
                TamanioEnPaginas = p.TamanioEnPaginas,
                CadenaTexto = p.CadenaTexto,
                Prioridad = p.Prioridad,
                Boletos = p.Boletos,
                PorcentajeAsignacion = p.PorcentajeAsignacion,
            };
    }

    private static IEnumerable<ProcesoGuardado> VolcarProcesos(Escenario e)
    {
        foreach (var p in e.ListaDeEjecucion)
            yield return new ProcesoGuardado
            {
                Id = p.Id,
                ProgramaId = p.Programa.Id,
                Rafaga = p.Rafaga,
                TiempoLlegada = p.TiempoLlegada,
                Cola = p.Cola,
            };
    }

    private static IEnumerable<ColaGuardada> VolcarColas(Escenario e)
    {
        foreach (var c in e.Config.Colas)
            yield return new ColaGuardada
            {
                Numero = c.Numero,
                Etiqueta = c.Etiqueta,
                Algoritmo = c.Algoritmo,
                Quantum = c.Quantum,
            };
    }
}
