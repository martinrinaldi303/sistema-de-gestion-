using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Control de stock: alta/edición de productos, movimientos (entrada/salida/ajuste) y
/// venta de producto (suma a la caja y descuenta stock). Muestra el valor del inventario
/// y alerta de los que están bajo mínimo.
/// </summary>
public sealed class FrmStock : Form
{
    private readonly DataGridView _grilla = Tema.Grilla();

    // Alta / edición
    private readonly TextBox _nombre = Tema.Caja(200);
    private readonly TextBox _codigo = Tema.Caja(110);
    private readonly TextBox _precio = Tema.Caja(110);
    private readonly TextBox _costo = Tema.Caja(110);
    private readonly TextBox _minimo = Tema.Caja(90);
    private readonly TextBox _inicial = Tema.Caja(90);
    private long? _idEditando;

    // Movimiento
    private readonly ComboBox _tipoMov = Tema.Combo(130);
    private readonly TextBox _cantMov = Tema.Caja(90);
    private readonly TextBox _motivoMov = Tema.Caja(180);

    // Venta
    private readonly ComboBox _prodVenta = Tema.Combo(200);
    private readonly TextBox _cantVenta = Tema.Caja(70);
    private readonly ComboBox _medioVenta = Tema.Combo(150);
    private readonly TextBox _precioVenta = Tema.Caja(110);

    private readonly Panel _tValor = Tema.Tarjeta("Valor del inventario", "$0,00", Tema.Verde);
    private readonly DataGridView _bajoMin = Tema.Grilla();

    public FrmStock()
    {
        Tema.AplicarA(this);
        Text = "Stock";
        AutoScroll = true;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Tema.Fondo, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(24, 20, 24, 20), AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(Tema.CabeceraPantalla("Control de stock", "Operaciones diarias"), 0, 0);
        raiz.Controls.Add(Acciones(), 0, 1);
        raiz.Controls.Add(Tema.TarjetaContenedor("Productos", _grilla, Tema.Verde), 0, 2);
        raiz.Controls.Add(Alertas(), 0, 3);
        Controls.Add(raiz);

        _grilla.SelectionChanged += (s, e) => CargarSeleccion();
        Load += (s, e) => Recargar();
    }

    private Control Acciones()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, BackColor = Tema.Fondo,
            Margin = new Padding(0, 0, 0, 8)
        };
        flow.Controls.Add(TarjetaAlta());
        flow.Controls.Add(TarjetaMovimiento());
        flow.Controls.Add(TarjetaVenta());
        return flow;
    }

    private Control TarjetaAlta()
    {
        foreach (var t in new[] { _precio, _costo, _minimo, _inicial }) t.PlaceholderText = "0";
        var tl = Pila("ALTA / EDICIÓN DE PRODUCTO");
        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(380, 0) };
        campos.Controls.Add(Tema.Campo("Nombre", _nombre));
        campos.Controls.Add(Tema.Campo("Código", _codigo));
        campos.Controls.Add(Tema.Campo("Precio venta", _precio));
        campos.Controls.Add(Tema.Campo("Costo", _costo));
        campos.Controls.Add(Tema.Campo("Mínimo", _minimo));
        campos.Controls.Add(Tema.Campo("Stock inicial", _inicial));
        tl.Controls.Add(campos);

        var guardar = Tema.Boton("Guardar", primario: true); guardar.Width = 120; guardar.Click += (s, e) => GuardarProducto();
        var nuevo = Tema.Boton("Nuevo"); nuevo.Width = 90; nuevo.Click += (s, e) => LimpiarAlta();
        var baja = Tema.BotonPeligro("Eliminar"); baja.Width = 110; baja.Click += (s, e) => Eliminar();
        tl.Controls.Add(Fila(guardar, nuevo, baja));
        return Card(tl);
    }

    private Control TarjetaMovimiento()
    {
        _tipoMov.Items.AddRange(new object[] { "Entrada", "Salida", "Ajuste (fijar)" });
        _tipoMov.SelectedIndex = 0;
        _cantMov.PlaceholderText = "0";
        var tl = Pila("MOVIMIENTO DE STOCK");
        tl.Controls.Add(new Label { Text = "Sobre el producto seleccionado en la lista.", Font = Tema.Normal(8.5f), ForeColor = Tema.Apagado, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });
        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(340, 0) };
        campos.Controls.Add(Tema.Campo("Tipo", _tipoMov));
        campos.Controls.Add(Tema.Campo("Cantidad", _cantMov));
        campos.Controls.Add(Tema.Campo("Motivo", _motivoMov));
        tl.Controls.Add(campos);
        var registrar = Tema.Boton("Registrar", primario: true); registrar.Width = 130; registrar.Click += (s, e) => RegistrarMovimiento();
        tl.Controls.Add(Fila(registrar));
        return Card(tl);
    }

    private Control TarjetaVenta()
    {
        _prodVenta.DisplayMember = "Nombre";
        _cantVenta.Text = "1";
        _prodVenta.SelectedIndexChanged += (s, e) => SugerirPrecio();
        _cantVenta.TextChanged += (s, e) => SugerirPrecio();
        var tl = Pila("VENDER PRODUCTO");
        tl.Controls.Add(new Label { Text = "Suma a la caja y descuenta stock.", Font = Tema.Normal(8.5f), ForeColor = Tema.Apagado, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });
        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(360, 0) };
        campos.Controls.Add(Tema.Campo("Producto", _prodVenta));
        campos.Controls.Add(Tema.Campo("Cantidad", _cantVenta));
        campos.Controls.Add(Tema.Campo("Medio de pago", _medioVenta));
        campos.Controls.Add(Tema.Campo("Precio total", _precioVenta));
        tl.Controls.Add(campos);
        var vender = Tema.Boton("Vender", primario: true); vender.Width = 130; vender.Click += (s, e) => Vender();
        tl.Controls.Add(Fila(vender));
        return Card(tl);
    }

    private Control Alertas()
    {
        var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Tema.Fondo, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tl.Controls.Add(_tValor, 0, 0);
        var card = Tema.TarjetaContenedor("Bajo mínimo", _bajoMin, Tema.Rojo);
        card.Height = 150;
        tl.Controls.Add(card, 1, 0);
        return tl;
    }

    // ----------------------------------------------------------------- helpers UI

    private static FlowLayoutPanel Pila(string titulo)
    {
        var tl = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Panel };
        tl.Controls.Add(new Label { Text = titulo, Font = Tema.Titulo(10f), ForeColor = Tema.Verde, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });
        return tl;
    }
    private static FlowLayoutPanel Fila(params Control[] cs)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel, Margin = new Padding(0, 8, 0, 0) };
        foreach (var c in cs) { c.Margin = new Padding(0, 0, 8, 0); f.Controls.Add(c); }
        return f;
    }
    private static Control Card(Control inner)
    {
        var card = new PanelRedondo { AutoSize = true, Radio = 14, ColorBorde = Tema.Borde, AccentoVerde = true, BackColor = Tema.Panel, Padding = new Padding(16, 14, 16, 14), Margin = new Padding(0, 0, 12, 12) };
        card.Controls.Add(inner);
        return card;
    }

    // ----------------------------------------------------------------- lógica

    private void Recargar()
    {
        _grilla.DataSource = Repo.ListarProductos();
        if (_grilla.Columns["Id"] is { } col) col.Visible = false;
        foreach (var c in new[] { "Precio", "Costo" })
            if (_grilla.Columns[c] is { } cc) cc.DefaultCellStyle.Format = "C2";

        _prodVenta.DataSource = Repo.ProductosActivos();
        _medioVenta.DataSource = Repo.MediosPago();

        Tema.ValorTarjeta(_tValor, Bd.Plata(Repo.ValorInventarioCent()));
        _bajoMin.DataSource = Repo.ProductosBajoMinimo();
        SugerirPrecio();
    }

    private long? IdSeleccionado()
    {
        if (_grilla.CurrentRow?.Cells["Id"].Value is { } v && v != DBNull.Value)
            return Convert.ToInt64(v);
        return null;
    }

    private void CargarSeleccion()
    {
        if (_grilla.CurrentRow is null) return;
        var r = _grilla.CurrentRow;
        _idEditando = IdSeleccionado();
        _nombre.Text = r.Cells["Producto"].Value?.ToString() ?? "";
        _codigo.Text = r.Cells["Código"].Value?.ToString() ?? "";
        _precio.Text = r.Cells["Precio"].Value?.ToString() ?? "";
        _costo.Text = r.Cells["Costo"].Value?.ToString() ?? "";
        _minimo.Text = r.Cells["Mínimo"].Value?.ToString() ?? "";
        _inicial.Text = "";
        _inicial.Enabled = false;   // en edición no se toca el stock acá (va por movimientos)
    }

    private void LimpiarAlta()
    {
        _idEditando = null;
        foreach (var t in new[] { _nombre, _codigo, _precio, _costo, _minimo, _inicial }) t.Clear();
        _inicial.Enabled = true;
        _nombre.Focus();
    }

    private static long Cent(TextBox t) => decimal.TryParse(t.Text.Trim(), out var d) ? Bd.ACentavos(d) : 0;
    private static long Entero(TextBox t) => long.TryParse(t.Text.Trim(), out var n) ? n : 0;

    private void GuardarProducto()
    {
        var nombre = _nombre.Text.Trim();
        if (nombre.Length == 0) { Tema.Error("Falta el nombre del producto."); _nombre.Focus(); return; }
        try
        {
            if (_idEditando is { } id)
                Repo.EditarProducto(id, nombre, _codigo.Text.Trim(), Cent(_precio), Cent(_costo), Entero(_minimo));
            else
                Repo.AgregarProducto(nombre, _codigo.Text.Trim(), Cent(_precio), Cent(_costo), Entero(_inicial), Entero(_minimo));
            LimpiarAlta();
            Recargar();
        }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }

    private void Eliminar()
    {
        if (IdSeleccionado() is not { } id) { Tema.Error("Elegí un producto de la lista."); return; }
        if (!Autorizacion.PedirAdmin(this, $"Vas a ELIMINAR el producto \"{_nombre.Text}\". (Se desactiva; el histórico queda.)")) return;
        try { Repo.DesactivarProducto(id); LimpiarAlta(); Recargar(); }
        catch (Exception ex) { Tema.Error(ex.Message); }
    }

    private void RegistrarMovimiento()
    {
        if (IdSeleccionado() is not { } id) { Tema.Error("Elegí un producto de la lista."); return; }
        var cant = Entero(_cantMov);
        if (cant <= 0) { Tema.Error("La cantidad tiene que ser mayor a cero."); return; }
        try
        {
            switch (_tipoMov.SelectedIndex)
            {
                case 0: Repo.RegistrarMovimiento(id, "entrada", cant, _motivoMov.Text.Trim()); break;
                case 1: Repo.RegistrarMovimiento(id, "salida", -cant, _motivoMov.Text.Trim()); break;
                default: // ajuste: fija el stock al número tipeado
                    var actual = Convert.ToInt64(_grilla.CurrentRow!.Cells["Stock"].Value);
                    Repo.RegistrarMovimiento(id, "ajuste", cant - actual, _motivoMov.Text.Trim());
                    break;
            }
            _cantMov.Clear(); _motivoMov.Clear();
            Recargar();
        }
        catch (Exception ex) { Tema.Error("No se pudo registrar.\n\n" + ex.Message); }
    }

    private void SugerirPrecio()
    {
        if (_prodVenta.SelectedItem is Repo.ProductoVenta p)
        {
            var cant = Entero(_cantVenta);
            if (cant <= 0) cant = 1;
            _precioVenta.Text = Bd.APesos(p.PrecioVentaCent * cant).ToString("0.##");
        }
    }

    private void Vender()
    {
        if (_prodVenta.SelectedItem is not Repo.ProductoVenta p) { Tema.Error("Elegí un producto."); return; }
        if (_medioVenta.SelectedItem is not Item medio) { Tema.Error("Elegí el medio de pago."); return; }
        var cant = Entero(_cantVenta);
        if (cant <= 0) { Tema.Error("La cantidad tiene que ser mayor a cero."); return; }
        if (cant > p.StockActual &&
            !Tema.Confirmar($"Hay {p.StockActual} en stock y querés vender {cant}. El stock va a quedar negativo.\n¿Seguir?"))
            return;
        var total = Cent(_precioVenta);
        if (total <= 0) { Tema.Error("El precio total tiene que ser mayor a cero."); return; }
        try
        {
            Repo.RegistrarVentaProducto(p.Id, cant, total, medio.Id);
            Tema.Aviso($"Venta registrada: {cant} x {p.Nombre} por {Bd.Plata(total)}.");
            Recargar();
        }
        catch (Exception ex) { Tema.Error("No se pudo registrar la venta.\n\n" + ex.Message); }
    }
}
