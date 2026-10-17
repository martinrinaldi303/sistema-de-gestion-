using System.Net;
using System.Net.Mail;
using System.Text;

namespace RetroGreenGestion.Reportes;

/// <summary>
/// Envía el correo por SMTP con TLS usando System.Net.Mail (integrado en .NET, sin
/// dependencias extra). Con EnableSsl + puerto 587 hace STARTTLS, que es lo que
/// piden Gmail, Outlook y la mayoría de los proveedores. La autenticación es usuario
/// + contraseña (p. ej. una "contraseña de aplicación" de Gmail).
///
/// La contraseña llega ya descifrada desde ConfigCorreo (DPAPI); esta clase no la
/// guarda ni la registra en ningún lado.
/// </summary>
public sealed class SmtpEnviador : IEnviadorCorreo
{
    private readonly ConfigCorreo _cfg;
    private readonly string _contrasena;

    public SmtpEnviador(ConfigCorreo cfg, string contrasena)
    {
        _cfg = cfg;
        _contrasena = contrasena;
    }

    public void Enviar(MensajeCorreo mensaje)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_cfg.Remitente),
            Subject = mensaje.Asunto,
            Body = mensaje.CuerpoHtml,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        mail.To.Add(mensaje.Destino);

        if (mensaje.RutaAdjunto is { } ruta && File.Exists(ruta))
            mail.Attachments.Add(new Attachment(ruta));

        using var cliente = new SmtpClient(_cfg.Host, _cfg.Puerto)
        {
            EnableSsl = _cfg.Ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_cfg.Usuario, _contrasena),
            Timeout = 30_000
        };

        cliente.Send(mail);
    }
}
