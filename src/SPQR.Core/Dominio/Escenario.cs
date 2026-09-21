using SPQR.Core.Estructuras;

namespace SPQR.Core.Dominio;

/// <summary>
/// Todo lo que el usuario configura: el catálogo de programas, la lista de
/// ejecución y los parámetros del sistema. Es lo que se guarda y lo que se le
/// entrega al simulador.
/// </summary>
public sealed class Escenario
{
    public ListaEnlazada<Programa> Catalogo { get; } = new();
    public ListaEnlazada<Proceso> ListaDeEjecucion { get; } = new();
    public ConfiguracionSO Config { get; } = new();

    public int SumaDeRafagas
    {
        get { var s = 0; foreach (var p in ListaDeEjecucion) s += p.Rafaga; return s; }
    }

    public Programa? BuscarPrograma(string id)
    {
        foreach (var p in Catalogo) if (p.Id == id) return p;
        return null;
    }

    /// <summary>
    /// Siguiente identificador libre del catálogo.
    ///
    /// Contar los elementos NO sirve: si el catálogo tiene P1…P5 y se quita
    /// P3, el conteo baja a 4 y el siguiente «P5» chocaría con el P5 que
    /// sigue existiendo. Hay que mirar el número más alto en uso, no cuántos
    /// hay.
    /// </summary>
    public string SiguienteIdDePrograma()
    {
        var mayor = 0;
        foreach (var p in Catalogo)
        {
            if (p.Id.Length < 2 || char.ToUpperInvariant(p.Id[0]) != 'P') continue;
            if (int.TryParse(p.Id[1..], out var n) && n > mayor) mayor = n;
        }
        return $"P{mayor + 1}";
    }

    /// <summary>Siguiente identificador libre de la lista de ejecución. Mismo criterio.</summary>
    public int SiguienteIdDeProceso()
    {
        var mayor = 0;
        foreach (var p in ListaDeEjecucion) if (p.Id > mayor) mayor = p.Id;
        return mayor + 1;
    }

    public int SiguienteNumeroDeCola()
    {
        var mayor = 0;
        foreach (var c in Config.Colas) if (c.Numero > mayor) mayor = c.Numero;
        return mayor + 1;
    }

    public Proceso Instanciar(Programa programa, int rafaga, int llegada, int cola)
    {
        var proceso = new Proceso
        {
            Id = SiguienteIdDeProceso(),
            Programa = programa,
            Rafaga = rafaga,
            TiempoLlegada = llegada,
            Cola = cola,
        };
        proceso.Reiniciar();
        ListaDeEjecucion.AgregarAlFinal(proceso);
        return proceso;
    }

    /// <summary>Cuántas instancias de este programa hay en la lista de ejecución.</summary>
    public int InstanciasDe(Programa programa)
    {
        var n = 0;
        foreach (var p in ListaDeEjecucion) if (ReferenceEquals(p.Programa, programa)) n++;
        return n;
    }

    /// <summary>
    /// El escenario que trae el emulador al abrir: cinco programas y ocho
    /// instancias. Es el mismo que muestran los mockups, para que el equipo
    /// pueda comparar la pantalla con el diseño.
    /// </summary>
    public static Escenario PorDefecto()
    {
        var e = new Escenario();
        e.Config.ColasPorDefecto();
        e.Config.AlgoritmoPlanificacion = "MultiplesColas";
        e.Config.AlgoritmoPaginacion = "Lru";

        Programa Agregar(string id, string nombre, int paginas, string cadena, int prioridad, int boletos, int porcentaje)
        {
            var p = new Programa
            {
                Id = id,
                Nombre = nombre,
                TamanioEnPaginas = paginas,
                Prioridad = prioridad,
                Boletos = boletos,
                PorcentajeAsignacion = porcentaje,
            };
            p.CadenaTexto = cadena;
            e.Catalogo.AgregarAlFinal(p);
            return p;
        }

        var word   = Agregar("P1", "Word",        5, "0, 1, 2w, 1, 3, 4, 2, 0w",     1, 40, 25);
        var pdf    = Agregar("P2", "PDF",         4, "0, 2, 1, 2w, 3, 1",            2, 22, 20);
        var excel  = Agregar("P3", "Excel",       5, "1, 0, 3w, 3, 1, 4, 2w, 4",     2, 18, 25);
        var naveg  = Agregar("P4", "Navegador",   6, "2, 4, 0, 1w, 4, 5, 3, 5w",     3, 12, 15);
        var reprod = Agregar("P5", "Reproductor", 5, "0, 3, 2, 3w, 4, 1, 4",         3,  8, 15);

        // Las páginas virtuales del sistema son la suma de lo que piden los
        // programas: 25 contra 8 marcos físicos. Esa desproporción es
        // deliberada — con un marco por página nunca habría reemplazo y los
        // seis algoritmos de la MMU darían todos el mismo resultado.
        var totalPaginas = 0;
        foreach (var p in e.Catalogo) totalPaginas += p.TamanioEnPaginas;
        e.Config.PaginasVirtuales = totalPaginas;

        // Las llegadas se apiñan al principio a propósito: si los procesos se
        // fueran turnando de a uno, cada uno liberaría sus marcos al terminar
        // y nunca habría reemplazo. Con ocho procesos vivos a la vez, la
        // demanda de páginas supera a los ocho marcos y los seis algoritmos
        // de la MMU se empiezan a diferenciar, que es justamente lo que hay
        // que poder demostrar.
        //
        // Y cada ráfaga es MÚLTIPLO del largo de la cadena de su programa, para
        // que ningún proceso corte su cadena a la mitad: Word y Excel tienen 8
        // referencias, PDF 6, Navegador 8 y Reproductor 7. Así el escenario de
        // ejemplo abre sin un solo aviso del validador, que es como tiene que
        // verse una demo.
        e.Instanciar(pdf,    12, 0, 1);   // 2 vueltas de 6
        e.Instanciar(word,   16, 0, 2);   // 2 vueltas de 8
        e.Instanciar(naveg,  16, 1, 3);   // 2 vueltas de 8
        e.Instanciar(word,    8, 1, 1);   // 1 vuelta  de 8
        e.Instanciar(excel,  16, 2, 2);   // 2 vueltas de 8
        e.Instanciar(reprod, 14, 2, 3);   // 2 vueltas de 7
        e.Instanciar(pdf,    12, 3, 1);   // 2 vueltas de 6
        e.Instanciar(word,   16, 3, 2);   // 2 vueltas de 8

        return e;
    }
}
