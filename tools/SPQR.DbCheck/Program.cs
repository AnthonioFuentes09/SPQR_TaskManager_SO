using SPQR.Core.Dominio;
using SPQR.Core.Simulacion;
using SPQR.Persistence;

// =====================================================================
//  SPQR.DbCheck — verifica la capa de persistencia sin abrir la ventana
//
//      dotnet run --project tools/SPQR.DbCheck
//
//  Crea una base, guarda el escenario de ejemplo, la cierra, la vuelve a
//  abrir y comprueba que todo volvió igual. Después corre una simulación,
//  la registra en el historial y verifica que los números guardados
//  coincidan con los que calculó el motor.
//
//  Es la forma más rápida de saber si algo se rompió, y el mejor lugar
//  para agregar pruebas nuevas.
// =====================================================================

var enVerde = 0;
var enRojo = 0;

void Verificar(string nombre, bool condicion, string? detalle = null)
{
    if (condicion)
    {
        enVerde++;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  ok    ");
    }
    else
    {
        enRojo++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  FALLA ");
    }
    Console.ResetColor();
    Console.WriteLine(detalle is null ? nombre : $"{nombre}  ({detalle})");
}

void Titulo(string texto)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"── {texto} " + new string('─', Math.Max(0, 58 - texto.Length)));
    Console.ResetColor();
}

var ruta = Path.Combine(AppContext.BaseDirectory, "escenario_prueba.db");
if (File.Exists(ruta)) File.Delete(ruta);

var almacen = new SqliteEscenarioStore();

Console.WriteLine("SPQR.DbCheck · verificación de la capa de persistencia");
Console.WriteLine($"Archivo: {ruta}");

// ---------------------------------------------------------------------
Titulo("1 · Crear y guardar");

var original = Escenario.PorDefecto();
almacen.GuardarEscenario(original, ruta);

Verificar("el archivo se creó", File.Exists(ruta));
Verificar("el archivo no está vacío", new FileInfo(ruta).Length > 0,
    $"{new FileInfo(ruta).Length} bytes");

// ---------------------------------------------------------------------
Titulo("2 · Cerrar, reabrir y comparar");

var leido = almacen.AbrirEscenario(ruta);

Verificar("misma cantidad de programas",
    leido.Catalogo.Cantidad == original.Catalogo.Cantidad,
    $"{leido.Catalogo.Cantidad}");

Verificar("misma cantidad de procesos",
    leido.ListaDeEjecucion.Cantidad == original.ListaDeEjecucion.Cantidad,
    $"{leido.ListaDeEjecucion.Cantidad}");

Verificar("misma cantidad de colas",
    leido.Config.Colas.Cantidad == original.Config.Colas.Cantidad,
    $"{leido.Config.Colas.Cantidad}");

var p1Original = original.BuscarPrograma("P1");
var p1Leido = leido.BuscarPrograma("P1");
Verificar("la cadena de referencias volvió en el mismo orden",
    p1Leido is not null && p1Original is not null && p1Leido.CadenaTexto == p1Original.CadenaTexto,
    p1Leido?.CadenaTexto);

Verificar("las escrituras siguen marcadas como escrituras",
    p1Leido is not null && ContarEscrituras(p1Leido) == ContarEscrituras(p1Original!),
    $"{ContarEscrituras(p1Leido!)} escrituras");

Verificar("la configuración volvió igual",
    leido.Config.MarcosFisicos == original.Config.MarcosFisicos &&
    leido.Config.Quantum == original.Config.Quantum &&
    leido.Config.AlgoritmoPaginacion == original.Config.AlgoritmoPaginacion,
    $"{leido.Config.MarcosFisicos} marcos, quantum {leido.Config.Quantum}, {leido.Config.AlgoritmoPaginacion}");

Verificar("la suma de ráfagas coincide",
    leido.SumaDeRafagas == original.SumaDeRafagas,
    $"{leido.SumaDeRafagas}");

Verificar("cada proceso apunta a su programa",
    TodosLosProcesosTienenPrograma(leido));

// ---------------------------------------------------------------------
Titulo("3 · El escenario leído simula igual que el original");

var corridaOriginal = Simulador.Ejecutar(original.Config, original.ListaDeEjecucion);
var corridaLeida = Simulador.Ejecutar(leido.Config, leido.ListaDeEjecucion);

Verificar("mismos fallos de página",
    corridaLeida.Mmu.FallosDePagina == corridaOriginal.Mmu.FallosDePagina,
    $"{corridaLeida.Mmu.FallosDePagina} de {corridaLeida.Mmu.ReferenciasTotales}");

Verificar("mismo rendimiento",
    Math.Abs(corridaLeida.Mmu.RendimientoPorcentual - corridaOriginal.Mmu.RendimientoPorcentual) < 0.001,
    $"{corridaLeida.Mmu.RendimientoPorcentual} %");

// ---------------------------------------------------------------------
Titulo("4 · Historial de corridas");

var id = almacen.RegistrarCorrida(ruta, corridaLeida, leido.Config);
Verificar("la corrida quedó registrada", id > 0, $"id {id}");

var historial = almacen.Historial(ruta);
Verificar("el historial tiene una corrida", historial.Count == 1);

var resumen = historial[0];
Verificar("los fallos guardados coinciden con los del motor",
    resumen.FallosDePagina == corridaLeida.Mmu.FallosDePagina,
    $"{resumen.FallosDePagina}");

Verificar("el rendimiento que calcula la VISTA coincide con el del motor",
    Math.Abs(resumen.RendimientoPorcentual - corridaLeida.Mmu.RendimientoPorcentual) < 0.01,
    $"vista {resumen.RendimientoPorcentual} % · motor {corridaLeida.Mmu.RendimientoPorcentual} %");

var bitacora = almacen.BitacoraDe(ruta, id);
Verificar("la bitácora se guardó", bitacora.Count > 0, $"{bitacora.Count} eventos");

// ---------------------------------------------------------------------
Titulo("5 · Guardar de nuevo NO borra el historial");

almacen.GuardarEscenario(leido, ruta);
Verificar("el historial sobrevive a volver a guardar",
    almacen.Historial(ruta).Count == 1);

var segunda = almacen.RegistrarCorrida(ruta, corridaLeida, leido.Config);
Verificar("se acumulan las corridas",
    almacen.Historial(ruta).Count == 2, $"ids {id} y {segunda}");

Verificar("el historial viene de la más reciente a la más vieja",
    almacen.Historial(ruta)[0].Id > almacen.Historial(ruta)[1].Id);

// ---------------------------------------------------------------------
Titulo("6 · La base rechaza lo imposible");

Verificar("no deja abrir un archivo que no existe",
    Rechaza(() => almacen.AbrirEscenario(Path.Combine(AppContext.BaseDirectory, "no_existe.db"))));

var archivoAjeno = Path.Combine(AppContext.BaseDirectory, "ajeno.db");
File.WriteAllText(archivoAjeno, "esto no es una base de datos");
Verificar("no deja abrir un archivo que no es una base de SPQR",
    Rechaza(() => almacen.AbrirEscenario(archivoAjeno)));
File.Delete(archivoAjeno);

// ---------------------------------------------------------------------
Titulo("7 · Limpiar el historial");

almacen.BorrarHistorial(ruta);
Verificar("el historial quedó vacío", almacen.Historial(ruta).Count == 0);
Verificar("la bitácora cayó en cascada", almacen.BitacoraDe(ruta, id).Count == 0);
Verificar("el escenario sigue intacto",
    almacen.AbrirEscenario(ruta).ListaDeEjecucion.Cantidad == original.ListaDeEjecucion.Cantidad);

// ---------------------------------------------------------------------
Console.WriteLine();
Console.ForegroundColor = enRojo == 0 ? ConsoleColor.Green : ConsoleColor.Red;
Console.WriteLine($"{enVerde} en verde, {enRojo} en rojo");
Console.ResetColor();
Console.WriteLine();
Console.WriteLine("Para mirar la base a ojo: abrí el archivo con DB Browser for SQLite");
Console.WriteLine("y consultá la vista ResumenDeCorridas.");

return enRojo == 0 ? 0 : 1;

// ---------------------------------------------------------------------
static int ContarEscrituras(Programa p)
{
    var n = 0;
    foreach (var r in p.CadenaDeReferencias) if (r.EsEscritura) n++;
    return n;
}

static bool TodosLosProcesosTienenPrograma(Escenario e)
{
    foreach (var p in e.ListaDeEjecucion)
        if (e.BuscarPrograma(p.Programa.Id) is null) return false;
    return true;
}

static bool Rechaza(Action accion)
{
    try { accion(); return false; }
    catch (Exception) { return true; }
}
