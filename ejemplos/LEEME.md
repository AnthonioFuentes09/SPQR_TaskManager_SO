# Ejemplos de clase

Se abren desde **Pantalla 1 → Importar JSON…**. Después, **Simular** (o **Comparar**, que corre los seis algoritmos a la vez).

Cada archivo reproduce un ejercicio resuelto en clase. La columna «Pizarra» es el resultado que dio el docente o el equipo que expuso; el emulador lo reproduce columna por columna.

| Archivo | Cadena | Marcos | Algoritmo | Pizarra |
|---|---|---|---|---|
| 01 | 1,2,3,3,5,1,2,2,6,2,1,5,7,6,3 | 4 | Óptimo y NRU (docente, 1 sep) | 7 fallos / 15 → 53.33 % · marcos finales 7, 3, 6, 5 |
| 02 | 2,4,6,5,1,2,4,3,3,2,4,6,5,1,3 | 4 | FIFO (Grupo 2) | 11 / 15 → 26.67 % |
| 03 | la misma | 5 | FIFO, anomalía de Belady | 12 / 15 → 20 % |
| 04 | 1,2,3,1,4,2,5 | 3 | Segunda oportunidad (docente, 3 sep) | 6 / 7 → 14.29 % · marcos finales 2, 4, 5 |
| 05 | 1,2,3,4,1,2,5,1,2,3,4,5 | 4 | Reloj (exposición, 3 sep) | 10 / 12 → 16.67 % |
| 06 | 8 programas, 3 páginas cada uno | 3 | Round Robin, quantum 4, ráfaga 4 | 24 / 32 → 25 % |

Para un ejercicio nuevo basta con abrir cualquiera de los cinco primeros y cambiar la cadena (Pantalla 1), la ráfaga al largo de la cadena (Pantalla 2) y los marcos (Pantalla 3).

## Reglas que usa cada algoritmo

Son las del docente en la pizarra, no las del libro:

- **Óptimo** — si varias páginas ya no vuelven a usarse, sale la que entró primero.
- **NRU** — la página que entra por fallo queda con R = 1 y M = 1 (el fallo cuenta como modificación). Un acierto enciende R. Los bits se limpian en cada fallo. Sale la de menor clase; si empatan, la que entró primero.
- **FIFO** — sale la que entró primero; la nueva ocupa su marco.
- **Segunda oportunidad** — entra con R = 0; un acierto le da una vida (R = 1). Al reemplazar, a la que tiene vida se le quita y se la salta, sin moverla de lugar; sale la primera con R = 0.
- **Reloj** — entra con R = 1. La manecilla solo se mueve al reemplazar: si apunta a R = 1 lo apaga y avanza; si apunta a R = 0, ese sale.
- **LRU** — sale la que lleva más tiempo sin usarse.

El «Reinicio del bit R» de la Pantalla 3 tiene que estar en **0** para que los resultados coincidan con la pizarra.
