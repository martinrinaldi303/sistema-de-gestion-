namespace RetroGreenGestion.Datos;

/// <summary>Las tablas. Mismo contenido que esquema.sql.</summary>
public static class Esquema
{
    public const string Sql = """"
-- ============================================================
--  RETRO GREEN - GESTION ECONOMICA
--  Esquema de la base (SQLite + SQLCipher)
--
--  La plata se guarda SIEMPRE en centavos, como numero entero.
--  Nunca como decimal con coma: los redondeos se acumulan y los
--  totales terminan dando 1.589.999,97 en vez de 1.590.000.
-- ============================================================

-- ---------- Catalogos (se cargan una vez, no se borran nunca) ----------

CREATE TABLE IF NOT EXISTS Profesionales (
  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
  nombre               TEXT    NOT NULL UNIQUE,
  comision_porcentaje  REAL    NOT NULL DEFAULT 0,   -- 0 = sin comision
  activo               INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Servicios (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  nombre          TEXT    NOT NULL UNIQUE,
  precio_cent     INTEGER NOT NULL DEFAULT 0,        -- precio sugerido
  activo          INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS MediosPago (
  id      INTEGER PRIMARY KEY AUTOINCREMENT,
  nombre  TEXT    NOT NULL UNIQUE,
  activo  INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS CategoriasGasto (
  id      INTEGER PRIMARY KEY AUTOINCREMENT,
  nombre  TEXT    NOT NULL UNIQUE,
  activo  INTEGER NOT NULL DEFAULT 1
);

-- ---------- Detalle (esto es lo que se exporta y se puede purgar) ----------

CREATE TABLE IF NOT EXISTS LibroDiario (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  fecha           TEXT    NOT NULL,                  -- 'AAAA-MM-DD'
  periodo         TEXT    NOT NULL,                  -- 'AAAA-MM'
  profesional_id  INTEGER NOT NULL REFERENCES Profesionales(id),
  servicio_id     INTEGER NOT NULL REFERENCES Servicios(id),
  descripcion     TEXT    NOT NULL,                  -- nombre del servicio, congelado
  precio_cent     INTEGER NOT NULL,                  -- lo que se cobro, no lo del catalogo
  medio_pago_id   INTEGER NOT NULL REFERENCES MediosPago(id),
  cliente         TEXT,
  nota            TEXT,
  cargado_por     TEXT    NOT NULL,
  cargado_el      TEXT    NOT NULL,
  anulado         INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_libro_periodo ON LibroDiario(periodo, fecha);

CREATE TABLE IF NOT EXISTS Egresos (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  fecha           TEXT    NOT NULL,
  periodo         TEXT    NOT NULL,
  categoria_id    INTEGER NOT NULL REFERENCES CategoriasGasto(id),
  descripcion     TEXT    NOT NULL,
  detalle         TEXT,
  monto_cent      INTEGER NOT NULL,
  medio_pago_id   INTEGER NOT NULL REFERENCES MediosPago(id),
  cargado_por     TEXT    NOT NULL,
  cargado_el      TEXT    NOT NULL,
  anulado         INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_egresos_periodo ON Egresos(periodo, fecha);

-- ---------- Memoria larga (sobrevive a la purga) ----------
-- Si se borra el detalle, esto es lo unico que queda. Ocupa nada
-- y es lo que permite comparar meses y años.

CREATE TABLE IF NOT EXISTS ResumenMensual (
  periodo        TEXT PRIMARY KEY,
  ingresos_cent  INTEGER NOT NULL,
  egresos_cent   INTEGER NOT NULL,
  neto_cent      INTEGER NOT NULL,
  cant_servicios INTEGER NOT NULL,
  cerrado_el     TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS ResumenProfesional (
  periodo             TEXT    NOT NULL,
  profesional         TEXT    NOT NULL,
  cantidad            INTEGER NOT NULL,
  total_cent          INTEGER NOT NULL,
  comision_porcentaje REAL    NOT NULL,
  comision_cent       INTEGER NOT NULL,
  PRIMARY KEY (periodo, profesional)
);

CREATE TABLE IF NOT EXISTS ResumenMedioPago (
  periodo     TEXT    NOT NULL,
  medio_pago  TEXT    NOT NULL,
  total_cent  INTEGER NOT NULL,
  PRIMARY KEY (periodo, medio_pago)
);

CREATE TABLE IF NOT EXISTS ResumenGasto (
  periodo     TEXT    NOT NULL,
  categoria   TEXT    NOT NULL,
  total_cent  INTEGER NOT NULL,
  PRIMARY KEY (periodo, categoria)
);

-- ---------- Rastro de las exportaciones ----------

CREATE TABLE IF NOT EXISTS Exportaciones (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  periodo          TEXT    NOT NULL,
  archivo          TEXT    NOT NULL,
  copia_seguridad  TEXT    NOT NULL,
  filas_servicios  INTEGER NOT NULL,
  filas_egresos    INTEGER NOT NULL,
  borro_detalle    INTEGER NOT NULL,
  fecha            TEXT    NOT NULL,
  usuario          TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Config (
  clave  TEXT PRIMARY KEY,
  valor  TEXT NOT NULL
);
"""";

    /// <summary>
    /// Tablas que se agregaron DESPUES de la primera version. Se aplican con
    /// CREATE TABLE IF NOT EXISTS cada vez que se abre la base, asi una base ya
    /// creada tambien las recibe (la creacion inicial solo corre si esta vacia).
    /// </summary>
    public const string SqlReportes = """"
-- ---------- Reporte diario por email (modulo agregado) ----------
-- Un registro por dia: evita mandar dos veces el mismo dia y permite
-- reintentar si el envio fallo (p. ej. sin internet al cerrar el dia).
CREATE TABLE IF NOT EXISTS EnviosDiarios (
  dia           TEXT PRIMARY KEY,                 -- 'AAAA-MM-DD'
  estado        TEXT    NOT NULL,                 -- 'enviado' | 'error'
  intentos      INTEGER NOT NULL DEFAULT 0,
  ultimo_error  TEXT,                             -- mensaje saneado, sin credenciales
  destino       TEXT,                             -- a quien se mando (para el rastro)
  enviado_el    TEXT                              -- cuando quedo 'enviado'
);
"""";

    /// <summary>
    /// Módulo de stock (agregado). Atado a las ventas: vender un producto descuenta
    /// stock y suma a la caja (VentasProducto es un ingreso más, como el libro diario).
    /// </summary>
    public const string SqlStock = """"
CREATE TABLE IF NOT EXISTS Productos (
  id                INTEGER PRIMARY KEY AUTOINCREMENT,
  nombre            TEXT    NOT NULL UNIQUE,
  codigo            TEXT,
  precio_venta_cent INTEGER NOT NULL DEFAULT 0,
  costo_cent        INTEGER NOT NULL DEFAULT 0,
  stock_actual      INTEGER NOT NULL DEFAULT 0,
  stock_minimo      INTEGER NOT NULL DEFAULT 0,
  activo            INTEGER NOT NULL DEFAULT 1
);

-- Cada cambio de stock queda registrado. 'cantidad' es el delta con signo
-- (entrada +, salida/venta -, ajuste +/-). stock_actual se actualiza en la misma transaccion.
CREATE TABLE IF NOT EXISTS MovimientosStock (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  fecha        TEXT    NOT NULL,                  -- 'AAAA-MM-DD'
  producto_id  INTEGER NOT NULL REFERENCES Productos(id),
  tipo         TEXT    NOT NULL,                  -- 'entrada' | 'salida' | 'ajuste' | 'venta'
  cantidad     INTEGER NOT NULL,                  -- delta con signo
  motivo       TEXT,
  referencia   TEXT,                              -- id de venta/servicio si aplica
  cargado_por  TEXT    NOT NULL,
  cargado_el   TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_movstock_prod ON MovimientosStock(producto_id, fecha);

-- La venta de un producto: ingreso para la caja (igual que un servicio) y, a la par,
-- un movimiento de salida de stock.
CREATE TABLE IF NOT EXISTS VentasProducto (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  fecha         TEXT    NOT NULL,                 -- 'AAAA-MM-DD'
  periodo       TEXT    NOT NULL,                 -- 'AAAA-MM'
  producto_id   INTEGER NOT NULL REFERENCES Productos(id),
  descripcion   TEXT    NOT NULL,                 -- nombre del producto, congelado
  cantidad      INTEGER NOT NULL,
  precio_cent   INTEGER NOT NULL,                 -- total de la venta (centavos)
  medio_pago_id INTEGER NOT NULL REFERENCES MediosPago(id),
  cargado_por   TEXT    NOT NULL,
  cargado_el    TEXT    NOT NULL,
  anulado       INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_ventasprod_periodo ON VentasProducto(periodo, fecha);
"""";

    /// <summary>Módulo de turnos (agenda). Conectado al libro diario: un turno atendido
    /// se puede registrar como servicio.</summary>
    public const string SqlTurnos = """"
CREATE TABLE IF NOT EXISTS Turnos (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  fecha         TEXT    NOT NULL,                 -- 'AAAA-MM-DD'
  hora          TEXT    NOT NULL,                 -- 'HH:mm'
  cliente       TEXT,
  profesional_id INTEGER REFERENCES Profesionales(id),
  servicio_id   INTEGER REFERENCES Servicios(id),
  duracion_min  INTEGER NOT NULL DEFAULT 30,
  estado        TEXT    NOT NULL DEFAULT 'pendiente', -- pendiente|atendido|cancelado|ausente
  nota          TEXT,
  servicio_registrado INTEGER NOT NULL DEFAULT 0,  -- si ya se pasó al libro diario
  cargado_por   TEXT    NOT NULL,
  cargado_el    TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_turnos_fecha ON Turnos(fecha, hora);
"""";
}
