using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Reportes;

/// <summary>
/// La configuración del reporte diario por email. Todo vive en la tabla Config
/// (dentro de la base cifrada). La contraseña SMTP NO se guarda en claro: se
/// protege con DPAPI (ligada al usuario de Windows) y recién ese blob va a Config.
/// Así hay doble capa: DPAPI + el cifrado SQLCipher de la base.
///
/// Cambiar estos valores se autoriza con la contraseña de administrador desde la
/// pantalla (acá sólo se persiste).
/// </summary>
public sealed class ConfigCorreo
{
    // Nombres de las claves en la tabla Config.
    private const string KActivo    = "correo_activo";
    private const string KDestino   = "correo_destino";
    private const string KHora      = "correo_hora";
    private const string KDesde     = "correo_desde";
    private const string KHost      = "smtp_host";
    private const string KPuerto    = "smtp_puerto";
    private const string KSsl       = "smtp_ssl";
    private const string KUsuario   = "smtp_usuario";
    private const string KRemitente = "smtp_remitente";
    private const string KPass      = "smtp_pass_dpapi";

    public bool Activo { get; set; }
    public string Destino { get; set; } = "";
    public TimeOnly Hora { get; set; } = new(21, 0);   // 21:00 por defecto
    public DateTime? Desde { get; set; }               // desde cuándo se empieza a reportar
    public string Host { get; set; } = "";
    public int Puerto { get; set; } = 587;
    public bool Ssl { get; set; } = true;              // STARTTLS (587) por defecto
    public string Usuario { get; set; } = "";
    public string Remitente { get; set; } = "";

    /// <summary>Están todos los datos para poder enviar (sin contar si está activado).</summary>
    public bool EstaConfigurada =>
        Destino.Trim().Length > 0 && Host.Trim().Length > 0 &&
        Usuario.Trim().Length > 0 && Remitente.Trim().Length > 0;

    // ----------------------------------------------------------- persistencia

    public static ConfigCorreo Cargar()
    {
        var cfg = new ConfigCorreo
        {
            Activo    = Bd.Config(KActivo, "0") == "1",
            Destino   = Bd.Config(KDestino),
            Host      = Bd.Config(KHost),
            Usuario   = Bd.Config(KUsuario),
            Remitente = Bd.Config(KRemitente),
            Ssl       = Bd.Config(KSsl, "1") == "1",
        };

        if (int.TryParse(Bd.Config(KPuerto, "587"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var puerto) && puerto > 0)
            cfg.Puerto = puerto;

        if (TimeOnly.TryParseExact(Bd.Config(KHora, "21:00"), "HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
            cfg.Hora = hora;

        if (DateTime.TryParseExact(Bd.Config(KDesde), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var desde))
            cfg.Desde = desde;

        return cfg;
    }

    /// <summary>Guarda los datos (no toca la contraseña: para eso está GuardarContrasena).</summary>
    public void Guardar()
    {
        // Al activar por primera vez, deja marcado "desde cuándo" para no mandar de
        // golpe el histórico viejo: sólo se reportan los días de hoy en adelante.
        if (Activo && Desde is null) Desde = DateTime.Today;

        Bd.GuardarConfig(KActivo, Activo ? "1" : "0");
        Bd.GuardarConfig(KDestino, Destino.Trim());
        Bd.GuardarConfig(KHora, Hora.ToString("HH:mm", CultureInfo.InvariantCulture));
        Bd.GuardarConfig(KHost, Host.Trim());
        Bd.GuardarConfig(KPuerto, Puerto.ToString(CultureInfo.InvariantCulture));
        Bd.GuardarConfig(KSsl, Ssl ? "1" : "0");
        Bd.GuardarConfig(KUsuario, Usuario.Trim());
        Bd.GuardarConfig(KRemitente, Remitente.Trim());
        if (Desde is { } d) Bd.GuardarConfig(KDesde, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    // ------------------------------------------------------ contraseña (DPAPI)

    public static bool HayContrasena => Bd.Config(KPass).Length > 0;

    /// <summary>Cifra la contraseña con DPAPI (usuario actual de Windows) y la guarda.</summary>
    public static void GuardarContrasena(string texto)
    {
        if (string.IsNullOrEmpty(texto)) { Bd.GuardarConfig(KPass, ""); return; }
        var bytes = Encoding.UTF8.GetBytes(texto);
        var protegido = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        Bd.GuardarConfig(KPass, Convert.ToBase64String(protegido));
    }

    /// <summary>Devuelve la contraseña en claro para usarla al enviar. null si no hay o no se pudo descifrar.</summary>
    public static string? LeerContrasena()
    {
        var guardado = Bd.Config(KPass);
        if (guardado.Length == 0) return null;
        try
        {
            var protegido = Convert.FromBase64String(guardado);
            var bytes = ProtectedData.Unprotect(protegido, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException) { return null; }  // otro usuario / otra máquina
        catch (FormatException) { return null; }
    }
}
