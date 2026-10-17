using RetroGreenGestion.Datos;
using RetroGreenGestion.Exportacion;

namespace RetroGreenGestion.Reportes;

public enum ResultadoEnvio
{
    Enviado,        // se mandó ahora
    YaEstaba,       // ya se había enviado ese día (no se duplica)
    SinConfigurar,  // faltan datos de correo
    Error           // se intentó y falló (queda registrado para reintentar)
}

/// <summary>
/// Orquesta el reporte diario: decide si corresponde enviar, arma el resumen y el
/// Excel del día, lo manda y registra el resultado. Dos reglas firmes:
///   - NO duplica: si el día ya se envió, no vuelve a mandarlo (salvo "forzar").
///   - NO rompe nada: si el envío falla, lo registra y sigue. El cierre económico
///     del mes nunca depende de que el correo salga.
/// </summary>
public static class ServicioReportes
{
    // Reintentos y ventana para el "catch-up" al abrir el programa.
    private const int MaxDiasAtras = 3;
    private const int MaxIntentos = 5;

    /// <summary>Carpeta donde quedan los Excel de los reportes (además de ir adjuntos).</summary>
    public static string CarpetaReportes => Path.Combine(Bd.Carpeta, "Reportes diarios");

    /// <summary>
    /// Envía el reporte de un día. Nunca lanza: ante cualquier falla devuelve Error y
    /// deja el motivo registrado. 'forzar' saltea la protección anti-duplicado (para
    /// el botón "enviar ahora").
    /// </summary>
    public static ResultadoEnvio EnviarDia(DateTime dia, IEnviadorCorreo enviador, ConfigCorreo cfg, bool forzar = false)
    {
        if (!cfg.EstaConfigurada) return ResultadoEnvio.SinConfigurar;
        if (!forzar && RegistroEnvios.YaEnviado(dia)) return ResultadoEnvio.YaEstaba;

        try
        {
            var reporte = ReporteDiario.DeLaBase(dia);
            var excel = Exportador.GenerarDia(dia, CarpetaReportes);
            var mensaje = new MensajeCorreo(cfg.Destino, reporte.Asunto, reporte.Html(), excel.Archivo);

            enviador.Enviar(mensaje);

            RegistroEnvios.MarcarEnviado(dia, cfg.Destino);
            return ResultadoEnvio.Enviado;
        }
        catch (Exception ex)
        {
            RegistroEnvios.MarcarError(dia, ex.Message);
            return ResultadoEnvio.Error;
        }
    }

    /// <summary>¿Corresponde mandar el reporte de HOY ahora? (activo, configurado, pasó la hora, no enviado).</summary>
    public static bool CorresponderiaHoy(ConfigCorreo cfg, DateTime ahora)
        => cfg.Activo && cfg.EstaConfigurada
           && TimeOnly.FromDateTime(ahora) >= cfg.Hora
           && !RegistroEnvios.YaEnviado(ahora.Date);

    /// <summary>
    /// El trabajo automático: lo llama el timer (cada minuto, con el programa abierto)
    /// y el arranque. Manda el día de hoy si ya pasó la hora, y pone al día los días
    /// anteriores que quedaron pendientes (programa cerrado a esa hora, o error previo).
    /// Devuelve cuántos días se enviaron en esta pasada.
    /// </summary>
    public static int ProcesarAutomatico(IEnviadorCorreo enviador, ConfigCorreo cfg, DateTime ahora)
    {
        if (!cfg.Activo || !cfg.EstaConfigurada) return 0;

        var enviados = 0;

        if (CorresponderiaHoy(cfg, ahora) &&
            EnviarDia(ahora.Date, enviador, cfg) == ResultadoEnvio.Enviado)
            enviados++;

        foreach (var dia in RegistroEnvios.DiasPendientes(ahora.Date, MaxDiasAtras, MaxIntentos, cfg.Desde))
            if (EnviarDia(dia, enviador, cfg) == ResultadoEnvio.Enviado)
                enviados++;

        return enviados;
    }
}
