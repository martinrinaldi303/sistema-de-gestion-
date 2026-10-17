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
        var prof = Repo.Profesionales().First(p => p.Nombre == profesional);
        var serv = Repo.Servicios().First(s => s.Nombre == servicio);
        var med = Repo.MediosPago().First(m => m.Nombre == medio);
        Repo.AgregarServicio(fecha, prof.Id, serv.Id, serv.Nombre, Bd.ACentavos(pesos), med.Id, cliente, "");
    }

    protected static void AgregarEgreso(DateTime fecha, string categoria, decimal pesos, string medio = "Efectivo")
    {
        var cat = Repo.CategoriasGasto().First(c => c.Nombre == categoria);
        var med = Repo.MediosPago().First(m => m.Nombre == medio);
        Repo.AgregarEgreso(fecha, cat.Id, "gasto", "", Bd.ACentavos(pesos), med.Id);
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
