using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

public sealed class FrmCatalogos : Form
{
    public FrmCatalogos()
    {
        Tema.AplicarA(this);
        Text = "Catálogos";

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 20, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(Tema.CabeceraPantalla("Catálogos", "Configuración"), 0, 0);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 8),
            Font = Tema.Normal(10)
        };
        tabs.TabPages.Add(Pestaña("Servicios", new PanelCatalogo(
            "Servicios", "Precio sugerido", PanelCatalogo.TipoExtra.Plata)));
        tabs.TabPages.Add(Pestaña("Profesionales", new PanelCatalogo(
            "Profesionales", "% de comisión", PanelCatalogo.TipoExtra.Porcentaje)));
        tabs.TabPages.Add(Pestaña("Medios de pago", new PanelCatalogo(
            "MediosPago", "", PanelCatalogo.TipoExtra.Ninguno)));
        tabs.TabPages.Add(Pestaña("Categorías de gasto", new PanelCatalogo(
            "CategoriasGasto", "", PanelCatalogo.TipoExtra.Ninguno)));

        raiz.Controls.Add(tabs, 0, 1);
        Controls.Add(raiz);
    }

    private static TabPage Pestaña(string nombre, Control contenido)
    {
        var p = new TabPage(nombre) { BackColor = Tema.Fondo, Padding = new Padding(0, 14, 0, 0) };
        contenido.Dock = DockStyle.Fill;
        p.Controls.Add(contenido);
        return p;
    }
}

/// <summary>Alta, edición y baja lógica de un catálogo simple, con layout de tarjetas.</summary>
public sealed class PanelCatalogo : UserControl
{
    public enum TipoExtra { Ninguno, Plata, Porcentaje }

    private readonly string _tabla;
    private readonly TipoExtra _tipo;
    private readonly string _columnaExtra;

    private readonly TextBox _nombre = Tema.Caja(260);
    private readonly TextBox _extra = Tema.Caja(140);
    private readonly CheckBox _verInactivos = new();
    private readonly DataGridView _grilla = Tema.Grilla();

    public PanelCatalogo(string tabla, string etiquetaExtra, TipoExtra tipo)
    {
        _tabla = tabla;
        _tipo = tipo;
        _columnaExtra = tipo switch
        {
            TipoExtra.Plata => "precio_cent",
            TipoExtra.Porcentaje => "comision_porcentaje",
            _ => ""
        };

        BackColor = Tema.Fondo;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 2
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(TarjetaAlta(etiquetaExtra), 0, 0);
        raiz.Controls.Add(Tema.TarjetaContenedor("Listado", _grilla), 0, 1);

        _grilla.SelectionChanged += (s, e) => LlevarAlFormulario();

        Controls.Add(raiz);
        Recargar();
    }

    private Control TarjetaAlta(string etiquetaExtra)
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Radio = 14,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(18, 16, 18, 16)
        };

        // Pila vertical que crece con su contenido: asi NO se cortan los campos
        // ni los botones (antes la tarjeta colapsaba y los ocultaba).
        var tl = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Tema.Panel
        };

        tl.Controls.Add(new Label
        {
            Text = "ALTA / EDICIÓN",
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });
        tl.Controls.Add(new Label
        {
            Text = "Nada se borra: se desactiva, así los registros viejos siguen teniendo sentido.",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        var filaCampos = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel };
        filaCampos.Controls.Add(Tema.Campo("Nombre", _nombre));
        if (_tipo != TipoExtra.Ninguno) filaCampos.Controls.Add(Tema.Campo(etiquetaExtra, _extra));
        tl.Controls.Add(filaCampos);

        var agregar = Tema.Boton("Agregar", primario: true);
        agregar.Width = 150;
        agregar.Click += (s, e) => Agregar();

        var guardar = Tema.Boton("Guardar cambios del seleccionado");
        guardar.Width = 300;
        guardar.Click += (s, e) => GuardarSeleccionado();

        var alternar = Tema.Boton("Activar / Desactivar");
        alternar.Width = 200;
        alternar.Click += (s, e) => Alternar();

        _verInactivos.Text = "Ver también los desactivados";
        _verInactivos.ForeColor = Tema.Apagado;
        _verInactivos.Font = Tema.Normal(9.5f);
        _verInactivos.AutoSize = true;
        _verInactivos.Margin = new Padding(8, 12, 0, 0);
        _verInactivos.CheckedChanged += (s, e) => Recargar();

        var filaBotones = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Tema.Panel,
            Margin = new Padding(0, 10, 0, 0)
        };
        foreach (Control b in new Control[] { agregar, guardar, alternar })
            b.Margin = new Padding(0, 4, 10, 4);
        filaBotones.Controls.Add(agregar);
        filaBotones.Controls.Add(guardar);
        filaBotones.Controls.Add(alternar);
        filaBotones.Controls.Add(_verInactivos);
        tl.Controls.Add(filaBotones);

        card.Controls.Add(tl);
        return card;
    }

    private string SelectSql()
    {
        var extra = _tipo switch
        {
            TipoExtra.Plata => ", precio_cent/100.0 AS \"Precio sugerido\"",
            TipoExtra.Porcentaje => ", comision_porcentaje AS \"% Comisión\"",
            _ => ""
        };
        var filtro = _verInactivos.Checked ? "" : " WHERE activo = 1";
        return $"SELECT id AS Id, nombre AS Nombre{extra}, " +
               $"CASE activo WHEN 1 THEN 'Sí' ELSE 'No' END AS Activo " +
               $"FROM {_tabla}{filtro} ORDER BY activo DESC, nombre;";
    }

    private void Recargar()
    {
        _grilla.DataSource = Bd.Tabla(SelectSql());
        if (_grilla.Columns["Id"] is { } c) c.Visible = false;
        if (_grilla.Columns["Precio sugerido"] is { } p)
        {
            p.DefaultCellStyle.Format = "C2";
            p.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }

    private void LlevarAlFormulario()
    {
        if (_grilla.CurrentRow?.DataBoundItem is not DataRowView f) return;
        _nombre.Text = f["Nombre"].ToString();
        if (_tipo == TipoExtra.Plata) _extra.Text = Convert.ToDecimal(f["Precio sugerido"]).ToString("0.##");
        if (_tipo == TipoExtra.Porcentaje) _extra.Text = Convert.ToDecimal(f["% Comisión"]).ToString("0.##");
    }

    private bool LeerExtra(out object valor)
    {
        valor = 0;
        if (_tipo == TipoExtra.Ninguno) return true;

        var texto = _extra.Text.Replace("$", "").Replace("%", "").Trim();
        if (texto.Length == 0) { valor = 0; return true; }

        if (!decimal.TryParse(texto, out var n) || n < 0)
        {
            Tema.Error("Ese valor no es un número válido.");
            return false;
        }
        valor = _tipo == TipoExtra.Plata ? Bd.ACentavos(n) : (double)n;
        return true;
    }

    private void Agregar()
    {
        var nombre = _nombre.Text.Trim();
        if (nombre.Length == 0) { Tema.Error("Falta el nombre."); _nombre.Focus(); return; }
        if (!LeerExtra(out var extra)) return;

        try
        {
            if (_tipo == TipoExtra.Ninguno)
                Bd.Ejecutar($"INSERT INTO {_tabla} (nombre) VALUES ($n);", ("$n", nombre));
            else
                Bd.Ejecutar($"INSERT INTO {_tabla} (nombre, {_columnaExtra}) VALUES ($n, $e);",
                    ("$n", nombre), ("$e", extra));

            _nombre.Clear();
            _extra.Clear();
            Recargar();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            Tema.Error($"Ya existe algo con el nombre \"{nombre}\".");
        }
    }

    private void GuardarSeleccionado()
    {
        if (_grilla.CurrentRow?.DataBoundItem is not DataRowView f)
        {
            Tema.Error("Elegí primero una fila de la lista.");
            return;
        }
        var nombre = _nombre.Text.Trim();
        if (nombre.Length == 0) { Tema.Error("Falta el nombre."); return; }
        if (!LeerExtra(out var extra)) return;

        var id = Convert.ToInt64(f["Id"]);
        if (_tipo == TipoExtra.Ninguno)
            Bd.Ejecutar($"UPDATE {_tabla} SET nombre = $n WHERE id = $id;",
                ("$n", nombre), ("$id", id));
        else
            Bd.Ejecutar($"UPDATE {_tabla} SET nombre = $n, {_columnaExtra} = $e WHERE id = $id;",
                ("$n", nombre), ("$e", extra), ("$id", id));

        Recargar();
    }

    private void Alternar()
    {
        if (_grilla.CurrentRow?.DataBoundItem is not DataRowView f)
        {
            Tema.Error("Elegí primero una fila de la lista.");
            return;
        }
        if (!Autorizacion.PedirAdmin(this, $"Vas a activar/desactivar \"{f["Nombre"]}\"."))
            return;

        var id = Convert.ToInt64(f["Id"]);
        Bd.Ejecutar($"UPDATE {_tabla} SET activo = 1 - activo WHERE id = $id;", ("$id", id));
        Recargar();
    }
}
