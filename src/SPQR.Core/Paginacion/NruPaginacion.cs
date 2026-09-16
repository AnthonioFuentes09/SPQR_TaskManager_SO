namespace SPQR.Core.Paginacion;

/// <summary>
/// NRU — «no usada recientemente». Clasifica cada página residente en una de
/// cuatro clases combinando los bits R y M, y desaloja una página al azar de
/// la clase más baja que no esté vacía:
///
///   Clase 0 · R = 0, M = 0 → ni usada ni sucia. La mejor candidata.
///   Clase 1 · R = 0, M = 1 → sucia pero vieja. Hay que escribirla al disco.
///   Clase 2 · R = 1, M = 0 → usada hace poco, limpia.
///   Clase 3 · R = 1, M = 1 → usada hace poco y sucia. La peor candidata.
///
/// El razonamiento del algoritmo es que conviene sacrificar una página vieja
/// aunque esté sucia (clase 1) antes que una que se acaba de usar (clase 2),
/// porque lo caro no es la escritura al disco sino el fallo que vendrá
/// enseguida al volver a pedir la página recién sacada.
///
/// Depende de que alguien apague periódicamente los bits R: de eso se encarga
/// la MMU con <c>ConfiguracionSO.IntervaloReinicioBitR</c>. Sin esa limpieza,
/// al poco tiempo todas las páginas quedarían en clase 2 o 3 y NRU no
/// distinguiría nada.
/// </summary>
public sealed class NruPaginacion(int semilla = 2026) : IAlgoritmoPaginacion
{
    private Random _rnd = new(semilla);

    public string Nombre => "NRU";
    public string Criterio => "Clasifica en cuatro clases según los bits R y M y desaloja de la clase más baja que exista.";

    public void Reiniciar() => _rnd = new Random(semilla);
    public void AlCargar(int marco, int instante) { }
    public void AlReferenciar(int marco, int instante) { }

    public int ElegirVictima(ContextoReemplazo c)
    {
        foreach (var marco in c.Marcos) if (marco.Libre) return marco.Numero;

        for (var clase = 0; clase <= 3; clase++)
        {
            var encontrados = 0;
            var elegido = -1;

            foreach (var marco in c.Marcos)
            {
                if (marco.Duenio is null) continue;
                var entrada = marco.Duenio.TablaDePaginas[marco.Pagina];
                if (entrada.Clase != clase) continue;

                encontrados++;
                // Selección uniforme en una sola pasada (muestreo de reservorio).
                if (_rnd.Next(encontrados) == 0) elegido = marco.Numero;
            }

            if (elegido >= 0) return elegido;
        }
        return 0;
    }
}
