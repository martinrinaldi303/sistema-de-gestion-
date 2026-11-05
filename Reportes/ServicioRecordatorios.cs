using System.Globalization;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Reportes;

/// <summary>
/// Configuración del bot de recordatorios de turnos. El administrador define si está
/// activo, cuántas horas antes avisar y (opcional) el texto del mensaje. Vive en Config.
/// </summary>
public sealed class ConfigRecordatorios
{
    private const string KActivo = "turnos_recordatorio_activo";
    private const string KHoras = "turnos_recordatorio_horas";
    private const string KTexto = "turnos_recordatorio_texto";

    public const string TextoPorDefecto =
        "Hola {cliente}, te recordamos tu turno del {fecha} a las {hora} en {negocio}. " +
        "¿Vas a venir? Respondé este mail o llamanos para confirmar o cancelar. ¡Gracias!";

    public bool Activo { get; set; }
    public int HorasAntes { get; set; } = 24;
    public string Texto { get; set; } = TextoPorDefecto;

    public static ConfigRecordatorios Cargar()
    {
        var cfg = new ConfigRecordatorios { Activo = Bd.Config(KActivo, "0") == "1" };
        if (int.TryParse(Bd.Config(KHoras, "24"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h > 0)
            cfg.HorasAntes = h;
        var t = Bd.Config(KTexto, "");
        cfg.Texto = t.Length > 0 ? t : TextoPorDefecto;
        return cfg;
    }

    public void Guardar()
    {
        Bd.GuardarConfig(KActivo, Activo ? "1" : "0");
        Bd.GuardarConfig(KHoras, HorasAntes.ToString(CultureInfo.InvariantCulture));
        Bd.GuardarConfig(KTexto, Texto.Trim());
    }
}

/// <summary>
/// El bot de recordatorios: para los turnos pendientes con email cuyo horario cae dentro
/// de la ventana configurada, le manda un mail al cliente preguntándole si va a venir, y
/// lo marca como avisado. Reusa el mismo envío SMTP del reporte diario. Nunca lanza: si un
/// envío falla, no marca el turno (se reintenta en la próxima vuelta).
/// </summary>
public static class ServicioRecordatorios
{
    /// <summary>Procesa los recordatorios pendientes. Devuelve cuántos avisos se mandaron.</summary>
    public static int ProcesarAutomatico(IEnviadorCorreo enviador, ConfigCorreo cfg, DateTime ahora)
    {
        var rec = ConfigRecordatorios.Cargar();
        if (!rec.Activo || !cfg.EstaConfigurada) return 0;

        var enviados = 0;
        foreach (var t in Repo.TurnosParaRecordar(ahora, rec.HorasAntes))
        {
            try
            {
                enviador.Enviar(new MensajeCorreo(t.Email, Asunto(), Html(rec.Texto, t)));
                Repo.MarcarRecordatorioEnviado(t.Id);
                enviados++;
            }
            catch { /* no se marca: se reintenta en la próxima pasada */ }
        }
        return enviados;
    }

    private static string Asunto() => $"{Marca.Nombre} · recordatorio de turno";

    private static string Html(string plantilla, Repo.TurnoRecordatorio t)
    {
        var cuerpo = plantilla
            .Replace("{cliente}", t.Cliente.Length > 0 ? t.Cliente : "")
            .Replace("{fecha}", FechaLinda(t.Fecha))
            .Replace("{hora}", t.Hora)
            .Replace("{negocio}", Marca.Nombre);
        return "<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1a1a1a;max-width:560px;font-size:15px\">"
             + Escapar(cuerpo).Replace("\n", "<br>")
             + "</div>";
    }

    private static string FechaLinda(string iso)
        => DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
            ? f.ToString("dd/MM/yyyy") : iso;

    private static string Escapar(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
