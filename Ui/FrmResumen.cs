using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// El mismo panel que tiene hoy en la planilla de Google, pero sin formulas
/// que se rompan y sin #DIV/0!.
/// </summary>
public sealed class FrmResumen : Form
{
    private readonly ComboBox _periodo = Tema.Combo(160);
    private readonly Panel _tIngresos = Tema.Tarjeta("Total ingresos", "$0,00", Tema.Verde);
    private readonly Panel _tEgresos = Tema.Tarjeta("Total egresos", "$0,00", Tema.Violeta);
    private readonly Panel _tNeto = Tema.Tarjeta("Resultado neto", "$0,00");
    private readonly Panel _tCantidad = Tema.Tarjeta("Servicios realizados", "0");
    private readonly Panel _tSemana = Tema.TarjetaKpi("Ganancia semanal", "$0,00", Tema.Verde);
    private readonly Panel _tTop = Tema.TarjetaKpi("Servicio más vendido", "—");

    private readonly DataGridView _profesionales = Tema.Grilla();
    private readonly DataGridView _medios = Tema.Grilla();
    private readonly DataGridView _gastos = Tema.Grilla();
    private readonly DataGridView _historico = Tema.Grilla();

    public FrmResumen()
    {
        Tema.AplicarA(this);
        Text = "Resumen mensual";
        AutoScroll = true;

        var cabecera = new Panel { Dock = DockStyle.Top, Height = 364, BackColor = Tema.Fondo };

        var titulo = Tema.Encabezado("Panel de control mensual", 17);
        titulo.Location = new Point(24, 18);
        cabecera.Controls.Add(titulo);

        var campoPeriodo = Tema.Campo("Mes", _periodo);
        campoPeriodo.Location = new Point(24, 56);
        cabecera.Controls.Add(campoPeriodo);

        // Ancho fijo para 4 tarjetas: así ingresos/egresos/neto/servicios quedan
        // en la primera fila y ganancia semanal + servicio más vendido bajan debajo.
        var tarjetas = new FlowLayoutPanel
        {
            Location = new Point(24, 118),
            Size = new Size(1012, 244),
            WrapContents = true,
            AutoScroll = false
        };
        tarjetas.Controls.AddRange(new Control[]
            { _tIngresos, _tEgresos, _tNeto, _tCantidad, _tSemana, _tTop });
        cabecera.Controls.Add(tarjetas);

        var cuerpo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(24, 10, 24, 24),
            BackColor = Tema.Fondo
        };
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        cuerpo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        cuerpo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        cuerpo.Controls.Add(Bloque("Rendimiento por profesional", _profesionales), 0, 0);
        cuerpo.Controls.Add(Bloque("Gastos por categoría", _gastos), 1, 0);
        cuerpo.Controls.Add(Bloque("Ingresos por medio de pago", _medios), 0, 1);
        cuerpo.Controls.Add(Bloque("Histórico (meses cerrados)", _historico), 1, 1);

        Controls.Add(cuerpo);
        Controls.Add(cabecera);

        _periodo.DataSource = Repo.PeriodosConDatos();
        _periodo.SelectedIndexChanged += (s, e) => Recargar();
        Recargar();
    }

    private static Panel Bloque(string titulo, DataGridView grilla)
    {
        var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 16) };
        var l = new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Tema.Titulo(11),
            ForeColor = Tema.Verde,
            Dock = DockStyle.Top,
            Height = 26
        };
        p.Controls.Add(grilla);
        p.Controls.Add(l);
        return p;
    }

    private void Recargar()
    {
        if (_periodo.SelectedItem is not string periodo) return;

        var t = Repo.Totales(periodo);
        Tema.ValorTarjeta(_tIngresos, Bd.Plata(t.IngresosCent));
        Tema.ValorTarjeta(_tEgresos, Bd.Plata(t.EgresosCent));
        Tema.ValorTarjeta(_tNeto, Bd.Plata(t.NetoCent));
        Tema.ValorTarjeta(_tCantidad, t.CantServicios.ToString());

        if (_tNeto.Controls["valor"] is Label neto)
            neto.ForeColor = t.NetoCent >= 0 ? Tema.Verde : Tema.Rojo;

        // Ganancia de los ultimos 7 dias (no depende del mes elegido).
        var (ingSem, egrSem) = Repo.TotalesSemana();
        var netoSem = ingSem - egrSem;
        Tema.ValorTarjeta(_tSemana, Bd.Plata(netoSem));
        Tema.PieTarjeta(_tSemana, "Últimos 7 días");
        if (_tSemana.Controls["valor"] is Label gs)
            gs.ForeColor = netoSem >= 0 ? Tema.Verde : Tema.Rojo;

        // Servicio mas vendido del mes elegido.
        var top = Repo.ServicioMasVendido(periodo);
        Tema.ValorTarjeta(_tTop, top?.nombre ?? "—");
        Tema.PieTarjeta(_tTop, top is { } tp ? $"{tp.veces} veces este mes" : "Sin datos");

        Poner(_profesionales, Repo.PorProfesional(periodo), "Total", "Comisión");
        Poner(_medios, Repo.PorMedioPago(periodo), "Total");
        Poner(_gastos, Repo.PorCategoria(periodo), "Total");
        Poner(_historico, Repo.Historico(), "Ingresos", "Egresos", "Neto");
    }

    private static void Poner(DataGridView g, System.Data.DataTable datos, params string[] plata)
    {
        g.DataSource = datos;
        foreach (var c in plata)
            if (g.Columns[c] is { } col) col.DefaultCellStyle.Format = "C2";
        if (g.Columns["% Comisión"] is { } pc) pc.DefaultCellStyle.Format = "0.##'%'";
        if (g.Columns["% del Total"] is { } pt)
        {
            pt.DefaultCellStyle.Format = "0.#'%'";
            pt.DefaultCellStyle.ForeColor = Tema.Verde;
        }
    }
}
