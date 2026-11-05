using System.Data;
using System.Globalization;
using System.Text;

namespace RetroGreenGestion.Datos;

public sealed record Item(long Id, string Nombre, long Cent = 0, double Porcentaje = 0)
{
    public override string ToString() => Nombre;
}

public sealed record TotalesMes(
    long IngresosCent, long EgresosCent, int CantServicios)
{
    public long NetoCent => IngresosCent - EgresosCent;
}

public static class Repo
{
    public static string Periodo(DateTime f) => f.ToString("yyyy-MM");
    public static string Dia(DateTime f) => f.ToString("yyyy-MM-dd");

    // ------------------------------------------------------------ catalogos

    private static List<Item> Catalogo(string tabla, string extra = "0")
    {
        var dt = Bd.Tabla($"SELECT id, nombre, {extra} AS extra FROM {tabla} WHERE activo = 1 ORDER BY nombre;");
        return dt.Rows.Cast<DataRow>()
            .Select(r => new Item(
                Convert.ToInt64(r["id"]),
                r["nombre"].ToString()!,
                extra == "0" ? 0 : Convert.ToInt64(Convert.ToDouble(r["extra"])),
                Convert.ToDouble(r["extra"])))
            .ToList();
    }

    public static List<Item> Profesionales() => Catalogo("Profesionales", "comision_porcentaje");
    public static List<Item> Servicios() => Catalogo("Servicios", "precio_cent");
    public static List<Item> MediosPago() => Catalogo("MediosPago");
    public static List<Item> CategoriasGasto() => Catalogo("CategoriasGasto");

    // ---------------------------------------------------------- libro diario

    public static void AgregarServicio(DateTime fecha, long profesionalId, long servicioId,
        string descripcion, long precioCent, long medioPagoId, string cliente, string nota)
    {
        Bd.Ejecutar(@"
            INSERT INTO LibroDiario
                (fecha, periodo, profesional_id, servicio_id, descripcion,
                 precio_cent, medio_pago_id, cliente, nota, cargado_por, cargado_el)
            VALUES
                ($fecha, $periodo, $prof, $serv, $desc,
                 $precio, $medio, $cliente, $nota, $usuario, $ahora);",
            ("$fecha", Dia(fecha)), ("$periodo", Periodo(fecha)),
            ("$prof", profesionalId), ("$serv", servicioId), ("$desc", descripcion),
            ("$precio", precioCent), ("$medio", medioPagoId),
            ("$cliente", cliente), ("$nota", nota),
            ("$usuario", Bd.Usuario), ("$ahora", DateTime.Now.ToString("s")));
    }

    public static DataTable ListarServicios(string periodo, DateTime? soloDia = null)
    {
        var filtroDia = soloDia is null ? "" : " AND l.fecha = $dia";
        var par = new List<(string, object?)> { ("$periodo", periodo) };
        if (soloDia is not null) par.Add(("$dia", Dia(soloDia.Value)));

        return Bd.Tabla($@"
            SELECT l.id              AS Id,
                   l.fecha           AS Fecha,
                   p.nombre          AS Profesional,
                   l.descripcion     AS Servicio,
                   l.precio_cent/100.0 AS Precio,
                   m.nombre          AS ""Medio de pago"",
                   IFNULL(l.cliente,'') AS Cliente,
                   IFNULL(l.nota,'')    AS Nota
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            JOIN MediosPago    m ON m.id = l.medio_pago_id
            WHERE l.anulado = 0 AND l.periodo = $periodo{filtroDia}
            ORDER BY l.fecha DESC, l.id DESC;", par.ToArray());
    }

    public static void AnularServicio(long id)
        => Bd.Ejecutar("UPDATE LibroDiario SET anulado = 1 WHERE id = $id;", ("$id", id));

    /// <summary>Servicios entre dos fechas (sirve para día, semana o mes).</summary>
    public static DataTable ListarServiciosRango(DateTime desde, DateTime hasta)
        => Bd.Tabla(@"
            SELECT l.id              AS Id,
                   l.fecha           AS Fecha,
                   p.nombre          AS Profesional,
                   l.descripcion     AS Servicio,
                   l.precio_cent/100.0 AS Precio,
                   m.nombre          AS ""Medio de pago"",
                   IFNULL(l.cliente,'') AS Cliente,
                   IFNULL(l.nota,'')    AS Nota
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            JOIN MediosPago    m ON m.id = l.medio_pago_id
            WHERE l.anulado = 0 AND l.fecha BETWEEN $d AND $h
            ORDER BY l.fecha DESC, l.id DESC;",
            ("$d", Dia(desde)), ("$h", Dia(hasta)));

    // --------------------------------------------------------------- clientes

    /// <summary>
    /// Clientes atendidos que coinciden con el texto, con su resumen. El filtro se
    /// hace en C# sin distinguir acentos ni mayúsculas ("martin" encuentra a "Martín").
    /// </summary>
    public static DataTable BuscarClientes(string texto)
    {
        var dt = Bd.Tabla(@"
            SELECT cliente                 AS Cliente,
                   COUNT(*)                AS Visitas,
                   MAX(fecha)              AS ""Última visita"",
                   SUM(precio_cent)/100.0  AS Gastado
            FROM LibroDiario
            WHERE anulado = 0 AND TRIM(IFNULL(cliente,'')) <> ''
            GROUP BY cliente COLLATE NOCASE
            ORDER BY Visitas DESC, Cliente;");

        var filtro = SinAcentos(texto);
        if (filtro.Length == 0) return dt;

        for (var i = dt.Rows.Count - 1; i >= 0; i--)
            if (!SinAcentos(dt.Rows[i]["Cliente"].ToString()!).Contains(filtro))
                dt.Rows.RemoveAt(i);
        return dt;
    }

    /// <summary>minúsculas y sin acentos, para comparar nombres sin importar cómo se escribieron.</summary>
    public static string SinAcentos(string s)
    {
        s = (s ?? "").Trim().ToLowerInvariant();
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Historial completo de un cliente (sus visitas y lo que se le hizo).</summary>
    public static DataTable HistorialCliente(string cliente)
        => Bd.Tabla(@"
            SELECT l.fecha           AS Fecha,
                   l.descripcion     AS Servicio,
                   p.nombre          AS Profesional,
                   l.precio_cent/100.0 AS Precio,
                   m.nombre          AS ""Medio de pago"",
                   IFNULL(l.nota,'')    AS Nota
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            JOIN MediosPago    m ON m.id = l.medio_pago_id
            WHERE l.anulado = 0 AND l.cliente = $c COLLATE NOCASE
            ORDER BY l.fecha DESC, l.id DESC;",
            ("$c", cliente));

    /// <summary>Cantidad y facturacion de servicios de un dia puntual.</summary>
    public static (int cant, long ingresosCent) ServiciosDia(DateTime dia)
    {
        var d = Dia(dia);
        var cant = (int)Bd.EscalarLong(
            "SELECT COUNT(*) FROM LibroDiario WHERE anulado = 0 AND fecha = $d;", ("$d", d));
        var ing = Bd.EscalarLong(
            "SELECT IFNULL(SUM(precio_cent),0) FROM LibroDiario WHERE anulado = 0 AND fecha = $d;", ("$d", d));
        return (cant, ing);
    }

    // --------------------------------------------------------------- egresos

    public static void AgregarEgreso(DateTime fecha, long categoriaId, string descripcion,
        string detalle, long montoCent, long medioPagoId)
    {
        Bd.Ejecutar(@"
            INSERT INTO Egresos
                (fecha, periodo, categoria_id, descripcion, detalle,
                 monto_cent, medio_pago_id, cargado_por, cargado_el)
            VALUES
                ($fecha, $periodo, $cat, $desc, $det,
                 $monto, $medio, $usuario, $ahora);",
            ("$fecha", Dia(fecha)), ("$periodo", Periodo(fecha)),
            ("$cat", categoriaId), ("$desc", descripcion), ("$det", detalle),
            ("$monto", montoCent), ("$medio", medioPagoId),
            ("$usuario", Bd.Usuario), ("$ahora", DateTime.Now.ToString("s")));
    }

    public static DataTable ListarEgresos(string periodo)
        => Bd.Tabla(@"
            SELECT e.id                AS Id,
                   e.fecha             AS Fecha,
                   c.nombre            AS Categoria,
                   e.descripcion       AS Descripcion,
                   e.monto_cent/100.0  AS Monto,
                   m.nombre            AS ""Medio de pago"",
                   IFNULL(e.detalle,'') AS Detalle
            FROM Egresos e
            JOIN CategoriasGasto c ON c.id = e.categoria_id
            JOIN MediosPago      m ON m.id = e.medio_pago_id
            WHERE e.anulado = 0 AND e.periodo = $periodo
            ORDER BY e.fecha DESC, e.id DESC;", ("$periodo", periodo));

    public static void AnularEgreso(long id)
        => Bd.Ejecutar("UPDATE Egresos SET anulado = 1 WHERE id = $id;", ("$id", id));

    /// <summary>Egresos entre dos fechas (sirve para el detalle de un día en el reporte).</summary>
    public static DataTable ListarEgresosRango(DateTime desde, DateTime hasta)
        => Bd.Tabla(@"
            SELECT e.id                AS Id,
                   e.fecha             AS Fecha,
                   c.nombre            AS Categoria,
                   e.descripcion       AS Descripcion,
                   e.monto_cent/100.0  AS Monto,
                   m.nombre            AS ""Medio de pago"",
                   IFNULL(e.detalle,'') AS Detalle
            FROM Egresos e
            JOIN CategoriasGasto c ON c.id = e.categoria_id
            JOIN MediosPago      m ON m.id = e.medio_pago_id
            WHERE e.anulado = 0 AND e.fecha BETWEEN $d AND $h
            ORDER BY e.fecha DESC, e.id DESC;",
            ("$d", Dia(desde)), ("$h", Dia(hasta)));

    /// <summary>Gasto total de un dia puntual.</summary>
    public static long EgresosDia(DateTime dia)
        => Bd.EscalarLong(
            "SELECT IFNULL(SUM(monto_cent),0) FROM Egresos WHERE anulado = 0 AND fecha = $d;",
            ("$d", Dia(dia)));

    // -------------------------------------------------------------- resumen

    public static List<string> PeriodosConDatos()
    {
        var dt = Bd.Tabla(@"
            SELECT periodo FROM LibroDiario WHERE anulado = 0
            UNION SELECT periodo FROM Egresos WHERE anulado = 0
            UNION SELECT periodo FROM VentasProducto WHERE anulado = 0
            UNION SELECT periodo FROM ResumenMensual
            ORDER BY periodo DESC;");
        var lista = dt.Rows.Cast<DataRow>().Select(r => r["periodo"].ToString()!).ToList();
        var actual = Periodo(DateTime.Today);
        if (!lista.Contains(actual)) lista.Insert(0, actual);
        return lista;
    }

    public static TotalesMes Totales(string periodo)
    {
        // Si el periodo ya se cerro y se purgo el detalle, los numeros
        // salen del resumen guardado.
        var cerrado = Bd.Tabla(
            "SELECT ingresos_cent, egresos_cent, cant_servicios FROM ResumenMensual WHERE periodo = $p;",
            ("$p", periodo));

        var ing = Bd.EscalarLong(
            "SELECT IFNULL(SUM(precio_cent),0) FROM LibroDiario WHERE anulado = 0 AND periodo = $p;",
            ("$p", periodo));
        var egr = Bd.EscalarLong(
            "SELECT IFNULL(SUM(monto_cent),0) FROM Egresos WHERE anulado = 0 AND periodo = $p;",
            ("$p", periodo));
        var cant = (int)Bd.EscalarLong(
            "SELECT COUNT(*) FROM LibroDiario WHERE anulado = 0 AND periodo = $p;",
            ("$p", periodo));

        // Las ventas de producto también entran a la caja (ingreso).
        var ventas = VentasProductoPeriodoCent(periodo);
        var ventasCant = (int)Bd.EscalarLong(
            "SELECT COUNT(*) FROM VentasProducto WHERE anulado = 0 AND periodo = $p;", ("$p", periodo));

        if (cant == 0 && egr == 0 && ventasCant == 0 && cerrado.Rows.Count == 1)
        {
            var r = cerrado.Rows[0];
            return new TotalesMes(
                Convert.ToInt64(r["ingresos_cent"]),
                Convert.ToInt64(r["egresos_cent"]),
                Convert.ToInt32(r["cant_servicios"]));
        }
        return new TotalesMes(ing + ventas, egr, cant);
    }

    public static DataTable PorProfesional(string periodo)
    {
        // "% del Total" = cuanto del total facturado del mes hizo cada uno. Es la
        // misma columna que la planilla: resalta quien mas trabaja.
        var vivo = Bd.Tabla(@"
            SELECT p.nombre                                      AS Profesional,
                   COUNT(*)                                      AS Servicios,
                   SUM(l.precio_cent)/100.0                      AS Total,
                   ROUND(100.0 * SUM(l.precio_cent)
                         / NULLIF(SUM(SUM(l.precio_cent)) OVER (), 0), 1) AS ""% del Total"",
                   p.comision_porcentaje                         AS ""% Comisión"",
                   ROUND(SUM(l.precio_cent) * p.comision_porcentaje / 100.0)/100.0 AS Comisión
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            WHERE l.anulado = 0 AND l.periodo = $p
            GROUP BY p.id, p.nombre, p.comision_porcentaje
            ORDER BY Total DESC;", ("$p", periodo));

        if (vivo.Rows.Count > 0) return vivo;

        return Bd.Tabla(@"
            SELECT profesional          AS Profesional,
                   cantidad             AS Servicios,
                   total_cent/100.0     AS Total,
                   ROUND(100.0 * total_cent
                         / NULLIF(SUM(total_cent) OVER (), 0), 1) AS ""% del Total"",
                   comision_porcentaje  AS ""% Comisión"",
                   comision_cent/100.0  AS Comisión
            FROM ResumenProfesional WHERE periodo = $p ORDER BY total_cent DESC;",
            ("$p", periodo));
    }

    /// <summary>Ingresos y egresos de los ultimos 7 dias (incluido hoy).</summary>
    public static (long ingresosCent, long egresosCent) TotalesSemana()
    {
        var desde = Dia(DateTime.Today.AddDays(-6));
        var ing = Bd.EscalarLong(
            "SELECT IFNULL(SUM(precio_cent),0) FROM LibroDiario WHERE anulado = 0 AND fecha >= $d;", ("$d", desde));
        var ventas = Bd.EscalarLong(
            "SELECT IFNULL(SUM(precio_cent),0) FROM VentasProducto WHERE anulado = 0 AND fecha >= $d;", ("$d", desde));
        var egr = Bd.EscalarLong(
            "SELECT IFNULL(SUM(monto_cent),0) FROM Egresos WHERE anulado = 0 AND fecha >= $d;", ("$d", desde));
        return (ing + ventas, egr);
    }

    /// <summary>El servicio mas pedido del mes: nombre y cuantas veces. null si no hay.</summary>
    public static (string nombre, int veces)? ServicioMasVendido(string periodo)
    {
        var dt = TopServicios(periodo, 1);
        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return (r["Servicio"].ToString()!, Convert.ToInt32(r["Veces"]));
    }

    public static DataTable PorMedioPago(string periodo)
    {
        // Incluye servicios (libro diario) y ventas de producto, ambos pagos a la caja.
        var vivo = Bd.Tabla(@"
            SELECT m.nombre AS ""Medio de pago"", SUM(t.cent)/100.0 AS Total
            FROM (
                SELECT medio_pago_id AS mid, precio_cent AS cent FROM LibroDiario   WHERE anulado = 0 AND periodo = $p
                UNION ALL
                SELECT medio_pago_id,       precio_cent        FROM VentasProducto WHERE anulado = 0 AND periodo = $p
            ) t JOIN MediosPago m ON m.id = t.mid
            GROUP BY m.id, m.nombre ORDER BY Total DESC;", ("$p", periodo));

        if (vivo.Rows.Count > 0) return vivo;

        return Bd.Tabla(@"
            SELECT medio_pago AS ""Medio de pago"", total_cent/100.0 AS Total
            FROM ResumenMedioPago WHERE periodo = $p ORDER BY total_cent DESC;", ("$p", periodo));
    }

    public static DataTable PorCategoria(string periodo)
    {
        var vivo = Bd.Tabla(@"
            SELECT c.nombre AS Categoria, SUM(e.monto_cent)/100.0 AS Total
            FROM Egresos e JOIN CategoriasGasto c ON c.id = e.categoria_id
            WHERE e.anulado = 0 AND e.periodo = $p
            GROUP BY c.id, c.nombre ORDER BY Total DESC;", ("$p", periodo));

        if (vivo.Rows.Count > 0) return vivo;

        return Bd.Tabla(@"
            SELECT categoria AS Categoria, total_cent/100.0 AS Total
            FROM ResumenGasto WHERE periodo = $p ORDER BY total_cent DESC;", ("$p", periodo));
    }

    // ----------------------------------------------------- desglose de un dia
    // Mismas reglas que las vistas mensuales (incluida la formula de comision),
    // pero acotadas a una fecha. Las usa el reporte diario por email. No hay
    // fallback al resumen guardado: el detalle de un dia reciente siempre esta.

    public static DataTable PorProfesionalDia(DateTime dia) => PorProfesionalRango(dia, dia);

    /// <summary>
    /// Servicios, total y comisión por profesional entre dos fechas (inclusive): sirve
    /// para un día, una semana, etc. Lee el detalle vivo, así que un mes ya cerrado y
    /// purgado no tiene desglose por semana (sólo queda su resumen mensual).
    /// </summary>
    public static DataTable PorProfesionalRango(DateTime desde, DateTime hasta)
        => Bd.Tabla(@"
            SELECT p.nombre                                      AS Profesional,
                   COUNT(*)                                      AS Servicios,
                   SUM(l.precio_cent)/100.0                      AS Total,
                   ROUND(100.0 * SUM(l.precio_cent)
                         / NULLIF(SUM(SUM(l.precio_cent)) OVER (), 0), 1) AS ""% del Total"",
                   p.comision_porcentaje                         AS ""% Comisión"",
                   ROUND(SUM(l.precio_cent) * p.comision_porcentaje / 100.0)/100.0 AS Comisión
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            WHERE l.anulado = 0 AND l.fecha BETWEEN $d AND $h
            GROUP BY p.id, p.nombre, p.comision_porcentaje
            ORDER BY Total DESC;", ("$d", Dia(desde)), ("$h", Dia(hasta)));

    /// <summary>Nombres de todos los profesionales, también los desactivados (tienen historial).</summary>
    public static List<string> NombresProfesionales()
        => Bd.Tabla("SELECT nombre FROM Profesionales ORDER BY nombre;").Rows.Cast<DataRow>()
            .Select(r => r["nombre"].ToString()!).ToList();

    public static DataTable PorMedioPagoDia(DateTime dia)
        => Bd.Tabla(@"
            SELECT m.nombre AS ""Medio de pago"", SUM(l.precio_cent)/100.0 AS Total
            FROM LibroDiario l JOIN MediosPago m ON m.id = l.medio_pago_id
            WHERE l.anulado = 0 AND l.fecha = $d
            GROUP BY m.id, m.nombre ORDER BY Total DESC;", ("$d", Dia(dia)));

    public static DataTable PorCategoriaDia(DateTime dia)
        => Bd.Tabla(@"
            SELECT c.nombre AS Categoria, SUM(e.monto_cent)/100.0 AS Total
            FROM Egresos e JOIN CategoriasGasto c ON c.id = e.categoria_id
            WHERE e.anulado = 0 AND e.fecha = $d
            GROUP BY c.id, c.nombre ORDER BY Total DESC;", ("$d", Dia(dia)));

    // ------------------------------------------------------------ dashboard

    /// <summary>
    /// Un mes del dashboard. Si el detalle todavia esta, los numeros salen de
    /// ahi; si el mes se purgo, salen del resumen guardado.
    /// </summary>
    public sealed record MesTotales(string Periodo, long IngresosCent, long EgresosCent, int Cantidad)
    {
        public long NetoCent => IngresosCent - EgresosCent;
    }

    public static List<MesTotales> Evolucion(int ultimos = 12)
    {
        var dt = Bd.Tabla(@"
            WITH vivo AS (
                SELECT periodo, SUM(precio_cent) AS ing, 0 AS egr, COUNT(*) AS cant
                FROM LibroDiario WHERE anulado = 0 GROUP BY periodo
                UNION ALL
                SELECT periodo, 0, SUM(monto_cent), 0
                FROM Egresos WHERE anulado = 0 GROUP BY periodo
                UNION ALL
                SELECT periodo, SUM(precio_cent), 0, 0
                FROM VentasProducto WHERE anulado = 0 GROUP BY periodo
            ),
            v AS (SELECT periodo, SUM(ing) AS ing, SUM(egr) AS egr, SUM(cant) AS cant
                  FROM vivo GROUP BY periodo)
            SELECT periodo, ing, egr, cant FROM v
            UNION ALL
            SELECT periodo, ingresos_cent, egresos_cent, cant_servicios
            FROM ResumenMensual
            WHERE periodo NOT IN (SELECT periodo FROM v)
            ORDER BY periodo;");

        return dt.Rows.Cast<DataRow>()
            .Select(r => new MesTotales(
                r["periodo"].ToString()!,
                Convert.ToInt64(r["ing"]),
                Convert.ToInt64(r["egr"]),
                Convert.ToInt32(r["cant"])))
            .TakeLast(ultimos)
            .ToList();
    }

    public static DataTable TopServicios(string periodo, int cuantos = 8)
        => Bd.Tabla(@"
            SELECT descripcion           AS Servicio,
                   COUNT(*)              AS Veces,
                   SUM(precio_cent)/100.0 AS Total
            FROM LibroDiario
            WHERE anulado = 0 AND periodo = $p
            GROUP BY descripcion
            ORDER BY SUM(precio_cent) DESC
            LIMIT $n;", ("$p", periodo), ("$n", cuantos));

    public static DataTable UltimosDias(int dias = 14)
        => Bd.Tabla(@"
            SELECT fecha                  AS Fecha,
                   COUNT(*)               AS Servicios,
                   SUM(precio_cent)/100.0 AS Total
            FROM LibroDiario
            WHERE anulado = 0 AND fecha >= $desde
            GROUP BY fecha ORDER BY fecha;",
            ("$desde", Dia(DateTime.Today.AddDays(-dias + 1))));

    public static DataTable Historico()
        => Bd.Tabla(@"
            SELECT periodo              AS Mes,
                   cant_servicios       AS Servicios,
                   ingresos_cent/100.0  AS Ingresos,
                   egresos_cent/100.0   AS Egresos,
                   neto_cent/100.0      AS Neto
            FROM ResumenMensual ORDER BY periodo DESC;");

    // ------------------------------------------------- cierre y purga de mes

    /// <summary>Graba los totales del mes. Esto sobrevive aunque se borre el detalle.</summary>
    public static void GuardarResumen(string periodo)
    {
        var t = Totales(periodo);

        Bd.Ejecutar(@"
            INSERT INTO ResumenMensual
                (periodo, ingresos_cent, egresos_cent, neto_cent, cant_servicios, cerrado_el)
            VALUES ($p, $i, $e, $n, $c, $f)
            ON CONFLICT(periodo) DO UPDATE SET
                ingresos_cent = $i, egresos_cent = $e, neto_cent = $n,
                cant_servicios = $c, cerrado_el = $f;",
            ("$p", periodo), ("$i", t.IngresosCent), ("$e", t.EgresosCent),
            ("$n", t.NetoCent), ("$c", t.CantServicios), ("$f", DateTime.Now.ToString("s")));

        Bd.Ejecutar("DELETE FROM ResumenProfesional WHERE periodo = $p;", ("$p", periodo));
        Bd.Ejecutar(@"
            INSERT INTO ResumenProfesional
                (periodo, profesional, cantidad, total_cent, comision_porcentaje, comision_cent)
            SELECT $p, p.nombre, COUNT(*), SUM(l.precio_cent), p.comision_porcentaje,
                   CAST(ROUND(SUM(l.precio_cent) * p.comision_porcentaje / 100.0) AS INTEGER)
            FROM LibroDiario l
            JOIN Profesionales p ON p.id = l.profesional_id
            WHERE l.anulado = 0 AND l.periodo = $p
            GROUP BY p.id, p.nombre, p.comision_porcentaje;", ("$p", periodo));

        Bd.Ejecutar("DELETE FROM ResumenMedioPago WHERE periodo = $p;", ("$p", periodo));
        Bd.Ejecutar(@"
            INSERT INTO ResumenMedioPago (periodo, medio_pago, total_cent)
            SELECT $p, m.nombre, SUM(t.cent)
            FROM (
                SELECT medio_pago_id AS mid, precio_cent AS cent FROM LibroDiario   WHERE anulado = 0 AND periodo = $p
                UNION ALL
                SELECT medio_pago_id,       precio_cent        FROM VentasProducto WHERE anulado = 0 AND periodo = $p
            ) t JOIN MediosPago m ON m.id = t.mid
            GROUP BY m.id, m.nombre;", ("$p", periodo));

        Bd.Ejecutar("DELETE FROM ResumenGasto WHERE periodo = $p;", ("$p", periodo));
        Bd.Ejecutar(@"
            INSERT INTO ResumenGasto (periodo, categoria, total_cent)
            SELECT $p, c.nombre, SUM(e.monto_cent)
            FROM Egresos e JOIN CategoriasGasto c ON c.id = e.categoria_id
            WHERE e.anulado = 0 AND e.periodo = $p
            GROUP BY c.id, c.nombre;", ("$p", periodo));
    }

    /// <summary>Borra el detalle de un mes. Solo se llama despues de exportar y verificar.</summary>
    public static void BorrarDetalle(string periodo)
    {
        using var cn = Bd.Conectar();
        using var tx = cn.BeginTransaction();
        Bd.Ejecutar(cn, "DELETE FROM LibroDiario WHERE periodo = $p;", ("$p", periodo));
        Bd.Ejecutar(cn, "DELETE FROM Egresos WHERE periodo = $p;", ("$p", periodo));
        Bd.Ejecutar(cn, "DELETE FROM VentasProducto WHERE periodo = $p;", ("$p", periodo));
        tx.Commit();
    }

    public static void RegistrarExportacion(string periodo, string archivo, string copia,
        int filasServicios, int filasEgresos, bool borroDetalle)
        => Bd.Ejecutar(@"
            INSERT INTO Exportaciones
                (periodo, archivo, copia_seguridad, filas_servicios, filas_egresos,
                 borro_detalle, fecha, usuario)
            VALUES ($p, $a, $c, $fs, $fe, $b, $f, $u);",
            ("$p", periodo), ("$a", archivo), ("$c", copia),
            ("$fs", filasServicios), ("$fe", filasEgresos),
            ("$b", borroDetalle ? 1 : 0),
            ("$f", DateTime.Now.ToString("s")), ("$u", Bd.Usuario));

    /// <summary>Meses ya terminados que todavia no se exportaron.</summary>
    public static List<string> MesesCerradosSinExportar()
    {
        var actual = Periodo(DateTime.Today);
        var dt = Bd.Tabla(@"
            SELECT DISTINCT periodo FROM (
                SELECT periodo FROM LibroDiario WHERE anulado = 0
                UNION SELECT periodo FROM Egresos WHERE anulado = 0
                UNION SELECT periodo FROM VentasProducto WHERE anulado = 0
            ) WHERE periodo < $actual
              AND periodo NOT IN (SELECT periodo FROM Exportaciones)
            ORDER BY periodo;", ("$actual", actual));
        return dt.Rows.Cast<DataRow>().Select(r => r["periodo"].ToString()!).ToList();
    }

    // ===================================================== stock y productos

    /// <summary>Un producto para el combo de venta (id, nombre, precio y stock).</summary>
    public sealed record ProductoVenta(long Id, string Nombre, long PrecioVentaCent, long StockActual);

    public static List<ProductoVenta> ProductosActivos()
        => Bd.Tabla("SELECT id, nombre, precio_venta_cent, stock_actual FROM Productos WHERE activo = 1 ORDER BY nombre;")
            .Rows.Cast<DataRow>()
            .Select(r => new ProductoVenta(
                Convert.ToInt64(r["id"]), r["nombre"].ToString()!,
                Convert.ToInt64(r["precio_venta_cent"]), Convert.ToInt64(r["stock_actual"])))
            .ToList();

    public static DataTable ListarProductos()
        => Bd.Tabla(@"
            SELECT id                      AS Id,
                   nombre                  AS Producto,
                   IFNULL(codigo,'')       AS Código,
                   precio_venta_cent/100.0 AS Precio,
                   costo_cent/100.0        AS Costo,
                   stock_actual            AS Stock,
                   stock_minimo            AS Mínimo
            FROM Productos WHERE activo = 1 ORDER BY nombre;");

    /// <summary>Alta de producto. El stock arranca en 0 y, si hay inicial, se carga como movimiento.</summary>
    public static long AgregarProducto(string nombre, string codigo, long precioVentaCent,
        long costoCent, long stockInicial, long stockMinimo)
    {
        Bd.Ejecutar(@"
            INSERT INTO Productos (nombre, codigo, precio_venta_cent, costo_cent, stock_actual, stock_minimo)
            VALUES ($n, $cod, $pv, $co, 0, $min)
            ON CONFLICT(nombre) DO UPDATE SET activo = 1, codigo = $cod,
                precio_venta_cent = $pv, costo_cent = $co, stock_minimo = $min;",
            ("$n", nombre.Trim()), ("$cod", codigo), ("$pv", precioVentaCent),
            ("$co", costoCent), ("$min", stockMinimo));
        var id = Bd.EscalarLong("SELECT id FROM Productos WHERE nombre = $n;", ("$n", nombre.Trim()));
        if (stockInicial != 0)
            RegistrarMovimiento(id, stockInicial > 0 ? "entrada" : "ajuste", stockInicial, "stock inicial");
        return id;
    }

    public static void EditarProducto(long id, string nombre, string codigo,
        long precioVentaCent, long costoCent, long stockMinimo)
        => Bd.Ejecutar(@"
            UPDATE Productos SET nombre = $n, codigo = $cod, precio_venta_cent = $pv,
                   costo_cent = $co, stock_minimo = $min WHERE id = $id;",
            ("$n", nombre.Trim()), ("$cod", codigo), ("$pv", precioVentaCent),
            ("$co", costoCent), ("$min", stockMinimo), ("$id", id));

    /// <summary>No se borra: se desactiva (conserva el histórico de movimientos y ventas).</summary>
    public static void DesactivarProducto(long id)
        => Bd.Ejecutar("UPDATE Productos SET activo = 0 WHERE id = $id;", ("$id", id));

    /// <summary>Registra un movimiento (delta con signo) y actualiza el stock en la misma transacción.</summary>
    public static void RegistrarMovimiento(long productoId, string tipo, long deltaConSigno,
        string motivo = "", string referencia = "")
    {
        using var cn = Bd.Conectar();
        using var tx = cn.BeginTransaction();
        Bd.Ejecutar(cn, @"
            INSERT INTO MovimientosStock (fecha, producto_id, tipo, cantidad, motivo, referencia, cargado_por, cargado_el)
            VALUES ($f, $p, $t, $c, $m, $r, $u, $e);",
            ("$f", Dia(DateTime.Today)), ("$p", productoId), ("$t", tipo), ("$c", deltaConSigno),
            ("$m", motivo), ("$r", referencia), ("$u", Bd.Usuario), ("$e", DateTime.Now.ToString("s")));
        Bd.Ejecutar(cn, "UPDATE Productos SET stock_actual = stock_actual + $c WHERE id = $p;",
            ("$c", deltaConSigno), ("$p", productoId));
        tx.Commit();
    }

    /// <summary>Vende un producto: lo suma a la caja (ingreso) y descuenta stock, todo junto.</summary>
    public static void RegistrarVentaProducto(long productoId, long cantidad, long precioTotalCent, long medioPagoId)
    {
        var nombre = Bd.Escalar("SELECT nombre FROM Productos WHERE id = $id;", ("$id", productoId))?.ToString() ?? "Producto";
        var fecha = Dia(DateTime.Today);
        using var cn = Bd.Conectar();
        using var tx = cn.BeginTransaction();
        Bd.Ejecutar(cn, @"
            INSERT INTO VentasProducto
                (fecha, periodo, producto_id, descripcion, cantidad, precio_cent, medio_pago_id, cargado_por, cargado_el)
            VALUES ($f, $per, $p, $d, $cant, $precio, $medio, $u, $e);",
            ("$f", fecha), ("$per", Periodo(DateTime.Today)), ("$p", productoId), ("$d", nombre),
            ("$cant", cantidad), ("$precio", precioTotalCent), ("$medio", medioPagoId),
            ("$u", Bd.Usuario), ("$e", DateTime.Now.ToString("s")));
        var ventaId = Convert.ToInt64(Bd.Tabla(cn, "SELECT last_insert_rowid() AS id;").Rows[0]["id"]);
        Bd.Ejecutar(cn, @"
            INSERT INTO MovimientosStock (fecha, producto_id, tipo, cantidad, motivo, referencia, cargado_por, cargado_el)
            VALUES ($f, $p, 'venta', $c, 'venta', $r, $u, $e);",
            ("$f", fecha), ("$p", productoId), ("$c", -cantidad), ("$r", ventaId.ToString()),
            ("$u", Bd.Usuario), ("$e", DateTime.Now.ToString("s")));
        Bd.Ejecutar(cn, "UPDATE Productos SET stock_actual = stock_actual - $c WHERE id = $p;",
            ("$c", cantidad), ("$p", productoId));
        tx.Commit();
    }

    public static DataTable ProductosBajoMinimo()
        => Bd.Tabla(@"
            SELECT nombre AS Producto, stock_actual AS Stock, stock_minimo AS Mínimo
            FROM Productos
            WHERE activo = 1 AND stock_minimo > 0 AND stock_actual <= stock_minimo
            ORDER BY (stock_actual - stock_minimo), nombre;");

    public static long ValorInventarioCent()
        => Bd.EscalarLong("SELECT IFNULL(SUM(stock_actual * costo_cent), 0) FROM Productos WHERE activo = 1;");

    public static DataTable MovimientosProducto(long productoId, int cuantos = 60)
        => Bd.Tabla(@"
            SELECT fecha AS Fecha, tipo AS Tipo, cantidad AS Cantidad, IFNULL(motivo,'') AS Motivo
            FROM MovimientosStock WHERE producto_id = $id ORDER BY id DESC LIMIT $n;",
            ("$id", productoId), ("$n", cuantos));

    public static DataTable ListarVentasProducto(string periodo)
        => Bd.Tabla(@"
            SELECT v.fecha            AS Fecha,
                   v.descripcion      AS Producto,
                   v.cantidad         AS Cantidad,
                   v.precio_cent/100.0 AS Total,
                   m.nombre           AS ""Medio de pago""
            FROM VentasProducto v JOIN MediosPago m ON m.id = v.medio_pago_id
            WHERE v.anulado = 0 AND v.periodo = $p
            ORDER BY v.fecha DESC, v.id DESC;", ("$p", periodo));

    /// <summary>Ingreso de ventas de producto del mes (para sumarlo a la caja).</summary>
    public static long VentasProductoPeriodoCent(string periodo)
        => Bd.EscalarLong("SELECT IFNULL(SUM(precio_cent),0) FROM VentasProducto WHERE anulado = 0 AND periodo = $p;",
            ("$p", periodo));

    /// <summary>Cantidad e ingreso de ventas de producto de un día.</summary>
    public static (int cant, long ingresosCent) VentasProductoDia(DateTime dia)
    {
        var d = Dia(dia);
        var cant = (int)Bd.EscalarLong("SELECT COUNT(*) FROM VentasProducto WHERE anulado = 0 AND fecha = $d;", ("$d", d));
        var ing = Bd.EscalarLong("SELECT IFNULL(SUM(precio_cent),0) FROM VentasProducto WHERE anulado = 0 AND fecha = $d;", ("$d", d));
        return (cant, ing);
    }
}
