using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>Egresos del mes, con el mismo layout de tarjetas del boceto.</summary>
public sealed class FrmEgresos : Form
{
    private readonly DateTimePicker _fecha = Tema.Fecha();
    private readonly ComboBox _categoria = Tema.Combo(210);
    private readonly TextBox _descripcion = Tema.Caja(260);
    private readonly TextBox _monto = Tema.Caja(130);
    private readonly ComboBox _medio = Tema.Combo(150);

    private readonly DataGridView _grilla = Tema.Grilla();
    private readonly Label _total = new();

    public FrmEgresos()
    {
        Tema.AplicarA(this);
        Text = "Egresos";

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24, 20, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(Tema.CabeceraPantalla("Cargar un gasto"), 0, 0);
        raiz.Controls.Add(Tarjetas(), 0, 1);
        raiz.Controls.Add(Acciones(), 0, 2);
        raiz.Controls.Add(Metricas(), 0, 3);
        raiz.Controls.Add(Tema.TarjetaContenedor("Detalle de gastos del mes", _grilla), 0, 4);

        Controls.Add(raiz);

        _categoria.DataSource = Repo.CategoriasGasto();
        _medio.DataSource = Repo.MediosPago();
        _fecha.ValueChanged += (s, e) => Recargar();
        Recargar();
    }

    private Control Tarjetas()
    {
        var fila = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Tema.Fondo
        };
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        fila.Controls.Add(Tema.TarjetaCampos("Datos del gasto",
            ("Fecha", _fecha), ("Categoría", _categoria), ("Descripción", _descripcion)), 0, 0);
        var segunda = Tema.TarjetaCampos("Importe y pago",
            ("Monto ($ ARS)", _monto), ("Medio de pago", _medio));
        segunda.Margin = new Padding(0, 0, 0, 0);
        fila.Controls.Add(segunda, 1, 0);
        return fila;
    }

    private Control Acciones()
    {
        var agregar = Tema.Boton("Agregar gasto", primario: true);
        agregar.Width = 210;
        agregar.Click += (s, e) => Agregar();

        var anular = Tema.BotonPeligro("Anular el seleccionado");
        anular.Width = 230;
        anular.Click += (s, e) => Anular();

        return Tema.BarraCard(agregar, anular);
    }

    private Control Metricas()
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
        card.Controls.Add(new Label
        {
            Text = "TOTAL DE EGRESOS DEL MES",
            Font = Tema.Normal(7.75f, FontStyle.Bold),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(18, 12)
        });
        _total.Font = Tema.Titulo(20f);
        _total.ForeColor = Tema.Violeta;
        _total.AutoSize = true;
        _total.Location = new Point(18, 30);
        card.Controls.Add(_total);
        return card;
    }

    private void Agregar()
    {
        if (_categoria.SelectedItem is not Item cat || _medio.SelectedItem is not Item medio)
        {
            Tema.Error("Faltan cargar los catálogos de categorías o medios de pago.");
            return;
        }
        if (_descripcion.Text.Trim().Length == 0)
        {
            Tema.Error("Poné una descripción, aunque sea corta.");
            _descripcion.Focus();
            return;
        }
        if (!decimal.TryParse(_monto.Text.Replace("$", "").Trim(), out var monto) || monto <= 0)
        {
            Tema.Error("El monto no es un número válido.");
            _monto.Focus();
            return;
        }

        Repo.AgregarEgreso(_fecha.Value.Date, cat.Id, _descripcion.Text.Trim(), "",
            Bd.ACentavos(monto), medio.Id);

        _descripcion.Clear();
        _monto.Clear();
        Recargar();
        _categoria.Focus();
    }

    private void Anular()
    {
        if (_grilla.CurrentRow?.DataBoundItem is not DataRowView fila)
        {
            Tema.Error("Elegí primero una fila de la lista.");
            return;
        }
        if (!Tema.Confirmar($"¿Anular este gasto?\n\n{fila["Descripcion"]} - {fila["Monto"]:C2}"))
            return;

        Repo.AnularEgreso(Convert.ToInt64(fila["Id"]));
        Recargar();
    }

    private void Recargar()
    {
        var datos = Repo.ListarEgresos(Repo.Periodo(_fecha.Value));
        _grilla.DataSource = datos;
        if (_grilla.Columns["Id"] is { } col) col.Visible = false;
        if (_grilla.Columns["Monto"] is { } m)
        {
            m.DefaultCellStyle.Format = "C2";
            m.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        var suma = datos.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["Monto"]));
        _total.Text = $"{datos.Rows.Count} gastos del mes   ·   {suma:C2}";
    }
}
