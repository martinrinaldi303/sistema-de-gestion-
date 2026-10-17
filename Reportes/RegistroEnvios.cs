using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Reportes;

/// <summary>
/// El rastro de los envíos del reporte diario (tabla EnviosDiarios). Un registro
/// por día: así no se manda dos veces el mismo día y, si un envío falló (p. ej. sin
/// internet al cerrar), queda como 'error' para reintentar al abrir el programa.
/// Nunca guarda la contraseña ni nada sensible, sólo el mensaje de error saneado.
/// </summary>
public static class RegistroEnvios
{
    private static string D(DateTime dia) => Repo.Dia(dia);

    public static bool YaEnviado(DateTime dia)
        => Bd.EscalarLong("SELECT COUNT(*) FROM EnviosDiarios WHERE dia = $d AND estado = 'enviado';",
            ("$d", D(dia))) > 0;

    public static int Intentos(DateTime dia)
        => (int)Bd.EscalarLong("SELECT IFNULL(intentos,0) FROM EnviosDiarios WHERE dia = $d;", ("$d", D(dia)));

    public static void MarcarEnviado(DateTime dia, string destino)
        => Bd.Ejecutar(@"
            INSERT INTO EnviosDiarios (dia, estado, intentos, destino, enviado_el, ultimo_error)
            VALUES ($d, 'enviado', 1, $dest, $ahora, NULL)
            ON CONFLICT(dia) DO UPDATE SET
                estado = 'enviado', intentos = intentos + 1,
                destino = $dest, enviado_el = $ahora, ultimo_error = NULL;",
            ("$d", D(dia)), ("$dest", destino), ("$ahora", DateTime.Now.ToString("s")));

    public static void MarcarError(DateTime dia, string error)
        => Bd.Ejecutar(@"
            INSERT INTO EnviosDiarios (dia, estado, intentos, ultimo_error)
            VALUES ($d, 'error', 1, $err)
            ON CONFLICT(dia) DO UPDATE SET
                estado = 'error', intentos = intentos + 1, ultimo_error = $err;",
            ("$d", D(dia)), ("$err", Sanear(error)));

    /// <summary>Último error registrado para un día (para mostrarlo en la pantalla). "" si no hay.</summary>
    public static string UltimoError(DateTime dia)
        => Bd.Escalar("SELECT ultimo_error FROM EnviosDiarios WHERE dia = $d;", ("$d", D(dia)))?.ToString() ?? "";

    /// <summary>Estado del día: 'enviado', 'error' o "" si no hay registro.</summary>
    public static string Estado(DateTime dia)
        => Bd.Escalar("SELECT estado FROM EnviosDiarios WHERE dia = $d;", ("$d", D(dia)))?.ToString() ?? "";

    /// <summary>
    /// Días que todavía hay que enviar: los que tuvieron movimiento, ya terminaron
    /// (son anteriores a hoy), caen dentro de la ventana y o bien nunca se intentaron
    /// o quedaron en 'error' sin agotar los reintentos. El día de hoy no entra acá:
    /// lo dispara la hora configurada.
    /// </summary>
    public static List<DateTime> DiasPendientes(DateTime hoy, int maxDiasAtras, int maxIntentos, DateTime? desde)
    {
        // Piso de la ventana: no más viejo que 'desde' (cuándo se activó) ni que el
        // tope de días hacia atrás. Así al activar no se dispara el histórico entero.
        var piso = hoy.Date.AddDays(-maxDiasAtras);
        if (desde is { } d && d.Date > piso) piso = d.Date;

        var dt = Bd.Tabla(@"
            WITH actividad AS (
                SELECT DISTINCT fecha FROM LibroDiario WHERE anulado = 0
                UNION
                SELECT DISTINCT fecha FROM Egresos WHERE anulado = 0
            )
            SELECT a.fecha AS fecha
            FROM actividad a
            LEFT JOIN EnviosDiarios e ON e.dia = a.fecha
            WHERE a.fecha < $hoy AND a.fecha >= $piso
              AND (e.estado IS NULL OR (e.estado = 'error' AND e.intentos < $maxInt))
            ORDER BY a.fecha;",
            ("$hoy", Repo.Dia(hoy)), ("$piso", Repo.Dia(piso)), ("$maxInt", maxIntentos));

        var lista = new List<DateTime>();
        foreach (DataRow r in dt.Rows)
            if (DateTime.TryParse(r["fecha"].ToString(), out var f)) lista.Add(f.Date);
        return lista;
    }

    // Deja el mensaje en una línea y acotado, para que el log no crezca ni filtre nada raro.
    private static string Sanear(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return "error desconocido";
        var limpio = mensaje.Replace("\r", " ").Replace("\n", " ").Trim();
        return limpio.Length > 300 ? limpio[..300] : limpio;
    }
}
