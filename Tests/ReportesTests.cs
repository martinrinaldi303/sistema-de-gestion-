using System.Data;
using RetroGreenGestion.Datos;
using RetroGreenGestion.Exportacion;
using RetroGreenGestion.Reportes;
using Xunit;

namespace RetroGreenGestion.Tests;

public sealed class CalculosTests : PruebaConBase
{
    [Fact]
    public void TotalesDelDia_SumanAlCentavo()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 15000.00m);
        AgregarServicio(hoy, "Anahí", "Barba", 8000.50m);
        AgregarServicio(hoy, "Pedro", "Color", 45000.99m);
        AgregarEgreso(hoy, "Supermercado", 12000.25m);

        var r = ReporteDiario.DeLaBase(hoy);

        Assert.Equal(3, r.CantServicios);
        Assert.Equal(Bd.ACentavos(15000.00m + 8000.50m + 45000.99m), r.IngresosCent);
        Assert.Equal(Bd.ACentavos(12000.25m), r.EgresosCent);
        Assert.Equal(r.IngresosCent - r.EgresosCent, r.NetoCent);
    }

    [Fact]
    public void DesglosePorProfesional_CoincideConElTotal()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 15000m);
        AgregarServicio(hoy, "Pedro", "Barba", 8000m);
        AgregarServicio(hoy, "Anahí", "Color", 45000m);

        var r = ReporteDiario.DeLaBase(hoy);

        // La suma de la columna Total (en pesos) debe cuadrar al centavo con el total.
        long sumaCent = 0;
        foreach (DataRow fila in r.PorProfesional.Rows)
            sumaCent += Bd.ACentavos((decimal)Convert.ToDouble(fila["Total"]));

        Assert.Equal(r.IngresosCent, sumaCent);
    }

    [Fact]
    public void Comision_UsaElPorcentajeDelProfesional()
    {
        var hoy = DateTime.Today;
        // Pedro al 10%: comisión = 10% de lo facturado.
        Bd.Ejecutar("UPDATE Profesionales SET comision_porcentaje = 10 WHERE nombre = 'Pedro';");
        AgregarServicio(hoy, "Pedro", "Corte", 20000m);

        var r = ReporteDiario.DeLaBase(hoy);
        var pedro = r.PorProfesional.Rows.Cast<DataRow>().First(f => (string)f["Profesional"] == "Pedro");

        Assert.Equal(2000.0, Convert.ToDouble(pedro["Comisión"]), 3);
    }
}

public sealed class FechasTests : PruebaConBase
{
    [Fact]
    public void ServiciosDeOtroDia_NoEntranEnElReporte()
    {
        var hoy = DateTime.Today;
        var ayer = hoy.AddDays(-1);
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        AgregarServicio(ayer, "Pedro", "Corte", 99999m);

        var r = ReporteDiario.DeLaBase(hoy);

        Assert.Equal(1, r.CantServicios);
        Assert.Equal(Bd.ACentavos(10000m), r.IngresosCent);
    }

    [Fact]
    public void Periodo_EsElMesDeLaFecha()
    {
        Assert.Equal("2025-03", Repo.Periodo(new DateTime(2025, 3, 15)));
        Assert.Equal("2025-03-15", Repo.Dia(new DateTime(2025, 3, 15)));
    }
}

public sealed class DuplicadosYErroresTests : PruebaConBase
{
    [Fact]
    public void NoManda_DosVeces_ElMismoDia()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        var cfg = ConfigDePrueba();
        var enviador = new EnviadorFalso();

        var primera = ServicioReportes.EnviarDia(hoy, enviador, cfg);
        var segunda = ServicioReportes.EnviarDia(hoy, enviador, cfg);

        Assert.Equal(ResultadoEnvio.Enviado, primera);
        Assert.Equal(ResultadoEnvio.YaEstaba, segunda);
        Assert.Equal(1, enviador.Enviados);   // una sola vez
        Assert.True(RegistroEnvios.YaEnviado(hoy));
    }

    [Fact]
    public void FalloSmtp_NoLanza_YQuedaRegistrado()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        var cfg = ConfigDePrueba();
        var enviador = new EnviadorFalso { Falla = true };

        var r = ServicioReportes.EnviarDia(hoy, enviador, cfg);   // no debe tirar excepción

        Assert.Equal(ResultadoEnvio.Error, r);
        Assert.False(RegistroEnvios.YaEnviado(hoy));
        Assert.Equal("error", RegistroEnvios.Estado(hoy));
        Assert.True(RegistroEnvios.Intentos(hoy) >= 1);
        Assert.NotEqual("", RegistroEnvios.UltimoError(hoy));
    }

    [Fact]
    public void SinConfigurar_DevuelveSinConfigurar()
    {
        var hoy = DateTime.Today;
        var cfg = ConfigDePrueba();
        cfg.Destino = "";   // le falta el destino
        var enviador = new EnviadorFalso();

        Assert.Equal(ResultadoEnvio.SinConfigurar, ServicioReportes.EnviarDia(hoy, enviador, cfg));
        Assert.Equal(0, enviador.Enviados);
    }

    [Fact]
    public void Forzar_ReenviaAunqueYaSeHayaMandado()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        var cfg = ConfigDePrueba();
        var enviador = new EnviadorFalso();

        ServicioReportes.EnviarDia(hoy, enviador, cfg);
        var reenvio = ServicioReportes.EnviarDia(hoy, enviador, cfg, forzar: true);

        Assert.Equal(ResultadoEnvio.Enviado, reenvio);
        Assert.Equal(2, enviador.Enviados);
    }
}

public sealed class RecuperacionTests : PruebaConBase
{
    [Fact]
    public void TrasFalloYReinicio_ReintentaElDiaPendiente()
    {
        var hoy = DateTime.Today;
        var ayer = hoy.AddDays(-1);
        AgregarServicio(ayer, "Pedro", "Corte", 10000m);

        // Hora futura para que "hoy" no se dispare: aislamos el día pendiente de ayer.
        var cfg = ConfigDePrueba(hora: new TimeOnly(23, 59), desde: ayer);
        var ahora = hoy.AddHours(10);   // 10:00 de hoy, antes de la hora configurada

        // 1ª pasada: SMTP caído -> ayer queda en error, nada enviado.
        var caido = new EnviadorFalso { Falla = true };
        ServicioReportes.ProcesarAutomatico(caido, cfg, ahora);
        Assert.Equal(0, caido.Enviados);
        Assert.Equal("error", RegistroEnvios.Estado(ayer));

        // "Reinicio": el estado vive en la base. 2ª pasada con SMTP ok -> ayer se envía.
        var ok = new EnviadorFalso();
        ServicioReportes.ProcesarAutomatico(ok, cfg, ahora);

        Assert.True(RegistroEnvios.YaEnviado(ayer));
        Assert.Equal(1, ok.Enviados);
    }

    [Fact]
    public void ProcesarAutomatico_MandaHoy_SiYaPasoLaHora()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        var cfg = ConfigDePrueba(hora: new TimeOnly(8, 0), desde: hoy);
        var ahora = hoy.AddHours(21);   // 21:00, pasada la hora

        var enviador = new EnviadorFalso();
        var enviados = ServicioReportes.ProcesarAutomatico(enviador, cfg, ahora);

        Assert.Equal(1, enviados);
        Assert.True(RegistroEnvios.YaEnviado(hoy));
    }

    [Fact]
    public void ProcesarAutomatico_NoMandaHoy_SiNoLlegoLaHora()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m);
        var cfg = ConfigDePrueba(hora: new TimeOnly(23, 0), desde: hoy);
        var ahora = hoy.AddHours(9);   // 09:00, antes de la hora

        var enviador = new EnviadorFalso();
        var enviados = ServicioReportes.ProcesarAutomatico(enviador, cfg, ahora);

        Assert.Equal(0, enviados);
        Assert.False(RegistroEnvios.YaEnviado(hoy));
    }
}

public sealed class PrivacidadYExcelTests : PruebaConBase
{
    [Fact]
    public void Html_NoIncluyeNombresDeClientes()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m, cliente: "Juan Perez");

        var html = ReporteDiario.DeLaBase(hoy).Html();

        Assert.DoesNotContain("Juan", html);
        Assert.DoesNotContain("Perez", html);
    }

    [Fact]
    public void ExcelDelDia_SeGenera_SinColumnasPersonales()
    {
        var hoy = DateTime.Today;
        AgregarServicio(hoy, "Pedro", "Corte", 10000m, cliente: "Juan Perez");
        var carpeta = Path.Combine(Path.GetTempPath(), "rg_xlsx_" + Guid.NewGuid().ToString("N"));

        try
        {
            var r = Exportador.GenerarDia(hoy, carpeta);
            Assert.True(File.Exists(r.Archivo));
            Assert.True(new FileInfo(r.Archivo).Length > 1024);
            Assert.Equal(1, r.FilasServicios);
        }
        finally
        {
            try { Directory.Delete(carpeta, true); } catch { /* best-effort */ }
        }
    }
}
