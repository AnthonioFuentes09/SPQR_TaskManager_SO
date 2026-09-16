# SPQR Task Manager

Emulador de un sistema operativo con **planificador de procesos** y **administrador de memoria (MMU)**.
Proyecto Final · Sistemas Operativos I · Sección 74 · UNITEC-CEUTEC

WPF sobre .NET 8, C# 12.

---

## Los tres módulos

El sistema se explica y se reparte en tres módulos. Cada uno es dueño de su
rebanada de punta a punta, y se encuentran en dos interfaces y en el formato
de la traza.

### Módulo 1 · Procesos y planificación — *Antonio Fuentes*
Responde **quién entra a la CPU**.

- Catálogo de programas y lista de ejecución
- Registros del proceso: llegada, ráfaga, prioridad, boletos, porcentaje, remanente
- Los siete algoritmos: FIFO, SJF, Round Robin, Prioridad, Múltiples colas, Garantizada, Sorteo
- Estados del proceso y sus transiciones

Carpetas: `Estructuras/`, `Dominio/`, `Planificacion/`

### Módulo 2 · Memoria y paginación — *Daniel Alexander Ramos*
Responde **cuánta memoria le doy al que está corriendo**.

- Tabla de páginas, marcos, traducción de dirección
- Procedimiento de fallo de página y área de intercambio
- Los seis algoritmos de reemplazo: Óptimo, FIFO, LRU, NRU, Segunda oportunidad, Reloj

Carpetas: `Memoria/`, `Paginacion/`

> **Ojo:** el docente aclaró en clase que **segunda oportunidad y reloj son algoritmos distintos**.
> Van implementados por separado.

### Módulo 3 · Emulación y visualización — *Luis Flores*
Responde **cómo se ve y qué tan bien salió**.

- Orquestador que junta planificador y MMU, y produce la traza
- Las cinco pantallas y el reproductor paso a paso
- Informe de rendimiento

Carpetas: `Simulacion/`, `Metricas/`, `SPQR.UI/`

---

## La decisión de arquitectura

El motor **no dibuja** y la interfaz **no simula**. El orquestador corre la
simulación completa de una vez y devuelve una **traza**: la lista ordenada de
todo lo que ocurrió, instante por instante. La interfaz la reproduce con
controles de play, pausa y paso.

Esto resuelve tres cosas de golpe:

1. La visualización animada se vuelve recorrer una lista con un índice.
2. El algoritmo **Óptimo** se vuelve implementable, porque necesita conocer las
   referencias futuras y la traza las tiene antes de decidir. El docente lo
   habilitó explícitamente: *«en el proyecto vamos a colocar un supuesto de que
   conocemos el futuro»*.
3. El informe de rendimiento es contar sobre la traza.

## Reglas del modelo

- Un **programa** genera **muchos procesos**: la lista de ejecución no es uno a uno.
  Un mismo programa puede aparecer varias veces, cada instancia con su propia ráfaga.
- **Todo es simulado.** No se toca el administrador de tareas real de Windows.
- El núcleo usa **listas enlazadas propias con apuntadores**, no `List<T>`.
  Es un criterio explícito de la rúbrica.

## Rendimiento

El docente lo calcula así, y es el número que espera ver en el informe:

```
F = fallos de página ÷ referencias totales
Rendimiento = (1 − F) × 100 %
```

## Estructura

```
SPQR_TaskManager_SO.sln
├─ src/SPQR.Core/          Biblioteca de clases — sin dependencias de UI
│   ├─ Estructuras/        ListaEnlazada<T>, Nodo<T>
│   ├─ Dominio/            Programa, Proceso, Referencia, TablaDePaginas,
│   │                      ReglaDeCola, ConfiguracionSO, Escenario
│   ├─ Planificacion/      IAlgoritmoPlanificacion + los siete algoritmos
│   │                      + FabricaPlanificadores
│   ├─ Memoria/            MarcoFisico, Mmu
│   ├─ Paginacion/         IAlgoritmoPaginacion + los seis algoritmos
│   │                      + FabricaPaginacion
│   ├─ Simulacion/         Simulador, EventoSimulacion, FotoMemoria,
│   │                      ResultadoSimulacion, ComparadorAlgoritmos
│   └─ Metricas/           MetricasMmu, MetricasPlanificador
└─ src/SPQR.UI/            Aplicación WPF
    ├─ Recursos/           Estilos.xaml — la paleta de los mockups
    ├─ Vistas/             Una por opción del menú, más Ayuda
    └─ ViewModels/         PrincipalViewModel y las filas de cada tabla
```

## Las seis pantallas

| # | Pantalla | Módulo | Qué se hace ahí |
|---|---|---|---|
| 1 | Configuración de programas | 1 | Catálogo: páginas, cadena de referencias, prioridad, boletos, % |
| 2 | Definición de lista de ejecución | 1 | Instancias a simular, algoritmo de planificación, reglas por cola |
| 3 | Configuración SO | 2 | CPU, memoria, almacenamiento y el algoritmo de la MMU |
| 4 | Emular MMU | 3 | Reproductor paso a paso, administrador de tareas, mapa de marcos, tabla de páginas, log |
| 5 | Informe de rendimiento | 3 | Tiempos por proceso y comparación entre los seis algoritmos |
| ? | Ayuda | transversal | Todo el contexto conceptual, fuera de las pantallas de trabajo |

Las pantallas de trabajo no llevan cajas explicativas: esa decisión es a
propósito. Todo lo que hay que entender está reunido en **Ayuda**, así la
pantalla queda limpia para la demostración y el material de estudio queda en
un solo lugar.

## La cadena de referencias

Se escribe como texto en la pantalla 1: `0, 1, 2w, 1, 3`. Una **`w`** después
del número marca una **escritura**, que es lo que enciende el bit M de esa
página. Sin escrituras nunca habría que mandar nada al área de intercambio y
la mitad del costo del reemplazo desaparecería del informe.

Si la ráfaga del proceso es más larga que su cadena, la cadena se recorre en
ciclo.

Las dependencias apuntan en una sola dirección: `SPQR.Core` no referencia a
`SPQR.UI`, así que el motor se puede probar sin abrir la ventana.

## Cómo correrlo

```bash
dotnet build
dotnet run --project src/SPQR.UI
```

## Estado

Funcionando de punta a punta:

- Los **siete** algoritmos de planificación, implementados y verificados.
- Los **seis** algoritmos de reemplazo, implementados y verificados.
- Motor de dos pasadas: la primera genera la cadena global de referencias, la
  segunda corre la MMU con ese futuro a la vista. Es lo que hace implementable
  el Óptimo.
- Interfaz WPF con las seis pantallas, reproductor y exportación del log.
- Escenario de ejemplo cargado al abrir, con presión de memoria real (25
  páginas virtuales contra 8 marcos) para que los seis algoritmos se
  diferencien en el informe.

Verificación del motor sobre el escenario por defecto:

| Algoritmo de la MMU | Fallos | F | Rendimiento |
|---|---|---|---|
| Óptimo | 47 / 112 | 0.42 | 58.0 % |
| NRU | 58 / 112 | 0.52 | 48.2 % |
| Reloj | 65 / 112 | 0.58 | 42.0 % |
| LRU | 67 / 112 | 0.60 | 40.2 % |
| FIFO | 69 / 112 | 0.62 | 38.4 % |
| Segunda oportunidad | 69 / 112 | 0.62 | 38.4 % |

El Óptimo encabeza siempre: es la cota superior y ningún algoritmo
implementable puede superarlo. Si alguna corrida lo desmintiera, hay un error.

La curva de fallos de FIFO variando los marcos también está disponible desde
`ComparadorAlgoritmos.CurvaDeFallos`, para poder mostrar la anomalía de Belady.
