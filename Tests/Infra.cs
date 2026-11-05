using Microsoft.Data.Sqlite;
using RetroGreenGestion.Datos;
using RetroGreenGestion.Reportes;
using Xunit;

// Bd es estático y global, así que los tests que tocan la base no pueden correr en
// paralelo: cada uno usa su propia carpeta temporal, pero comparten el estado estático.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RetroGreenGestion.Tests;

/// <summary>
/// Base para los tests que necesitan una base real: crea una SQLCipher temporal en
/// una carpeta aparte (vía el seam Bd.CarpetaBaseOverride) y la borra al terminar.
/// Nunca toca los datos reales de Documentos\Retro Green.
/// </summary>
public abstract class PruebaConBase : IDisposable
{
    private readonly string _carpeta;

    protected PruebaConBase()
    {
        SQLitePCL.Batteries_V2.Init();
        _carpeta = Path.Combine(Path.GetTempPath(), "rg_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_carpeta);
        Bd.CarpetaBaseOverride = _carpeta;

        if (!Bd.CrearAdminYEntrar("Tester", "clave1234"))
            throw new InvalidOperationException("No se pudo crear la base de prueba.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Bd.CarpetaBaseOverride = null;
        try { Directory.Delete(_carpeta, recursive: true); } catch { /* best-effort */ }
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------ sembrado

    protected static void AgregarServicio(DateTime fecha, string profesional, string servicio,
        decimal pesos, string medio = "Efectivo", string cliente = "")
    {
        var profId = Asegurar("Profesionales", profesional);
        var servId = Asegurar("Servicios", servicio, "precio_cent", Bd.ACentavos(pesos));
        var medId = Asegurar("MediosPago", medio);
        Repo.AgregarServicio(fecha, profId, servId, servicio, Bd.ACentavos(pesos), medId, cliente, "");
    }

    protected static void AgregarEgreso(DateTime fecha, string categoria, decimal pesos, string medio = "Efectivo")
    {
        var catId = Asegurar("CategoriasGasto", categoria);
        var medId = Asegurar("MediosPago", medio);
        Repo.AgregarEgreso(fecha, catId, "gasto", "", Bd.ACentavos(pesos), medId);
    }

    /// <summary>Fija la comisión de un profesional (lo crea si hace falta).</summary>
    protected static void SetComision(string profesional, double pct)
    {
        Asegurar("Profesionales", profesional);
        Bd.Ejecutar("UPDATE Profesionales SET comision_porcentaje = $p WHERE nombre = $n;",
            ("$p", pct), ("$n", profesional));
    }

    // Inserta el ítem de catálogo si no existe (nombre es UNIQUE) y devuelve su id.
    // Reemplaza a la antigua semilla fija de profesionales/servicios.
    protected static long Asegurar(string tabla, string nombre, string? colExtra = null, long valExtra = 0)
    {
        if (colExtra is null)
            Bd.Ejecutar($"INSERT INTO {tabla} (nombre) VALUES ($n) ON CONFLICT(nombre) DO NOTHING;", ("$n", nombre));
        else
            Bd.Ejecutar($"INSERT INTO {tabla} (nombre, {colExtra}) VALUES ($n, $v) ON CONFLICT(nombre) DO NOTHING;",
                ("$n", nombre), ("$v", valExtra));
        return Bd.EscalarLong($"SELECT id FROM {tabla} WHERE nombre = $n;", ("$n", nombre));
    }

    protected static ConfigCorreo ConfigDePrueba(bool activo = true, TimeOnly? hora = null, DateTime? desde = null)
        => new()
        {
            Activo = activo,
            Destino = "duenio@example.com",
            Hora = hora ?? new TimeOnly(21, 0),
            Desde = desde ?? DateTime.Today.AddMonths(-1),
            Host = "smtp.example.com",
            Puerto = 587,
            Ssl = true,
            Usuario = "cuenta@example.com",
            Remitente = "cuenta@example.com"
        };
}

/// <summary>Enviador falso: cuenta envíos y puede simular una caída de SMTP.</summary>
public sealed class EnviadorFalso : IEnviadorCorreo
{
    public int Enviados { get; private set; }
    public List<MensajeCorreo> Mensajes { get; } = new();
    public bool Falla { get; set; }

    public void Enviar(MensajeCorreo mensaje)
    {
        if (Falla) throw new InvalidOperationException("SMTP caído (simulado)");
        Enviados++;
        Mensajes.Add(mensaje);
    }
}
