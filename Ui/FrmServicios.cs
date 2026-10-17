using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>Libro diario: "Pedro hizo tal servicio por tal precio".
/// Layout tipo boceto (code.html): 3 tarjetas, barra de métricas y la tabla en tarjeta.</summary>
public sealed class FrmServicios : Form
{
    private readonly DateTimePicker _fecha = Tema.Fecha();
    private readonly ComboBox _profesional = Tema.Combo(150);
    private readonly ComboBox _servicio = Tema.Combo(165);
    private readonly TextBox _precio = Tema.Caja(110);
    private readonly ComboBox _medio = Tema.Combo(140);
    private readonly TextBox _cliente = Tema.Caja(150);
    private readonly TextBox _nota = Tema.Caja(150);

    private readonly ComboBox _periodo = Tema.Combo(130);
    private readonly DataGridView _grilla = Tema.Grilla();
    private readonly Label _total = new();

    public FrmServicios()
    {
        Tema.AplicarA(this);
        Text = "Servicios del día";

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24, 20, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // encabezado
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));   // 3 tarjetas
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));    // acciones
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));    // métricas
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // tabla

        raiz.Controls.Add(Encabezado(), 0, 0);
        raiz.Controls.Add(FilaTarjetas(), 0, 1);
        raiz.Controls.Add(BarraAcciones(), 0, 2);
        raiz.Controls.Add(BarraMetricas(), 0, 3);
        raiz.Controls.Add(TarjetaTabla(), 0, 4);

        Controls.Add(raiz);

        CargarCombos();
        _fecha.ValueChanged += (s, e) => Recargar();
        Recargar();
    }

    // ---------------------------------------------------------------- piezas

    private static Control Encabezado()
    {
        var cab = new Panel { Dock = DockStyle.Fill, AutoSize = true, BackColor = Tema.Fondo, Margin = new Padding(0, 0, 0, 14) };

        var ruta = new Label
        {
            Text = "RETRO GREEN GESTIÓN   /   OPERACIONES DIARIAS",
            Font = Tema.Normal(8f, FontStyle.Bold),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Location = new Point(2, 0)
        };
        var titulo = Tema.Encabezado("Cargar un servicio", 24);
        titulo.Location = new Point(0, 18);

        cab.Controls.Add(titulo);
        cab.Controls.Add(ruta);
        cab.Height = 64;
        return cab;
    }

    private Control FilaTarjetas()
    {
        var fila = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Tema.Fondo,
            Margin = new Padding(0, 0, 0, 0)
        };
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        fila.Controls.Add(Tarjeta("Paso 01 · Datos del servicio",
            ("Fecha", _fecha), ("Profesional", _profesional), ("Servicio principal", _servicio)), 0, 0);
        fila.Controls.Add(Tarjeta("Paso 02 · Cobro y arancel",
            ("Precio cobrado ($ ARS)", _precio), ("Medio de pago", _medio)), 1, 0);
        fila.Controls.Add(Tarjeta("Paso 03 · Cliente & extra",
            ("Nombre del cliente", _cliente), ("Detalle / nota interna", _nota)), 2, 0);
        return fila;
    }

    private static PanelRedondo Tarjeta(string titulo, params (string et, Control ctrl)[] campos)
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0, 0, 14, 0)
        };

        var tl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = Tema.Panel,
            AutoScroll = false
        };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        tl.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        foreach (var (et, ctrl) in campos)
        {
            tl.Controls.Add(new Label
            {
                Text = et.ToUpperInvariant(),
                Font = Tema.Normal(7.75f, FontStyle.Bold),
                ForeColor = Tema.Apagado,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            });
            ctrl.Dock = DockStyle.Fill;
            ctrl.Margin = new Padding(0, 0, 0, 8);
            tl.Controls.Add(ctrl);
        }

        card.Controls.Add(tl);
        return card;
    }

    private Control BarraAcciones()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 12,
            ColorBorde = Tema.Borde,
            BackColor = Tema.Panel,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 14, 0, 0)
        };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Panel,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };

        var agregar = Tema.Boton("Agregar servicio", primario: true);
        agregar.Width = 230;
        agregar.Margin = new Padding(0, 4, 10, 4);
        agregar.Click += (s, e) => Agregar();

        var anular = Tema.BotonPeligro("Anular el seleccionado");
        anular.Width = 230;
        anular.Margin = new Padding(0, 4, 10, 4);
        anular.Click += (s, e) => Anular();

        var limpiar = Tema.Boton("Limpiar");
        limpiar.Width = 130;
        limpiar.Margin = new Padding(0, 4, 0, 4);
        limpiar.Click += (s, e) => LimpiarFormulario();

        flow.Controls.Add(agregar);
        flow.Controls.Add(anular);
        flow.Controls.Add(limpiar);
        card.Controls.Add(flow);
        return card;
    }

    private Control BarraMetricas()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 12,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(18, 12, 18, 12),
            Margin = new Padding(0, 14, 0, 0)
        };

        _total.Font = Tema.Titulo(20f);
        _total.ForeColor = Tema.Verde;
        _total.AutoSize = true;
        _total.Location = new Point(18, 30);
        card.Controls.Add(_total);

        var etiqueta = new Label
        {
            Text = "RESUMEN DEL TURNO",
            Font = Tema.Normal(7.75f, FontStyle.Bold),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(18, 12)
        };
        card.Controls.Add(etiqueta);

        card.Controls.Add(new Label
        {
            Text = "PERÍODO",
            Font = Tema.Normal(7.75f, FontStyle.Bold),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(card.Width - 160, 14)
        });

        _periodo.Items.AddRange(new object[] { "Día", "Semana", "Mes" });
        _periodo.SelectedIndex = 0;
        _periodo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _periodo.Location = new Point(card.Width - 160, 34);
        _periodo.SelectedIndexChanged += (s, e) => Recargar();
        card.Controls.Add(_periodo);

        return card;
    }

    private Control TarjetaTabla()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Tema.Borde,
            BackColor = Tema.Panel,
            Padding = new Padding(16, 14, 16, 16),
            Margin = new Padding(0, 14, 0, 0)
        };

        var titulo = new Label
        {
            Text = "DETALLE DE SERVICIOS REGISTRADOS",
            Font = Tema.Titulo(11f),
            ForeColor = Tema.Texto,
            Dock = DockStyle.Top,
            Height = 28
        };

        _grilla.Dock = DockStyle.Fill;
        card.Controls.Add(_grilla);
        card.Controls.Add(titulo);
        return card;
    }

    // ---------------------------------------------------------------- lógica

    private void CargarCombos()
    {
        _profesional.DataSource = Repo.Profesionales();
        _medio.DataSource = Repo.MediosPago();

        // El handler se engancha ANTES de asignar el origen: así el precio sugerido
        // se completa solo al abrir y cada vez que se cambia el servicio.
        _servicio.SelectedIndexChanged += (s, e) =>
        {
            if (_servicio.SelectedItem is Item it)
                _precio.Text = Bd.APesos(it.Cent).ToString("0.##");
        };
        _servicio.DataSource = Repo.Servicios();
    }

    private void Agregar()
    {
        if (_profesional.SelectedItem is not Item prof ||
            _servicio.SelectedItem is not Item serv ||
            _medio.SelectedItem is not Item medio)
        {
            Tema.Error("Faltan cargar los catálogos (profesionales, servicios o medios de pago).");
            return;
        }

        if (!decimal.TryParse(_precio.Text.Replace("$", "").Trim(), out var precio) || precio < 0)
        {
            Tema.Error("El precio no es un número válido.");
            _precio.Focus();
            return;
        }

        Repo.AgregarServicio(_fecha.Value.Date, prof.Id, serv.Id, serv.Nombre,
            Bd.ACentavos(precio), medio.Id, _cliente.Text.Trim(), _nota.Text.Trim());

        _cliente.Clear();
        _nota.Clear();
        Recargar();
        _profesional.Focus();
    }

    /// <summary>Reinicia el formulario de carga para empezar de cero.</summary>
    private void LimpiarFormulario()
    {
        _cliente.Clear();
        _nota.Clear();
        if (_fecha.Value.Date != DateTime.Today) _fecha.Value = DateTime.Today;
        if (_servicio.SelectedItem is Item it)
            _precio.Text = Bd.APesos(it.Cent).ToString("0.##");
        _grilla.ClearSelection();
        _profesional.Focus();
    }

    private void Anular()
    {
        if (_grilla.CurrentRow?.DataBoundItem is not DataRowView fila)
        {
            Tema.Error("Elegí primero una fila de la lista.");
            return;
        }

        var desc = $"{fila["Servicio"]} de {fila["Profesional"]} por {fila["Precio"]:C2}";
        if (!Autorizacion.PedirAdmin(this,
                $"Vas a ANULAR este servicio:\n{desc}\n(No se borra: queda marcado como anulado y deja de sumar.)"))
            return;

        Repo.AnularServicio(Convert.ToInt64(fila["Id"]));
        Recargar();
    }

    private void Recargar()
    {
        var d = _fecha.Value.Date;
        DateTime desde, hasta;
        string que;
        switch (_periodo.SelectedItem?.ToString())
        {
            case "Semana":
                var diff = ((int)d.DayOfWeek + 6) % 7;   // lunes como inicio de semana
                desde = d.AddDays(-diff);
                hasta = desde.AddDays(6);
                que = "de la semana";
                break;
            case "Mes":
                desde = new DateTime(d.Year, d.Month, 1);
                hasta = desde.AddMonths(1).AddDays(-1);
                que = "del mes";
                break;
            default:
                desde = hasta = d;
                que = "del día";
                break;
        }

        var datos = Repo.ListarServiciosRango(desde, hasta);

        _grilla.DataSource = datos;
        if (_grilla.Columns["Id"] is { } col) col.Visible = false;
        if (_grilla.Columns["Precio"] is { } p)
        {
            p.DefaultCellStyle.Format = "C2";
            p.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        var suma = datos.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["Precio"]));
        _total.Text = $"{datos.Rows.Count} servicios {que}   ·   {suma:C2}";
    }
}
