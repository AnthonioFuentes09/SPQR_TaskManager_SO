using System.Globalization;
using System.Reflection;
using Microsoft.Data.Sqlite;
using SPQR.Core.Dominio;
using SPQR.Core.Persistencia;
using SPQR.Core.Simulacion;

namespace SPQR.Persistence;

/// <summary>
/// Guarda el trabajo en un archivo SQLite. Es la ÚNICA clase del proyecto
/// que escribe SQL.
///
/// Mismo criterio que el SqliteVolumeStore del proyecto anterior: un archivo
/// .db es un banco de trabajo completo y autocontenido. Copiarlo es copiar el
/// trabajo, entregarlo es adjuntar un archivo, y abrir otro es abrir otro
/// archivo. El programa no toca nada más de la computadora.
///
/// El esquema está en docs/esquema-base-datos.sql y viaja embebido en el
/// ensamblado. Al abrir un archivo que no existe, se crea con ese esquema.
/// </summary>
public sealed class SqliteEscenarioStore : IAlmacen
{
    public const int VersionDeEsquema = 1;

    public string Extension => ".db";
    public string FiltroDeArchivo => "Base de datos SPQR (*.db)|*.db|SQLite (*.sqlite)|*.sqlite|Todos|*.*";

    // =================================================================
    //  Conexión y esquema
    // =================================================================

    private static SqliteConnection Conectar(string ruta, bool crearSiNoExiste)
    {
        var existia = File.Exists(ruta);

        if (!existia && !crearSiNoExiste)
            throw new FileNotFoundException($"No existe el archivo {ruta}.", ruta);

        var cadena = new SqliteConnectionStringBuilder
        {
            DataSource = ruta,
            Mode = crearSiNoExiste ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            ForeignKeys = true,          // sin esto SQLite ignora las claves foráneas
        }.ToString();

        var cx = new SqliteConnection(cadena);
        cx.Open();

        if (!existia) AplicarEsquema(cx);
        else VerificarVersion(cx, ruta);

        return cx;
    }

    private static void AplicarEsquema(SqliteConnection cx)
    {
        using var comando = cx.CreateCommand();
        comando.CommandText = LeerEsquemaEmbebido();
        comando.ExecuteNonQuery();
    }

    private static void VerificarVersion(SqliteConnection cx, string ruta)
    {
        using var comando = cx.CreateCommand();
        comando.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(comando.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);

        if (version == 0)
            throw new InvalidDataException(
                $"El archivo «{Path.GetFileName(ruta)}» es una base SQLite, pero no es una base de " +
                "SPQR Task Manager: no tiene versión de esquema.");

        if (version > VersionDeEsquema)
            throw new InvalidDataException(
                $"El archivo se guardó con la versión {version} del esquema y esta versión del " +
                $"emulador solo entiende la {VersionDeEsquema}. Actualizá el programa.");
    }

    private static string LeerEsquemaEmbebido()
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        using var flujo = ensamblado.GetManifestResourceStream("SPQR.Persistence.esquema-base-datos.sql")
            ?? throw new InvalidOperationException(
                "No se encontró el esquema embebido. Revisá el EmbeddedResource de SPQR.Persistence.csproj.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }

    // =================================================================
    //  Guardar el escenario
    // =================================================================

    public void GuardarEscenario(Escenario escenario, string ruta)
    {
        using var cx = Conectar(ruta, crearSiNoExiste: true);
        using var transaccion = cx.BeginTransaction();

        // Todo el escenario se reemplaza de una: es una foto, no un diff.
        // El historial de corridas NO se toca — esa es la parte que se
        // acumula, y borrarla al guardar sería tirar lo único que una base
        // de datos aporta frente a un archivo de configuración.
        //
        // El orden importa por las claves foráneas: primero lo que depende.
        Ejecutar(cx, transaccion, "DELETE FROM Proceso;");
        Ejecutar(cx, transaccion, "DELETE FROM Referencia;");
        Ejecutar(cx, transaccion, "DELETE FROM Programa;");
        Ejecutar(cx, transaccion, "DELETE FROM ReglaDeCola;");
        Ejecutar(cx, transaccion, "DELETE FROM Configuracion;");

        // ---- configuración ----
        using (var c = cx.CreateCommand())
        {
            c.Transaction = transaccion;
            c.CommandText = """
                INSERT INTO Configuracion
                    (Id, Nombre, Quantum, MemoriaRamKb, PaginasVirtuales, MarcosFisicos,
                     TamanioPaginaKb, DiscosDuros, AreaDeIntercambio, IntervaloReinicioBitR,
                     SemillaSorteo, AlgoritmoPlanificacion, AlgoritmoPaginacion, GuardadoEl)
                VALUES
                    (1, $nombre, $quantum, $ram, $paginas, $marcos,
                     $tamPagina, $discos, $swap, $bitR,
                     $semilla, $planificacion, $paginacion, $guardadoEl);
                """;
            var cfg = escenario.Config;
            c.Parameters.AddWithValue("$nombre", Path.GetFileNameWithoutExtension(ruta));
            c.Parameters.AddWithValue("$quantum", cfg.Quantum);
            c.Parameters.AddWithValue("$ram", cfg.MemoriaRamKb);
            c.Parameters.AddWithValue("$paginas", cfg.PaginasVirtuales);
            c.Parameters.AddWithValue("$marcos", cfg.MarcosFisicos);
            c.Parameters.AddWithValue("$tamPagina", cfg.TamanioPaginaKb);
            c.Parameters.AddWithValue("$discos", cfg.DiscosDuros);
            c.Parameters.AddWithValue("$swap", cfg.AreaDeIntercambio);
            c.Parameters.AddWithValue("$bitR", cfg.IntervaloReinicioBitR);
            c.Parameters.AddWithValue("$semilla", cfg.SemillaSorteo);
            c.Parameters.AddWithValue("$planificacion", cfg.AlgoritmoPlanificacion);
            c.Parameters.AddWithValue("$paginacion", cfg.AlgoritmoPaginacion);
            c.Parameters.AddWithValue("$guardadoEl", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            c.ExecuteNonQuery();
        }

        // ---- catálogo y su cadena de referencias ----
        foreach (var p in escenario.Catalogo)
        {
            using (var c = cx.CreateCommand())
            {
                c.Transaction = transaccion;
                c.CommandText = """
                    INSERT INTO Programa (Id, Nombre, TamanioEnPaginas, Prioridad, Boletos, PorcentajeAsignacion)
                    VALUES ($id, $nombre, $paginas, $prioridad, $boletos, $porcentaje);
                    """;
                c.Parameters.AddWithValue("$id", p.Id);
                c.Parameters.AddWithValue("$nombre", p.Nombre);
                c.Parameters.AddWithValue("$paginas", p.TamanioEnPaginas);
                c.Parameters.AddWithValue("$prioridad", p.Prioridad);
                c.Parameters.AddWithValue("$boletos", p.Boletos);
                c.Parameters.AddWithValue("$porcentaje", p.PorcentajeAsignacion);
                c.ExecuteNonQuery();
            }

            var orden = 0;
            foreach (var r in p.CadenaDeReferencias)
            {
                using var c = cx.CreateCommand();
                c.Transaction = transaccion;
                c.CommandText = """
                    INSERT INTO Referencia (ProgramaId, Orden, Pagina, EsEscritura)
                    VALUES ($programa, $orden, $pagina, $escritura);
                    """;
                c.Parameters.AddWithValue("$programa", p.Id);
                c.Parameters.AddWithValue("$orden", orden++);
                c.Parameters.AddWithValue("$pagina", r.Pagina);
                c.Parameters.AddWithValue("$escritura", r.EsEscritura ? 1 : 0);
                c.ExecuteNonQuery();
            }
        }

        // ---- colas ----
        foreach (var regla in escenario.Config.Colas)
        {
            using var c = cx.CreateCommand();
            c.Transaction = transaccion;
            c.CommandText = """
                INSERT INTO ReglaDeCola (Numero, Etiqueta, Algoritmo, Quantum)
                VALUES ($numero, $etiqueta, $algoritmo, $quantum);
                """;
            c.Parameters.AddWithValue("$numero", regla.Numero);
            c.Parameters.AddWithValue("$etiqueta", regla.Etiqueta ?? "");
            c.Parameters.AddWithValue("$algoritmo", regla.Algoritmo);
            c.Parameters.AddWithValue("$quantum", regla.Quantum);
            c.ExecuteNonQuery();
        }

        // ---- lista de ejecución ----
        var posicion = 0;
        foreach (var proceso in escenario.ListaDeEjecucion)
        {
            using var c = cx.CreateCommand();
            c.Transaction = transaccion;
            c.CommandText = """
                INSERT INTO Proceso (Id, ProgramaId, Rafaga, TiempoLlegada, Cola, Orden)
                VALUES ($procesoId, $programa, $rafaga, $llegada, $cola, $orden);
                """;
            c.Parameters.AddWithValue("$procesoId", proceso.Id);
            c.Parameters.AddWithValue("$programa", proceso.Programa.Id);
            c.Parameters.AddWithValue("$rafaga", proceso.Rafaga);
            c.Parameters.AddWithValue("$llegada", proceso.TiempoLlegada);
            c.Parameters.AddWithValue("$cola", proceso.Cola);
            c.Parameters.AddWithValue("$orden", posicion++);
            c.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    // =================================================================
    //  Abrir el escenario
    // =================================================================

    public Escenario AbrirEscenario(string ruta)
    {
        using var cx = Conectar(ruta, crearSiNoExiste: false);
        var escenario = new Escenario();

        // ---- catálogo ----
        using (var c = cx.CreateCommand())
        {
            c.CommandText = """
                SELECT Id, Nombre, TamanioEnPaginas, Prioridad, Boletos, PorcentajeAsignacion
                FROM Programa ORDER BY Id;
                """;
            using var lector = c.ExecuteReader();
            while (lector.Read())
                escenario.Catalogo.AgregarAlFinal(new Programa
                {
                    Id = lector.GetString(0),
                    Nombre = lector.GetString(1),
                    TamanioEnPaginas = lector.GetInt32(2),
                    Prioridad = lector.GetInt32(3),
                    Boletos = lector.GetInt32(4),
                    PorcentajeAsignacion = lector.GetInt32(5),
                });
        }

        // ---- cadenas de referencias ----
        foreach (var programa in escenario.Catalogo)
        {
            var partes = new List<string>();
            using var c = cx.CreateCommand();
            c.CommandText = "SELECT Pagina, EsEscritura FROM Referencia WHERE ProgramaId = $programaId ORDER BY Orden;";
            c.Parameters.AddWithValue("$programaId", programa.Id);
            using var lector = c.ExecuteReader();
            while (lector.Read())
                partes.Add(lector.GetInt32(1) == 1 ? $"{lector.GetInt32(0)}w" : lector.GetInt32(0).ToString());

            programa.CadenaTexto = string.Join(", ", partes);
        }

        // ---- colas ----
        using (var c = cx.CreateCommand())
        {
            c.CommandText = "SELECT Numero, Etiqueta, Algoritmo, Quantum FROM ReglaDeCola ORDER BY Numero;";
            using var lector = c.ExecuteReader();
            while (lector.Read())
                escenario.Config.Colas.AgregarAlFinal(new ReglaDeCola
                {
                    Numero = lector.GetInt32(0),
                    Etiqueta = lector.GetString(1),
                    Algoritmo = lector.GetString(2),
                    Quantum = lector.GetInt32(3),
                });
        }
        if (escenario.Config.Colas.EstaVacia) escenario.Config.ColasPorDefecto();

        // ---- lista de ejecución ----
        using (var c = cx.CreateCommand())
        {
            c.CommandText = """
                SELECT Id, ProgramaId, Rafaga, TiempoLlegada, Cola
                FROM Proceso ORDER BY Orden, Id;
                """;
            using var lector = c.ExecuteReader();
            while (lector.Read())
            {
                var programa = escenario.BuscarPrograma(lector.GetString(1));
                if (programa is null) continue;   // no debería pasar: hay clave foránea

                var proceso = new Proceso
                {
                    Id = lector.GetInt32(0),
                    Programa = programa,
                    Rafaga = lector.GetInt32(2),
                    TiempoLlegada = lector.GetInt32(3),
                    Cola = lector.GetInt32(4),
                };
                proceso.Reiniciar();
                escenario.ListaDeEjecucion.AgregarAlFinal(proceso);
            }
        }

        // ---- configuración ----
        using (var c = cx.CreateCommand())
        {
            c.CommandText = """
                SELECT Quantum, MemoriaRamKb, PaginasVirtuales, MarcosFisicos, TamanioPaginaKb,
                       DiscosDuros, AreaDeIntercambio, IntervaloReinicioBitR, SemillaSorteo,
                       AlgoritmoPlanificacion, AlgoritmoPaginacion
                FROM Configuracion WHERE Id = 1;
                """;
            using var lector = c.ExecuteReader();
            if (lector.Read())
            {
                var cfg = escenario.Config;
                cfg.Quantum = lector.GetInt32(0);
                cfg.MemoriaRamKb = lector.GetInt32(1);
                cfg.PaginasVirtuales = lector.GetInt32(2);
                cfg.MarcosFisicos = lector.GetInt32(3);
                cfg.TamanioPaginaKb = lector.GetInt32(4);
                cfg.DiscosDuros = lector.GetInt32(5);
                cfg.AreaDeIntercambio = lector.GetInt32(6);
                cfg.IntervaloReinicioBitR = lector.GetInt32(7);
                cfg.SemillaSorteo = lector.GetInt32(8);
                cfg.AlgoritmoPlanificacion = lector.GetString(9);
                cfg.AlgoritmoPaginacion = lector.GetString(10);
            }
        }

        return escenario;
    }

    // =================================================================
    //  Historial de corridas
    // =================================================================

    public int RegistrarCorrida(string ruta, ResultadoSimulacion resultado, ConfiguracionSO config)
    {
        using var cx = Conectar(ruta, crearSiNoExiste: true);
        using var transaccion = cx.BeginTransaction();

        int id;
        using (var c = cx.CreateCommand())
        {
            c.Transaction = transaccion;
            c.CommandText = """
                INSERT INTO Corrida
                    (EjecutadaEl, AlgoritmoPlanificacion, AlgoritmoPaginacion, MarcosFisicos, Quantum,
                     InstanteFinal, ReferenciasTotales, FallosDePagina, Desalojos, EscriturasADisco,
                     CambiosDeContexto, RetornoPromedio, EsperaPromedio, RespuestaPromedio)
                VALUES
                    ($fecha, $planificacion, $paginacion, $marcos, $quantum,
                     $instanteFinal, $referencias, $fallos, $desalojos, $escrituras,
                     $contexto, $retorno, $espera, $respuesta);
                SELECT last_insert_rowid();
                """;
            c.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            c.Parameters.AddWithValue("$planificacion", resultado.NombreAlgoritmoPlanificacion);
            c.Parameters.AddWithValue("$paginacion", resultado.NombreAlgoritmoPaginacion);
            c.Parameters.AddWithValue("$marcos", config.MarcosFisicos);
            c.Parameters.AddWithValue("$quantum", config.Quantum);
            c.Parameters.AddWithValue("$instanteFinal", resultado.InstanteFinal);
            c.Parameters.AddWithValue("$referencias", resultado.Mmu.ReferenciasTotales);
            c.Parameters.AddWithValue("$fallos", resultado.Mmu.FallosDePagina);
            c.Parameters.AddWithValue("$desalojos", resultado.Mmu.Desalojos);
            c.Parameters.AddWithValue("$escrituras", resultado.Mmu.EscriturasADisco);
            c.Parameters.AddWithValue("$contexto", resultado.Planificador.CambiosDeContexto);
            c.Parameters.AddWithValue("$retorno", resultado.Planificador.RetornoPromedio);
            c.Parameters.AddWithValue("$espera", resultado.Planificador.EsperaPromedio);
            c.Parameters.AddWithValue("$respuesta", resultado.Planificador.RespuestaPromedio);

            id = Convert.ToInt32(c.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        // Solo los eventos con observación: son los que el docente pidió
        // poder mostrar. Guardar los 900 estados por tick de cada corrida
        // llenaría la base de filas que nadie va a leer.
        var orden = 0;
        foreach (var evento in resultado.Eventos)
        {
            if (evento.Observacion is null) continue;

            using var c = cx.CreateCommand();
            c.Transaction = transaccion;
            c.CommandText = """
                INSERT INTO EventoCorrida
                    (CorridaId, Orden, Instante, ProcesoId, ProcesoNombre, Estado,
                     PaginaReferenciada, MarcoAsignado, HuboFalloDePagina,
                     PaginaDesalojada, SeEscribioADisco, Observacion)
                VALUES
                    ($corrida, $orden, $instante, $proceso, $nombre, $estado,
                     $pagina, $marco, $fallo, $desalojada, $escribio, $observacion);
                """;
            c.Parameters.AddWithValue("$corrida", id);
            c.Parameters.AddWithValue("$orden", orden++);
            c.Parameters.AddWithValue("$instante", evento.Instante);
            c.Parameters.AddWithValue("$proceso", evento.ProcesoId);
            c.Parameters.AddWithValue("$nombre", evento.ProcesoNombre);
            c.Parameters.AddWithValue("$estado", evento.Estado.ToString());
            c.Parameters.AddWithValue("$pagina", (object?)evento.PaginaReferenciada ?? DBNull.Value);
            c.Parameters.AddWithValue("$marco", (object?)evento.MarcoAsignado ?? DBNull.Value);
            c.Parameters.AddWithValue("$fallo", evento.HuboFalloDePagina ? 1 : 0);
            c.Parameters.AddWithValue("$desalojada", (object?)evento.PaginaDesalojada ?? DBNull.Value);
            c.Parameters.AddWithValue("$escribio", evento.SeEscribioADisco ? 1 : 0);
            c.Parameters.AddWithValue("$observacion", evento.Observacion);
            c.ExecuteNonQuery();
        }

        transaccion.Commit();
        return id;
    }

    public List<ResumenDeCorrida> Historial(string ruta)
    {
        var historial = new List<ResumenDeCorrida>();
        if (!File.Exists(ruta)) return historial;

        using var cx = Conectar(ruta, crearSiNoExiste: false);
        using var c = cx.CreateCommand();

        // Se lee de la vista, no de la tabla: la fórmula del rendimiento
        // está declarada una sola vez, en el esquema.
        c.CommandText = """
            SELECT Id, EjecutadaEl, AlgoritmoPlanificacion, AlgoritmoPaginacion, MarcosFisicos,
                   ReferenciasTotales, FallosDePagina, Desalojos, EscriturasADisco
            FROM ResumenDeCorridas
            ORDER BY Id DESC;
            """;

        using var lector = c.ExecuteReader();
        while (lector.Read())
            historial.Add(new ResumenDeCorrida
            {
                Id = lector.GetInt32(0),
                EjecutadaEl = DateTime.TryParse(lector.GetString(1), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var fecha) ? fecha : DateTime.MinValue,
                AlgoritmoPlanificacion = lector.GetString(2),
                AlgoritmoPaginacion = lector.GetString(3),
                MarcosFisicos = lector.GetInt32(4),
                ReferenciasTotales = lector.GetInt32(5),
                FallosDePagina = lector.GetInt32(6),
                Desalojos = lector.GetInt32(7),
                EscriturasADisco = lector.GetInt32(8),
            });

        return historial;
    }

    public List<(int Instante, string Proceso, string Detalle)> BitacoraDe(string ruta, int corridaId)
    {
        var bitacora = new List<(int, string, string)>();
        if (!File.Exists(ruta)) return bitacora;

        using var cx = Conectar(ruta, crearSiNoExiste: false);
        using var c = cx.CreateCommand();
        c.CommandText = """
            SELECT Instante, ProcesoNombre, Observacion
            FROM EventoCorrida
            WHERE CorridaId = $corridaId AND Observacion IS NOT NULL
            ORDER BY Orden;
            """;
        c.Parameters.AddWithValue("$corridaId", corridaId);

        using var lector = c.ExecuteReader();
        while (lector.Read())
            bitacora.Add((lector.GetInt32(0), lector.GetString(1), lector.GetString(2)));

        return bitacora;
    }

    public void BorrarHistorial(string ruta)
    {
        if (!File.Exists(ruta)) return;
        using var cx = Conectar(ruta, crearSiNoExiste: false);
        // EventoCorrida cae solo: la clave foránea es en cascada.
        Ejecutar(cx, null, "DELETE FROM Corrida;");
        Ejecutar(cx, null, "VACUUM;");
    }

    // =================================================================

    private static void Ejecutar(SqliteConnection cx, SqliteTransaction? transaccion, string sql)
    {
        using var c = cx.CreateCommand();
        c.Transaction = transaccion;
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }
}
