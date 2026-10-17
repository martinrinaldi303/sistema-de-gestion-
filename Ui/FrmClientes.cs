using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Buscador de clientes: una lupa para encontrar a los clientes atendidos y ver
/// su historial y preferencias (servicio y profesional habitual, notas).
/// </summary>
public sealed class FrmClientes : Form
{
    private readonly TextBox _busqueda = Tema.Caja(360);
    private readonly DataGridView _clientes = Tema.Grilla();
    private readonly DataGridView _historial = Tema.Grilla();
    private readonly Label _preferencias = new();

    public FrmClientes()
    {
        Tema.AplicarA(this);
        Text = "Clientes";

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 20, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));   // encabezado
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // barra de búsqueda
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // resultados

        raiz.Controls.Add(Tema.CabeceraPantalla("Clientes", "Atención"), 0, 0);
        raiz.Controls.Add(BarraBusqueda(), 0, 1);
        raiz.Controls.Add(Cuerpo(), 0, 2);

        Controls.Add(raiz);

        _clientes.SelectionChanged += (s, e) => MostrarHistorial();
        Buscar();   // arranca mostrando todos
    }

    private Control BarraBusqueda()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 12,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(14, 8, 14, 8)
        };

        var fila = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Tema.Panel, WrapContents = false };

        var lupa = new Label
        {
            Text = "🔍",
            Font = Tema.Normal(13f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 6, 6, 0)
        };

        _busqueda.Font = Tema.Normal(11f);
        _busqueda.Margin = new Padding(0, 3, 10, 0);
        _busqueda.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Buscar(); } };
        _busqueda.TextChanged += (s, e) => Buscar();   // búsqueda en vivo a medida que se escribe

        var boton = Tema.Boton("Buscar", primario: true);
        boton.Width = 120;
        boton.Margin = new Padding(0, 1, 0, 0);
        boton.Click += (s, e) => Buscar();

        fila.Controls.Add(lupa);
        fila.Controls.Add(_busqueda);
        fila.Controls.Add(boton);
        card.Controls.Add(fila);
        return card;
    }

    private Control Cuerpo()
    {
        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 14, 0, 0)
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        tabla.Controls.Add(Tema.TarjetaContenedor("Clientes encontrados", _clientes), 0, 0);

        // Columna derecha: preferencias arriba, historial abajo.
        var der = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(14, 0, 0, 0)
        };
        der.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        der.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        der.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        der.Controls.Add(TarjetaPreferencias(), 0, 0);
        der.Controls.Add(Tema.TarjetaContenedor("Historial del cliente", _historial), 0, 1);

        tabla.Controls.Add(der, 1, 0);
        return tabla;
    }

    private Control TarjetaPreferencias()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(16, 12, 16, 12)
        };

        _preferencias.Dock = DockStyle.Fill;
        _preferencias.Font = Tema.Normal(9.75f);
        _preferencias.ForeColor = Tema.Texto;
        _preferencias.Text = "Elegí un cliente de la lista para ver sus preferencias.";

        var titulo = new Label
        {
            Text = "PREFERENCIAS",
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            Dock = DockStyle.Top,
            Height = 24
        };

        card.Controls.Add(_preferencias);
        card.Controls.Add(titulo);
        return card;
    }

    private void Buscar()
    {
        var datos = Repo.BuscarClientes(_busqueda.Text);
        _clientes.DataSource = datos;
        if (_clientes.Columns["Gastado"] is { } g)
        {
            g.DefaultCellStyle.Format = "C2";
            g.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        if (datos.Rows.Count == 0)
        {
            _historial.DataSource = null;
            _preferencias.Text = _busqueda.Text.Trim().Length == 0
                ? "Todavía no hay clientes con nombre cargado.\n" +
                  "Se cargan en \"Nombre del cliente\" al registrar un servicio."
                : "No se encontraron clientes con ese nombre.";
        }
        else MostrarHistorial();
    }

    private void MostrarHistorial()
    {
        if (_clientes.CurrentRow?.Cells["Cliente"].Value is not string cliente) return;

        var dt = Repo.HistorialCliente(cliente);
        _historial.DataSource = dt;
        if (_historial.Columns["Precio"] is { } p)
        {
            p.DefaultCellStyle.Format = "C2";
            p.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        string Moda(string col) => dt.Rows.Cast<DataRow>()
            .Select(r => r[col].ToString()!.Trim())
            .Where(v => v.Length > 0)
            .GroupBy(v => v)
            .OrderByDescending(gr => gr.Count())
            .Select(gr => gr.Key)
            .FirstOrDefault() ?? "—";

        var notas = dt.Rows.Cast<DataRow>()
            .Select(r => r["Nota"].ToString()!.Trim())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();
        var total = dt.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["Precio"]));

        _preferencias.Text =
            $"Cliente: {cliente}\n" +
            $"Servicio habitual: {Moda("Servicio")}      Profesional habitual: {Moda("Profesional")}\n" +
            $"Visitas: {dt.Rows.Count}      Gastado: {total:C2}\n" +
            "Notas / preferencias: " + (notas.Count > 0 ? string.Join("  ·  ", notas) : "(sin notas)");
    }
}
