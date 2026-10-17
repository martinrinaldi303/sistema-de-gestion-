namespace RetroGreenGestion.Reportes;

/// <summary>Un correo listo para enviar. El adjunto es opcional (el Excel del día).</summary>
public sealed record MensajeCorreo(
    string Destino,
    string Asunto,
    string CuerpoHtml,
    string? RutaAdjunto = null);

/// <summary>
/// Quien efectivamente manda el correo. Es una interfaz para poder reemplazarla por
/// un doble en las pruebas: así se testea el flujo de cierre, duplicados y errores
/// sin un servidor SMTP real.
/// </summary>
public interface IEnviadorCorreo
{
    void Enviar(MensajeCorreo mensaje);
}
