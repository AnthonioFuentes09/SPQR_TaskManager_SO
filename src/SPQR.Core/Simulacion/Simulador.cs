using SPQR.Core.Dominio;
using SPQR.Core.Estructuras;
using SPQR.Core.Memoria;
using SPQR.Core.Metricas;
using SPQR.Core.Paginacion;
using SPQR.Core.Planificacion;

namespace SPQR.Core.Simulacion;

/// <summary>
/// El motor. Corre la simulación COMPLETA de un solo tirón y devuelve una
/// lista ordenada de eventos; la interfaz después la reproduce sin volver a
/// llamar acá. Esa decisión es la que permite adelantar, retroceder y pausar,
/// y la que convierte el informe final en un simple conteo.
///
/// Trabaja en DOS PASADAS:
///   Pasada 1 — corre solo el planificador y anota la cadena global de
///              referencias: qué página toca cada proceso y en qué orden.
///   Pasada 2 — vuelve a correr exactamente la misma planificación, pero
///              ahora con la MMU encendida y con la cadena de la pasada 1
///              como «futuro».
///
/// La pasada 1 es lo que hace implementable el algoritmo Óptimo, que necesita
/// saber cuándo se volverá a usar cada página. Las dos pasadas dan el mismo
/// orden porque la planificación no depende de la memoria y porque el sorteo
/// usa una semilla fija.
/// </summary>
public static class Simulador
{
    public static ResultadoSimulacion Ejecutar(ConfiguracionSO config, ListaEnlazada<Proceso> procesos)
    {
        // ---------- pasada 1: la cadena global de referencias ----------
        var cadena = new List<(int ProcesoId, int Pagina)>();
        var planificador1 = FabricaPlanificadores.Crear(config.AlgoritmoPlanificacion, config);
        Correr(config, procesos, planificador1, mmu: null, eventos: null,
               registrar: (p, r) => cadena.Add((p.Id, r.Pagina)));

        // ---------- pasada 2: la corrida real, con memoria ----------
        var algoritmoPaginas = FabricaPaginacion.Crear(config.AlgoritmoPaginacion);
        var mmu = new Mmu(config, algoritmoPaginas);
        var planificador2 = FabricaPlanificadores.Crear(config.AlgoritmoPlanificacion, config);
        var eventos = new ListaEnlazada<EventoSimulacion>();

        var indice = 0;
        var metricasPlan = new MetricasPlanificador();
        var memoria = new ListaEnlazada<FotoMemoria>();

        var instanteFinal = Correr(config, procesos, planificador2, mmu, eventos,
            registrar: null,
            futuroEn: () => Futuro(cadena, ++indice),
            metricas: metricasPlan,
            memoria: memoria);

        metricasPlan.Calcular(procesos);

        return new ResultadoSimulacion
        {
            Eventos = eventos,
            Procesos = procesos,
            Memoria = memoria,
            Mmu = mmu.Metricas,
            Planificador = metricasPlan,
            InstanteFinal = instanteFinal,
            NombreAlgoritmoPlanificacion = planificador2.Nombre,
            NombreAlgoritmoPaginacion = mmu.NombreAlgoritmo,
        };
    }

    /// <summary>Las referencias que todavía no ocurrieron, desde <paramref name="desde"/>.</summary>
    private static ListaEnlazada<(int ProcesoId, int Pagina)> Futuro(
        List<(int ProcesoId, int Pagina)> cadena, int desde)
    {
        var lista = new ListaEnlazada<(int, int)>();
        for (var i = desde; i < cadena.Count; i++) lista.AgregarAlFinal(cadena[i]);
        return lista;
    }

    /// <summary>
    /// El ciclo principal, compartido por las dos pasadas. Con
    /// <paramref name="mmu"/> en null se comporta como un planificador puro.
    /// </summary>
    private static int Correr(
        ConfiguracionSO config,
        ListaEnlazada<Proceso> procesos,
        IAlgoritmoPlanificacion planificador,
        Mmu? mmu,
        ListaEnlazada<EventoSimulacion>? eventos,
        Action<Proceso, Referencia>? registrar = null,
        Func<ListaEnlazada<(int ProcesoId, int Pagina)>>? futuroEn = null,
        MetricasPlanificador? metricas = null,
        ListaEnlazada<FotoMemoria>? memoria = null)
    {
        planificador.Reiniciar();

        var totalRafagas = 0;
        foreach (var p in procesos) { p.Reiniciar(); totalRafagas += p.Rafaga; }

        var noLlegados = new ListaEnlazada<Proceso>();
        foreach (var p in Ordenados(procesos)) noLlegados.AgregarAlFinal(p);

        var listos = new ListaEnlazada<Proceso>();
        Proceso? enCpu = null;

        var cpuRepartida = 0;
        var pendientes = procesos.Cantidad;
        var limite = Math.Max(1000, totalRafagas * 20);
        var t = 0;

        while (pendientes > 0 && t < limite)
        {
            // 1 · llegadas de este instante
            while (noLlegados.VerPrimero() is { } siguiente && siguiente.TiempoLlegada <= t)
            {
                noLlegados.QuitarDelInicio();
                siguiente.Estado = EstadoProceso.Listo;
                listos.AgregarAlFinal(siguiente);
            }

            // 2 · limpieza periódica de los bits R (la necesitan NRU y reloj)
            if (mmu is not null && mmu.TocaLimpiarBitsR(t)) mmu.ReiniciarBitsR();

            // 3 · ¿hay que replanificar?
            var quantum = enCpu is null ? 0 : planificador.QuantumPara(enCpu, config);
            var venceQuantum = enCpu is not null && quantum > 0 && enCpu.QuantumConsumido >= quantum;

            if (enCpu is null || venceQuantum || planificador.EsApropiativo)
            {
                if (venceQuantum && enCpu is not null)
                {
                    enCpu.Estado = EstadoProceso.Listo;
                    enCpu.QuantumConsumido = 0;
                    listos.AgregarAlFinal(enCpu);
                    enCpu = null;
                }

                var elegido = planificador.Seleccionar(new ContextoPlanificacion
                {
                    Listos = listos,
                    EnCpu = enCpu,
                    Instante = t,
                    Config = config,
                    CpuTotalRepartida = cpuRepartida,
                });

                if (elegido is not null && !ReferenceEquals(elegido, enCpu))
                {
                    if (enCpu is not null)
                    {
                        enCpu.Estado = EstadoProceso.Listo;
                        enCpu.QuantumConsumido = 0;
                        listos.AgregarAlFinal(enCpu);
                    }
                    listos.Quitar(p => ReferenceEquals(p, elegido));
                    enCpu = elegido;
                    enCpu.QuantumConsumido = 0;
                    if (metricas is not null) metricas.CambiosDeContexto++;
                }
            }

            // 4 · un tick de CPU
            EventoSimulacion? eventoCpu = null;

            if (enCpu is not null)
            {
                enCpu.Estado = EstadoProceso.Ejecutando;
                enCpu.InstantePrimeraEjecucion ??= t;

                var referencia = enCpu.Programa.ReferenciaEn(enCpu.CpuRecibida);
                registrar?.Invoke(enCpu, referencia);

                ResultadoAcceso? acceso = null;
                if (mmu is not null)
                {
                    var futuro = futuroEn?.Invoke() ?? new ListaEnlazada<(int, int)>();
                    acceso = mmu.Acceder(enCpu, referencia, t, futuro);
                }

                enCpu.CpuRecibida++;
                enCpu.QuantumConsumido++;
                enCpu.TiempoRestante--;
                cpuRepartida++;

                var termino = enCpu.TiempoRestante <= 0;
                if (termino)
                {
                    enCpu.Estado = EstadoProceso.Finalizado;
                    enCpu.InstanteFinalizacion = t + 1;
                    enCpu.RemanenteDeQuantum = Math.Max(0, quantum - enCpu.QuantumConsumido);
                    pendientes--;
                    mmu?.LiberarProceso(enCpu);
                }

                if (eventos is not null)
                {
                    eventoCpu = new EventoSimulacion
                    {
                        Instante = t,
                        ProcesoId = enCpu.Id,
                        ProcesoNombre = enCpu.Nombre,
                        Estado = EstadoProceso.Ejecutando,
                        PaginaReferenciada = referencia.Pagina,
                        MarcoAsignado = acceso?.Marco,
                        HuboFalloDePagina = acceso?.Fallo ?? false,
                        PaginaDesalojada = acceso?.PaginaDesalojada,
                        DuenioDeLaVictima = acceso?.DuenioDeLaVictima,
                        SeEscribioADisco = acceso?.SeEscribioADisco ?? false,
                        Observacion = Narrar(enCpu, referencia, acceso, termino),
                    };
                }

                if (termino) enCpu = null;
            }

            // 5 · foto de la memoria física de este instante
            if (memoria is not null && mmu is not null)
            {
                var marcos = new FotoMarco[mmu.Marcos.Length];
                for (var i = 0; i < marcos.Length; i++)
                {
                    var m = mmu.Marcos[i];
                    if (m.Duenio is null)
                    {
                        marcos[i] = new FotoMarco { Numero = i };
                        continue;
                    }
                    var entrada = m.Duenio.TablaDePaginas[m.Pagina];
                    marcos[i] = new FotoMarco
                    {
                        Numero = i,
                        ProcesoId = m.Duenio.Id,
                        Proceso = m.Duenio.Nombre,
                        Pagina = m.Pagina,
                        BitR = entrada.Referenciada,
                        BitM = entrada.Modificada,
                    };
                }
                memoria.AgregarAlFinal(new FotoMemoria
                {
                    Instante = t,
                    Marcos = marcos,
                    MarcoVictima = eventoCpu?.PaginaDesalojada is not null ? eventoCpu.MarcoAsignado ?? -1 : -1,
                });
            }

            // 6 · foto del instante: un evento por proceso, para la línea de tiempo
            if (eventos is not null)
            {
                foreach (var p in procesos)
                {
                    if (eventoCpu is not null && p.Id == eventoCpu.ProcesoId)
                    {
                        eventos.AgregarAlFinal(eventoCpu);
                        continue;
                    }
                    eventos.AgregarAlFinal(new EventoSimulacion
                    {
                        Instante = t,
                        ProcesoId = p.Id,
                        ProcesoNombre = p.Nombre,
                        Estado = p.Estado,
                    });
                }
            }

            t++;
        }

        return t;
    }

    private static string? Narrar(Proceso p, Referencia r, ResultadoAcceso? acceso, bool termino)
    {
        if (acceso is null) return termino ? $"{p.Nombre} finaliza." : null;

        var verbo = r.EsEscritura ? "escribe" : "lee";
        if (!acceso.Fallo)
            return $"{p.Nombre} {verbo} la página {r.Pagina} (acierto, marco {acceso.Marco}).";

        var texto = $"{p.Nombre} {verbo} la página {r.Pagina}: FALLO. Se carga en el marco {acceso.Marco}";
        if (acceso.PaginaDesalojada is { } victima)
        {
            texto += $", desalojando la página {victima} del proceso {acceso.DuenioDeLaVictima}";
            texto += acceso.SeEscribioADisco
                ? " (venía modificada: se escribió al área de intercambio)"
                : " (estaba limpia: no hizo falta escribirla)";
        }
        return texto + ".";
    }

    private static IEnumerable<Proceso> Ordenados(ListaEnlazada<Proceso> procesos)
    {
        var copia = new List<Proceso>();
        foreach (var p in procesos) copia.Add(p);
        copia.Sort((a, b) => a.TiempoLlegada != b.TiempoLlegada
            ? a.TiempoLlegada.CompareTo(b.TiempoLlegada)
            : a.Id.CompareTo(b.Id));
        return copia;
    }
}
