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
│   ├─ Persistencia/       IAlmacen (el contrato) y AlmacenJson (intercambio)
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
├─ src/SPQR.Persistence/   La capa de datos — lo único que escribe SQL
│   └─ SqliteEscenarioStore.cs
├─ tools/SPQR.DbCheck/     Verificación de la persistencia, sin abrir la ventana
├─ docs/esquema-base-datos.sql   La fuente de verdad del esquema
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

## Dónde se guarda el trabajo

**En una base SQLite**, igual que el proyecto anterior (SPQR File System
Administrator). Un archivo `.db` es un banco de trabajo completo y
autocontenido: copiarlo es copiar el trabajo, entregarlo es adjuntar un
archivo, y abrir otro escenario es abrir otro archivo. El programa no toca
nada más de la computadora.

Guarda dos cosas distintas:

| Qué | Se reemplaza al guardar | Por qué |
|---|---|---|
| El **escenario**: catálogo, cadenas de referencias, lista de ejecución, colas y parámetros del SO | Sí, entero | Es una foto, no un diff |
| El **historial de corridas**: cada simulación con sus algoritmos, sus fallos y su rendimiento | No, se acumula | Es lo que una base aporta frente a un archivo de configuración |

Esa segunda tabla es la razón de ser de la base. Un archivo de configuración
guarda lo que vas a correr; el historial guarda lo que **ya corriste**, y
sobrevive a cerrar el programa: se puede abrir el `.db` la semana que viene y
comparar la corrida de hoy con la del lunes.

### Las siete tablas

| Tabla | Cuántas filas | Qué representa |
|---|---|---|
| `Configuracion` | siempre 1 | Los parámetros de la máquina simulada |
| `Programa` | una por programa | El catálogo |
| `Referencia` | una por acceso | ★ La cadena de referencias, en orden |
| `ReglaDeCola` | una por cola | Las colas de «múltiples colas» |
| `Proceso` | una por instancia | La lista de ejecución |
| `Corrida` | una por simulación | El historial |
| `EventoCorrida` | una por evento | La bitácora de cada corrida |

Más dos vistas: `ResumenDeCorridas`, que calcula `F` y `1 − F` dentro de la
base, y `ListaDeEjecucionLegible`, que junta cada proceso con el nombre de su
programa.

`Referencia` guarda la cadena **un acceso por fila** en vez de como un texto
`"0, 1, 2w, 1, 3"`. Es más largo de escribir, pero es lo que permite
consultarla: qué páginas toca un programa, cuántas escrituras tiene. Es el
equivalente de la tabla `DataRun` del proyecto anterior.

### Qué guarda y qué no

La base guarda el **estado**. No ejecuta la lógica.

| Lo hace el algoritmo en C# | Lo guarda la base |
|---|---|
| Elegir a quién le toca la CPU | El escenario que se corrió |
| Elegir la página víctima | Cuántos fallos hubo |
| Recorrer la cadena de referencias | La cadena, en orden |
| Calcular `1 − F` | Los dos números para calcularlo |

Si alguien empieza a poner lógica de planificación o de reemplazo dentro de
una consulta SQL, se perdió el proyecto.

### Las capas

| Capa | Qué hace | Habla hacia abajo por |
|---|---|---|
| `SPQR.UI` | Las seis pantallas. **Nunca escribe SQL.** | `IAlmacen` |
| `SPQR.Core` | El motor. Trabaja en memoria. | — |
| `SPQR.Persistence` | Traduce objetos a filas y al revés. Lo único que escribe SQL. | `Microsoft.Data.Sqlite` |
| `escenario.db` | El trabajo. | — |

`IAlmacen` vive en `SPQR.Core/Persistencia/`, así que el motor no arrastra
ninguna dependencia de base de datos y se puede probar sin tocar un archivo.
El único lugar que elige *quién* implementa esa interfaz es `MainWindow`.

### El esquema

`docs/esquema-base-datos.sql` es la fuente de verdad, y viaja embebido dentro
de `SPQR.Persistence` para que la aplicación pueda crear una base nueva sin
depender de que el `.sql` esté al lado del ejecutable. Si el esquema y el
código no coinciden, el que está mal es el código.

El archivo lleva `PRAGMA user_version` para poder migrarlo más adelante, y al
abrir una base más nueva que el programa, el programa avisa en vez de romper.

### Verificar que la persistencia funciona

```bash
dotnet run --project tools/SPQR.DbCheck
```

Crea una base, guarda el escenario de ejemplo, la cierra, la vuelve a abrir y
comprueba que todo volvió igual; después corre una simulación, la registra y
verifica que el rendimiento que calcula la **vista** coincida con el que
calculó el motor. Es la forma más rápida de saber si algo se rompió, y el
mejor lugar para agregar pruebas nuevas.

Para mirar la base a ojo: **DB Browser for SQLite** es gratis y abre los `.db`
como una hoja de cálculo. Cerrá la aplicación antes de escribir desde ahí;
leer mientras corre está bien.

### JSON, como formato de intercambio

Los botones *Exportar JSON* / *Importar JSON* escriben el escenario en texto
plano, para revisarlo a ojo, mandarlo por correo o versionarlo junto al
código. Solo lleva la configuración, no el historial: para eso está la base.

## Validación

Antes de simular, `Validador.Revisar` recorre el escenario y devuelve errores
y avisos. Los errores frenan la simulación; los avisos no. Detecta, entre
otras cosas, una cadena que referencia una página que el programa no tiene, un
proceso parado en una cola que nadie definió, identificadores repetidos, y una
configuración con tantos marcos que no habría un solo reemplazo —caso en el que
los seis algoritmos empatarían y el informe no demostraría nada—.

Los setters del dominio además acotan lo imposible: una ráfaga nunca baja de
1, un porcentaje vive entre 1 y 100, los marcos nunca son cero.

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
