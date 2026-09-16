# Los tres módulos, para explicar el sistema

Guía de reparto y de defensa. Cada quien debe poder explicar su módulo y,
en una frase, qué hacen los otros dos.

## La frase que resume todo

> El planificador decide **quién** usa la CPU. La MMU decide **cuánta memoria**
> le doy al que está corriendo. El tercer módulo los junta y los hace visibles.

## El ciclo, instante por instante

1. El algoritmo de planificación elige un proceso de la cola de listos.
2. Ese proceso ejecuta y referencia una de sus páginas.
3. Si la página no está presente hay **fallo**: el proceso se bloquea y el
   algoritmo de paginación elige la víctima a desalojar.
4. Se carga la página, el proceso vuelve a listo y avanza el reloj.

Sin planificador no sabés a quién le toca; sin MMU no sabés si puede correr.

---

## Módulo 1 · Procesos y planificación

**Qué contesta:** quién entra a la CPU ahora.

| Algoritmo | ¿Quién entra? | ¿Expropia? |
|---|---|---|
| FIFO | El que llegó primero | No |
| SJF | El de ráfaga más corta entre los que llegaron | No |
| Round Robin | El primero de la cola, por un quantum fijo | Sí, al agotarse el quantum |
| Prioridad | El de número de prioridad más alto (1 = alta) | Configurable |
| Múltiples colas | La cola más alta no vacía; dentro, su propio algoritmo | Sí, entre colas |
| Garantizada | El que está más abajo de su porcentaje prometido | Sí |
| Sorteo | Se sortea un boleto; gana su dueño | Probabilístico |

**Convenciones de desempate.** Gana el que llegó antes; si empatan, el de
identificador más bajo (que es el orden en que se agregó a la lista de
ejecución). En Round Robin, quien llega en el instante t entra antes que quien
agota su quantum en ese mismo t: el simulador procesa las llegadas al inicio
del tick y recién después vence el quantum. El docente insistió dos minutos en
esta pregunta sin que nadie la contestara: conviene tenerla lista.

**El contrato de los algoritmos.** `Seleccionar` devuelve al candidato pero no
lo saca de la lista de listos — de eso se encarga el simulador. Así el
algoritmo queda sin efectos secundarios y se puede probar solo. Si el
algoritmo declara `EsApropiativo`, el simulador lo consulta en cada tick; si
no, solo cuando la CPU queda libre.

---

## Módulo 2 · Memoria y paginación

**Qué contesta:** qué marco le asigno a la página que el proceso acaba de tocar.

| Algoritmo | A quién desaloja | Qué recuerda |
|---|---|---|
| Óptimo | Al que tardará más en volver a usarse | La cadena futura |
| FIFO | Al que lleva más tiempo cargado | Cola de orden de carga |
| LRU | Al que lleva más tiempo sin referenciarse | Marca de último acceso |
| NRU | Al de clase más baja según bits R y M | Los dos bits |
| Segunda oportunidad | FIFO, pero si el bit R está encendido lo apaga y da otra vuelta | Cola + bit R |
| Reloj | Puntero circular sobre los marcos, misma idea con otra estructura | Puntero + bit R |

**Las cuatro clases de NRU:** clase 0 ni referenciada ni modificada, clase 1 no
referenciada pero sí modificada, clase 2 referenciada no modificada, clase 3
ambas. Se desaloja de la clase más baja que exista.

**Procedimiento de fallo de página**

1. Se anota el fallo en las métricas.
2. Si hay marco libre, se toma de la lista de marcos libres.
3. Si no hay, el algoritmo elige la víctima.
4. Si la víctima tiene el bit M encendido, se escribe al área de intercambio.
5. Se carga la página y se actualizan ambas tablas.

> **Simplificación declarada.** En un sistema real el proceso se bloquea
> mientras se resuelve el fallo y pierde el procesador. Acá el fallo se resuelve
> dentro del mismo tick y el proceso sigue: el emulador mide el COSTO del fallo
> contándolo, no penalizándolo en tiempo. Es una decisión consciente, porque la
> métrica que pide el curso es `1 − F`, no el tiempo perdido en E/S. Si en la
> demostración preguntan por esto, la respuesta es esta.

**El reinicio periódico del bit R.** Cada cierto número de ticks
(`ConfiguracionSO.IntervaloReinicioBitR`) el sistema apaga todos los bits R.
Sin esa limpieza, a los pocos instantes todas las páginas quedarían con R = 1
y NRU, reloj y segunda oportunidad dejarían de distinguir nada. Es parte del
algoritmo, no un detalle de implementación.

---

## Módulo 3 · Emulación y visualización

**Qué contesta:** cómo se ve la simulación y qué tan bien salió.

- **La traza.** El motor corre todo de una y devuelve la lista de eventos.
  La interfaz la reproduce; no vuelve a llamar al motor.
- **Los paneles:** vista de administrador de tareas, línea de tiempo E/B/F,
  mapa de marcos y tabla de paginación del proceso activo.
- **El informe:** rendimiento = 1 − (fallos ÷ referencias), en porcentaje.

---

## Preguntas de defensa

| Pregunta | Respuesta corta |
|---|---|
| ¿Diferencia entre página y marco? | La página es la unidad de la memoria virtual, el marco la de la física. Mismo tamaño; la tabla dice qué página está en qué marco. |
| ¿Qué es un fallo de página? | Referenciar una página que no está cargada. El SO bloquea el proceso, busca marco, desaloja si hace falta, carga y desbloquea. |
| ¿Para qué el bit de modificación? | Para no escribir al disco de más. Si no se modificó, la copia del área de intercambio sigue siendo válida. |
| ¿Segunda oportunidad y reloj son lo mismo? | No. Misma idea, estructura distinta: uno recorre una cola, el otro un puntero circular. El docente lo aclaró en clase. |
| ¿Cómo implementaron el Óptimo si necesita el futuro? | La traza se genera antes de reproducirse, así que la cadena futura existe al momento de elegir. En un sistema real no. |
| ¿Dónde están las listas enlazadas? | En el núcleo: catálogo de programas, lista de ejecución, colas de listos, marcos libres y cola del FIFO de paginación. |
| ¿El quantum afecta los fallos? | Sí. Un quantum corto multiplica los cambios de contexto y rompe la localidad de referencia. |
| ¿Qué es la anomalía de Belady? | En FIFO, más marcos pueden producir más fallos. LRU y Óptimo no la sufren. |
| ¿Por qué un programa aparece varias veces? | Porque la lista de ejecución simula el uso real: abrís Word cuatro veces al día. Cada instancia es un proceso con su propia ráfaga. |


---

## Dónde vive cada cosa

| Qué | Archivo |
|---|---|
| Lista enlazada con apuntadores | `Estructuras/ListaEnlazada.cs`, `Estructuras/Nodo.cs` |
| Programa, proceso, referencias | `Dominio/Programa.cs`, `Dominio/Proceso.cs`, `Dominio/Referencia.cs` |
| Tabla de páginas y bits R/M | `Dominio/TablaDePaginas.cs` |
| Parámetros del SO y reglas de cola | `Dominio/ConfiguracionSO.cs`, `Dominio/ReglaDeCola.cs` |
| Escenario de ejemplo | `Dominio/Escenario.cs` |
| Los siete de planificación | `Planificacion/*.cs` (uno por algoritmo) |
| Elección del algoritmo desde el menú | `Planificacion/FabricaPlanificadores.cs` |
| La MMU | `Memoria/Mmu.cs` |
| Los seis de paginación | `Paginacion/*.cs` (uno por algoritmo) |
| Elección del algoritmo de la MMU | `Paginacion/FabricaPaginacion.cs` |
| Motor de dos pasadas | `Simulacion/Simulador.cs` |
| Tabla comparativa y curva de Belady | `Simulacion/ComparadorAlgoritmos.cs` |
| Fórmula del rendimiento | `Metricas/MetricasMmu.cs` |
| Interfaz | `SPQR.UI/Vistas/`, `SPQR.UI/ViewModels/` |

## Cómo agregar un algoritmo nuevo

1. Una clase que implemente `IAlgoritmoPlanificacion` o `IAlgoritmoPaginacion`.
2. Una línea en la fábrica correspondiente.

Nada más. El motor solo conoce las interfaces, así que no hay que tocarlo, y
la pantalla se llena sola desde la lista `Disponibles` de la fábrica.
