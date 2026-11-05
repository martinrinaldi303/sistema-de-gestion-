using RetroGreenGestion.Datos;
using RetroGreenGestion.Reportes;
using Xunit;

namespace RetroGreenGestion.Tests;

public sealed class TurnosTests : PruebaConBase
{
    [Fact]
    public void AltaYListadoDelDia()
    {
        var dia = new DateTime(2026, 11, 10);
        Repo.AgregarTurno(dia, "11:00", "Juan", "", null, null, 30, "");

        var dt = Repo.TurnosDia(dia);
        Assert.Single(dt.Rows);
        Assert.Equal("Juan", dt.Rows[0]["Cliente"]);
        Assert.Equal("pendiente", dt.Rows[0]["Estado"]);
    }

    [Fact]
    public void CambiarEstado()
    {
        var dia = new DateTime(2026, 11, 10);
        Repo.AgregarTurno(dia, "09:00", "Ana", "", null, null, 30, "");
        var id = Convert.ToInt64(Repo.TurnosDia(dia).Rows[0]["Id"]);

        Repo.CambiarEstadoTurno(id, "cancelado");

        Assert.Equal("cancelado", Repo.TurnosDia(dia).Rows[0]["Estado"]);
    }

    [Fact]
    public void RegistrarServicioDesdeTurno_CreaFilaEnElLibro_YmarcaAtendido()
    {
        var dia = DateTime.Today;
        var profId = Asegurar("Profesionales", "Leo");
        var servId = Asegurar("Servicios", "Corte", "precio_cent", Bd.ACentavos(8000m));
        var medId = Asegurar("MediosPago", "Efectivo");
        Repo.AgregarTurno(dia, "10:00", "Cliente X", "", profId, servId, 30, "");
        var id = Convert.ToInt64(Repo.TurnosDia(dia).Rows[0]["Id"]);

        Repo.RegistrarServicioDesdeTurno(id, Bd.ACentavos(8000m), medId);

        var (cant, ing) = Repo.ServiciosDia(dia);
        Assert.Equal(1, cant);
        Assert.Equal(Bd.ACentavos(8000m), ing);
        Assert.Equal("atendido", Repo.TurnosDia(dia).Rows[0]["Estado"]);

        // No se puede registrar dos veces.
        Assert.Throws<InvalidOperationException>(() => Repo.RegistrarServicioDesdeTurno(id, Bd.ACentavos(8000m), medId));
    }

    [Fact]
    public void Recordatorios_SeMandanDentroDeLaVentana_YseMarcan()
    {
        var ahora = new DateTime(2026, 11, 10, 10, 0, 0);
        Repo.AgregarTurno(ahora.Date, "11:00", "Con mail", "cli@example.com", null, null, 30, "");          // dentro de 24h
        Repo.AgregarTurno(ahora.Date.AddDays(3), "11:00", "Lejano", "lejos@example.com", null, null, 30, "");// fuera
        Repo.AgregarTurno(ahora.Date, "12:00", "Sin mail", "", null, null, 30, "");                         // sin email
        new ConfigRecordatorios { Activo = true, HorasAntes = 24, Texto = ConfigRecordatorios.TextoPorDefecto }.Guardar();

        var cfg = ConfigDePrueba();
        var fake = new EnviadorFalso();

        var n = ServicioRecordatorios.ProcesarAutomatico(fake, cfg, ahora);

        Assert.Equal(1, n);
        Assert.Equal(1, fake.Enviados);
        Assert.Equal("cli@example.com", fake.Mensajes[0].Destino);

        // Segunda pasada: ya quedó marcado, no se repite.
        Assert.Equal(0, ServicioRecordatorios.ProcesarAutomatico(fake, cfg, ahora));
    }

    [Fact]
    public void Recordatorios_NoMandan_SiEstanDesactivados()
    {
        var ahora = new DateTime(2026, 11, 10, 10, 0, 0);
        Repo.AgregarTurno(ahora.Date, "11:00", "Con mail", "cli@example.com", null, null, 30, "");
        new ConfigRecordatorios { Activo = false, HorasAntes = 24, Texto = ConfigRecordatorios.TextoPorDefecto }.Guardar();

        Assert.Equal(0, ServicioRecordatorios.ProcesarAutomatico(new EnviadorFalso(), ConfigDePrueba(), ahora));
    }
}
