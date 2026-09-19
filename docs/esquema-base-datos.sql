-- =====================================================================
--  SPQR Task Manager · esquema de la base de datos
--  Sistemas Operativos I · Sección 74 · UNITEC-CEUTEC
--
--  Motor: SQLite 3, a través de Microsoft.Data.Sqlite
--  Este archivo es la FUENTE DE VERDAD del esquema. Si acá y el código
--  no coinciden, el que está mal es el código.
--
--  Mismo criterio que el proyecto anterior (SPQR File System
--  Administrator): un archivo .db es un banco de trabajo completo y
--  autocontenido. Copiarlo es copiar el trabajo; entregarlo es adjuntar
--  un archivo.
--
--  QUÉ GUARDA Y QUÉ NO
--  -------------------
--  La base guarda el ESTADO. No ejecuta la lógica.
--
--    Lo hace el algoritmo en C#          Lo guarda la base
--    ---------------------------------   ------------------------------
--    Elegir a quién le toca la CPU       El escenario que se corrió
--    Elegir la página víctima            Cuántos fallos hubo
--    Recorrer la cadena de referencias   La cadena, en orden
--    Calcular 1 − F                      Los dos números para calcularlo
--
--  Si alguien empieza a poner lógica de planificación o de reemplazo
--  dentro de una consulta SQL, se perdió el proyecto.
-- =====================================================================

PRAGMA foreign_keys = ON;
PRAGMA user_version = 1;          -- versión del esquema, para migrar después

-- ---------------------------------------------------------------------
-- 1 · Configuracion — SIEMPRE una sola fila
--     El equivalente de la tabla Volumen del proyecto anterior: los
--     parámetros de la máquina simulada.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Configuracion (
    Id                      INTEGER PRIMARY KEY CHECK (Id = 1),
    Nombre                  TEXT    NOT NULL DEFAULT 'Escenario SPQR',
    Quantum                 INTEGER NOT NULL CHECK (Quantum >= 1),
    MemoriaRamKb            INTEGER NOT NULL CHECK (MemoriaRamKb >= 1),
    PaginasVirtuales        INTEGER NOT NULL CHECK (PaginasVirtuales >= 1),
    MarcosFisicos           INTEGER NOT NULL CHECK (MarcosFisicos >= 1),
    TamanioPaginaKb         INTEGER NOT NULL CHECK (TamanioPaginaKb >= 1),
    DiscosDuros             INTEGER NOT NULL CHECK (DiscosDuros >= 1),
    AreaDeIntercambio       INTEGER NOT NULL CHECK (AreaDeIntercambio >= 0),
    IntervaloReinicioBitR   INTEGER NOT NULL CHECK (IntervaloReinicioBitR >= 0),
    SemillaSorteo           INTEGER NOT NULL,
    AlgoritmoPlanificacion  TEXT    NOT NULL,
    AlgoritmoPaginacion     TEXT    NOT NULL,
    GuardadoEl              TEXT    NOT NULL
);

-- ---------------------------------------------------------------------
-- 2 · Programa — el catálogo. Una fila por programa.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Programa (
    Id                   TEXT    PRIMARY KEY,
    Nombre               TEXT    NOT NULL,
    TamanioEnPaginas     INTEGER NOT NULL CHECK (TamanioEnPaginas >= 1),
    Prioridad            INTEGER NOT NULL CHECK (Prioridad >= 1),
    Boletos              INTEGER NOT NULL CHECK (Boletos >= 1),
    PorcentajeAsignacion INTEGER NOT NULL CHECK (PorcentajeAsignacion BETWEEN 1 AND 100)
);

-- ---------------------------------------------------------------------
-- 3 · Referencia — la cadena de referencias, un acceso por fila.
--
--     Es el equivalente de DataRun: el detalle ORDENADO que cuelga de un
--     registro. Guardar «0, 1, 2w, 1, 3» como una sola cadena de texto
--     sería más corto, pero entonces no se podría consultar. Así se puede
--     preguntar qué páginas toca un programa o cuántas escrituras tiene,
--     que es exactamente para lo que sirve tener una base.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Referencia (
    ProgramaId  TEXT    NOT NULL REFERENCES Programa(Id) ON DELETE CASCADE,
    Orden       INTEGER NOT NULL CHECK (Orden >= 0),
    Pagina      INTEGER NOT NULL CHECK (Pagina >= 0),
    EsEscritura INTEGER NOT NULL CHECK (EsEscritura IN (0, 1)),
    PRIMARY KEY (ProgramaId, Orden)
);

-- ---------------------------------------------------------------------
-- 4 · ReglaDeCola — las colas de «múltiples colas».
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ReglaDeCola (
    Numero    INTEGER PRIMARY KEY CHECK (Numero >= 1),
    Etiqueta  TEXT    NOT NULL DEFAULT '',
    Algoritmo TEXT    NOT NULL,
    Quantum   INTEGER NOT NULL DEFAULT 0 CHECK (Quantum >= 0)
);

-- ---------------------------------------------------------------------
-- 5 · Proceso — la lista de ejecución. Una fila por INSTANCIA.
--
--     ON DELETE RESTRICT a propósito: si un programa tiene instancias,
--     la base no deja borrarlo. Es la misma regla que la pantalla ya
--     aplica, pero acá queda garantizada aunque alguien escriba SQL a
--     mano. Un proceso sin programa no significa nada.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Proceso (
    Id            INTEGER PRIMARY KEY CHECK (Id >= 1),
    ProgramaId    TEXT    NOT NULL REFERENCES Programa(Id) ON DELETE RESTRICT,
    Rafaga        INTEGER NOT NULL CHECK (Rafaga >= 1),
    TiempoLlegada INTEGER NOT NULL CHECK (TiempoLlegada >= 0),
    Cola          INTEGER NOT NULL CHECK (Cola >= 1),
    Orden         INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_Proceso_Programa ON Proceso(ProgramaId);

-- ---------------------------------------------------------------------
-- 6 · Corrida — una fila por simulación ejecutada.
--
--     Acá está la razón de ser de la base. Un archivo de configuración
--     guarda lo que vas a correr; esta tabla guarda lo que YA corriste,
--     y sobrevive a cerrar el programa. Permite abrir la base la semana
--     que viene y comparar la corrida de hoy con la del lunes.
--
--     Se guardan también los parámetros con los que se corrió, aunque
--     estén en Configuracion: la configuración cambia, y una corrida
--     vieja tiene que poder explicarse sola.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Corrida (
    Id                     INTEGER PRIMARY KEY AUTOINCREMENT,
    EjecutadaEl            TEXT    NOT NULL,
    AlgoritmoPlanificacion TEXT    NOT NULL,
    AlgoritmoPaginacion    TEXT    NOT NULL,
    MarcosFisicos          INTEGER NOT NULL,
    Quantum                INTEGER NOT NULL,
    InstanteFinal          INTEGER NOT NULL,
    ReferenciasTotales     INTEGER NOT NULL CHECK (ReferenciasTotales >= 0),
    FallosDePagina         INTEGER NOT NULL CHECK (FallosDePagina >= 0),
    Desalojos              INTEGER NOT NULL CHECK (Desalojos >= 0),
    EscriturasADisco       INTEGER NOT NULL CHECK (EscriturasADisco >= 0),
    CambiosDeContexto      INTEGER NOT NULL CHECK (CambiosDeContexto >= 0),
    RetornoPromedio        REAL    NOT NULL,
    EsperaPromedio         REAL    NOT NULL,
    RespuestaPromedio      REAL    NOT NULL,

    -- No puede haber más fallos que referencias: eso daría un
    -- rendimiento negativo, que no significa nada.
    CHECK (FallosDePagina <= ReferenciasTotales),

    -- Tampoco puede haber más escrituras al área de intercambio que
    -- desalojos: solo se escribe al desalojar una página sucia.
    CHECK (EscriturasADisco <= Desalojos)
);

-- ---------------------------------------------------------------------
-- 7 · EventoCorrida — la bitácora de una corrida.
--
--     El equivalente de LogOperacion. Guarda solo los eventos con
--     observación, que son los que el docente pidió poder mostrar:
--     el cambio de estado de cada proceso y cada fallo de página.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS EventoCorrida (
    CorridaId          INTEGER NOT NULL REFERENCES Corrida(Id) ON DELETE CASCADE,
    Orden              INTEGER NOT NULL,
    Instante           INTEGER NOT NULL CHECK (Instante >= 0),
    ProcesoId          INTEGER NOT NULL,
    ProcesoNombre      TEXT    NOT NULL,
    Estado             TEXT    NOT NULL,
    PaginaReferenciada INTEGER,
    MarcoAsignado      INTEGER,
    HuboFalloDePagina  INTEGER NOT NULL CHECK (HuboFalloDePagina IN (0, 1)),
    PaginaDesalojada   INTEGER,
    SeEscribioADisco   INTEGER NOT NULL CHECK (SeEscribioADisco IN (0, 1)),
    Observacion        TEXT,
    PRIMARY KEY (CorridaId, Orden)
);

CREATE INDEX IF NOT EXISTS ix_Evento_Instante ON EventoCorrida(CorridaId, Instante);

-- ---------------------------------------------------------------------
-- Vista · el rendimiento, tal como lo calcula el docente
--
--     F = fallos ÷ referencias      Rendimiento = 1 − F
--
--     Dejar la fórmula en la base tiene una ventaja concreta: se puede
--     abrir el .db con DB Browser y leer el informe sin abrir el
--     emulador. Ojo, esto NO es lógica de negocio en SQL: es una
--     división sobre dos números que el motor ya contó.
-- ---------------------------------------------------------------------
DROP VIEW IF EXISTS ResumenDeCorridas;
CREATE VIEW ResumenDeCorridas AS
SELECT
    Id,
    EjecutadaEl,
    AlgoritmoPlanificacion,
    AlgoritmoPaginacion,
    MarcosFisicos,
    ReferenciasTotales,
    FallosDePagina,
    ROUND(CAST(FallosDePagina AS REAL) / ReferenciasTotales, 4)              AS F,
    ROUND((1 - CAST(FallosDePagina AS REAL) / ReferenciasTotales) * 100, 2)  AS RendimientoPorcentual,
    Desalojos,
    EscriturasADisco
FROM Corrida
WHERE ReferenciasTotales > 0;

-- ---------------------------------------------------------------------
-- Vista · la lista de ejecución legible, con el nombre del programa
-- ---------------------------------------------------------------------
DROP VIEW IF EXISTS ListaDeEjecucionLegible;
CREATE VIEW ListaDeEjecucionLegible AS
SELECT
    p.Id,
    g.Nombre        AS Programa,
    p.Rafaga,
    p.TiempoLlegada,
    p.Cola,
    g.Prioridad,
    g.Boletos,
    g.PorcentajeAsignacion,
    g.TamanioEnPaginas
FROM Proceso p
JOIN Programa g ON g.Id = p.ProgramaId
ORDER BY p.Orden;
