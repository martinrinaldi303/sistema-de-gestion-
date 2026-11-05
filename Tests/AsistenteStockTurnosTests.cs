using RetroGreenGestion.Datos;
using RetroGreenGestion.Ui;
using Xunit;

namespace RetroGreenGestion.Tests;

/// <summary>El asistente también responde sobre stock y turnos.</summary>
public sealed class AsistenteStockTurnosTests : PruebaConBase
{
    private static readonly DateTime Hoy = new(2026, 11, 10);

    [Fact]
    public void Stock_QueFaltaReponer_ListaBajoMinimo_NoSeConfundeConCliente()
    {
        Repo.AgregarProducto("Shampoo", "", Bd.ACentavos(5000m), Bd.ACentavos(2000m), 1, 3); // 1 <= mínimo 3

        var r = Asistente.Responder("qué falta reponer", Hoy);

        Assert.Contains("Shampoo", r.Texto);
        Assert.DoesNotContain("No encontré ningún cliente", r.Texto);
    }

    [Fact]
    public void Stock_CuantoQuedaDeUnProducto()
    {
        Repo.AgregarProducto("Cera", "", Bd.ACentavos(3000m), Bd.ACentavos(1000m), 7, 2);

        var r = Asistente.Responder("cuánto queda de cera", Hoy);

        Assert.Contains("7", r.Texto);
        Assert.Contains("Cera", r.Texto);
    }

    [Fact]
    public void Stock_ValorInventario()
    {
        Repo.AgregarProducto("Aceite", "", Bd.ACentavos(0m), Bd.ACentavos(1000m), 4, 0);

        var r = Asistente.Responder("valor del inventario", Hoy);

        Assert.Contains("inventario", r.Texto.ToLowerInvariant());
    }

    [Fact]
    public void Turnos_CuantosHoy()
    {
        Repo.AgregarTurno(Hoy, "10:00", "Juan", "", null, null, 30, "");
        Repo.AgregarTurno(Hoy, "11:00", "Ana", "", null, null, 30, "");

        var r = Asistente.Responder("cuántos turnos hay hoy", Hoy);

        Assert.Contains("2 turnos", r.Texto);
    }

    [Fact]
    public void Turnos_Proximos()
    {
        Repo.AgregarTurno(DateTime.Today.AddDays(2), "09:00", "Pedro", "", null, null, 30, "");

        var r = Asistente.Responder("próximos turnos", Hoy);

        Assert.Contains("Pedro", r.Texto);
    }
}
