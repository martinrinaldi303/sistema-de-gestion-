using System.Drawing;
using System.Drawing.Imaging;

namespace RetroGreenGestion.Datos;

/// <summary>
/// La identidad del negocio (white-label): su nombre y su logo. El nombre vive en
/// la tabla Config (dentro de la base cifrada); el logo es un archivo (logo.png) en
/// la carpeta de datos. Cada negocio pone lo suyo desde la pantalla de Personalización;
/// el programa no trae ninguna marca fija.
/// </summary>
public static class Marca
{
    private const string KNombre = "marca_nombre";
    public const string NombrePorDefecto = "Mi Negocio";

    /// <summary>Nombre del negocio para títulos, encabezados, Excel y emails.</summary>
    public static string Nombre
    {
        get
        {
            var n = Bd.Config(KNombre, "").Trim();
            return n.Length > 0 ? n : NombrePorDefecto;
        }
    }

    public static void GuardarNombre(string nombre) => Bd.GuardarConfig(KNombre, (nombre ?? "").Trim());

    // ------------------------------------------------------------------ logo

    public static string RutaLogo => Path.Combine(Bd.Carpeta, "logo.png");
    public static bool HayLogo => File.Exists(RutaLogo);

    private static Image? _logo;
    private static bool _logoBuscado;

    /// <summary>El logo del negocio (o null si todavía no cargaron uno).</summary>
    public static Image? Logo
    {
        get
        {
            if (_logoBuscado) return _logo;
            _logoBuscado = true;
            try
            {
                if (File.Exists(RutaLogo))
                {
                    // Copia en memoria: no dejamos el archivo tomado, así se puede reemplazar.
                    var bytes = File.ReadAllBytes(RutaLogo);
                    using var ms = new MemoryStream(bytes);
                    using var tmp = Image.FromStream(ms);
                    _logo = new Bitmap(tmp);
                }
            }
            catch { _logo = null; }
            return _logo;
        }
    }

    /// <summary>Guarda (convierte a PNG) la imagen elegida como logo del negocio.</summary>
    public static void GuardarLogo(string rutaOrigen)
    {
        using (var origen = Image.FromFile(rutaOrigen))
        using (var copia = new Bitmap(origen))
            copia.Save(RutaLogo, ImageFormat.Png);
        Reiniciar();
    }

    public static void QuitarLogo()
    {
        try { if (File.Exists(RutaLogo)) File.Delete(RutaLogo); } catch { /* best-effort */ }
        Reiniciar();
    }

    // Vuelve a leer el logo la próxima vez (tras cambiarlo).
    private static void Reiniciar()
    {
        try { _logo?.Dispose(); } catch { /* nada */ }
        _logo = null;
        _logoBuscado = false;
    }
}
