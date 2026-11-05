using RetroGreenGestion.Datos;
using Xunit;

namespace RetroGreenGestion.Tests;

public sealed class StockTests : PruebaConBase
{
    private long MedioEfectivo() => Asegurar("MediosPago", "Efectivo");

    [Fact]
    public void AltaConStockInicial_DejaElStockYunMovimiento()
    {
        var id = Repo.AgregarProducto("Shampoo", "SH1", Bd.ACentavos(5000m), Bd.ACentavos(2000m), 10, 3);

        Assert.Equal(10, StockDe(id));
        Assert.True(Repo.MovimientosProducto(id).Rows.Count >= 1);
    }

    [Fact]
    public void EntradaYSalida_AjustanElStock()
    {
        var id = Repo.AgregarProducto("Cera", "", Bd.ACentavos(3000m), Bd.ACentavos(1000m), 0, 2);
        Repo.RegistrarMovimiento(id, "entrada", 8, "compra");
        Repo.RegistrarMovimiento(id, "salida", -3, "uso");

        Assert.Equal(5, StockDe(id));
    }

    [Fact]
    public void Venta_DescuentaStock_YSumaALaCaja()
    {
        var hoy = DateTime.Today;
        var id = Repo.AgregarProducto("Gel", "", Bd.ACentavos(4000m), Bd.ACentavos(1500m), 10, 2);

        Repo.RegistrarVentaProducto(id, 3, Bd.ACentavos(12000m), MedioEfectivo());

        Assert.Equal(7, StockDe(id));                                   // 10 - 3
        var (cant, ing) = Repo.VentasProductoDia(hoy);
        Assert.Equal(1, cant);
        Assert.Equal(Bd.ACentavos(12000m), ing);
    }

    [Fact]
    public void Venta_EntraEnLosTotalesDelMes_JuntoAlosServicios()
    {
        var hoy = DateTime.Today;
        var periodo = Repo.Periodo(hoy);
        AgregarServicio(hoy, "Ana", "Corte", 10000m);
        var id = Repo.AgregarProducto("Pomada", "", Bd.ACentavos(5000m), Bd.ACentavos(2000m), 5, 1);
        Repo.RegistrarVentaProducto(id, 1, Bd.ACentavos(5000m), MedioEfectivo());

        var t = Repo.Totales(periodo);
        Assert.Equal(Bd.ACentavos(15000m), t.IngresosCent);            // servicio + producto

        // y el medio de pago agrupa ambos
        var medios = Repo.PorMedioPago(periodo);
        var efectivo = medios.Rows.Cast<System.Data.DataRow>().First(r => (string)r["Medio de pago"] == "Efectivo");
        Assert.Equal(15000.0, Convert.ToDouble(efectivo["Total"]));    // $15.000 en pesos (servicio + producto)
    }

    [Fact]
    public void BajoMinimo_DetectaLosQueCaenAlLimite()
    {
        var id = Repo.AgregarProducto("Tijera", "", Bd.ACentavos(9000m), Bd.ACentavos(4000m), 2, 3);
        // stock 2 <= mínimo 3 -> aparece
        var bajo = Repo.ProductosBajoMinimo();
        Assert.Contains(bajo.Rows.Cast<System.Data.DataRow>(), r => (string)r["Producto"] == "Tijera");

        Repo.RegistrarMovimiento(id, "entrada", 5, "compra");          // ahora 7 > 3
        Assert.DoesNotContain(Repo.ProductosBajoMinimo().Rows.Cast<System.Data.DataRow>(),
            r => (string)r["Producto"] == "Tijera");
    }

    [Fact]
    public void ValorInventario_EsStockPorCosto()
    {
        Repo.AgregarProducto("A", "", Bd.ACentavos(0m), Bd.ACentavos(1000m), 4, 0);  // 4 * $1000 = $4000
        Repo.AgregarProducto("B", "", Bd.ACentavos(0m), Bd.ACentavos(2500m), 2, 0);  // 2 * $2500 = $5000
        Assert.Equal(Bd.ACentavos(9000m), Repo.ValorInventarioCent());
    }

    private static long StockDe(long id)
        => Bd.EscalarLong("SELECT stock_actual FROM Productos WHERE id = $id;", ("$id", id));
}
