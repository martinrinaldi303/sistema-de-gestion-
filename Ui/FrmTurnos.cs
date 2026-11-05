using System.Data;
using RetroGreenGestion.Datos;
using RetroGreenGestion.Reportes;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Agenda de turnos: alta, cambio de estado y "registrar servicio" (pasa un turno
/// atendido al libro diario, reusando el alta de servicios).
/// </summary>
public sealed class FrmTurnos : Form
{
    private readonly DateTimePicker _fecha = Tema.Fecha();
    private readonly DateTimePicker _hora = new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 80, Font = Tema.Normal(10f)
    };
    private readonly TextBox _cliente = Tema.Caja(180);
    private readonly TextBox _clienteEmail = Tema.Caja(200);
    private readonly ComboBox _prof = Tema.Combo(160);
    private readonly ComboBox _serv = Tema.Combo(180);
    private readonly TextBox _duracion = Tema.Caja(70);
    private readonly TextBox _nota = Tema.Caja(180);

    private readonly DateTimePicker _diaVer = Tema.Fecha();
    private readonly DataGridView _grilla = Tema.Grilla();
    private readonly DataGridView _proximos = Tema.Grilla();

    // Registrar servicio (sobre el turno seleccionado)
    private readonly TextBox _precio = Tema.Caja(110);
    private readonly ComboBox _medio = Tema.Combo(150);

    // Bot de recordatorios (solo admin)
    private readonly CheckBox _recActivo = new();
    private readonly TextBox _recHoras = Tema.Caja(70);
    private readonly TextBox _recTexto = Tema.Caja(560);

    public FrmTurnos()
    {
        Tema.AplicarA(this);
        Text = "Turnos";
        AutoScroll = true;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Tema.Fondo, ColumnCount = 2, RowCount = 4,
            Padding = new Padding(24, 20, 24, 20), AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 320));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var cab = Tema.CabeceraPantalla("Turnos", "Operaciones diarias");
        raiz.Controls.Add(cab, 0, 0);
        raiz.SetColumnSpan(cab, 2);
        raiz.Controls.Add(TarjetaAlta(), 0, 1);
        raiz.Controls.Add(TarjetaRegistrar(), 1, 1);
        raiz.Controls.Add(Agenda(), 0, 2);
        raiz.Controls.Add(Tema.TarjetaContenedor("Próximos turnos", _proximos, Tema.Verde), 1, 2);

        if (Bd.EsAdmin)
        {
            var rec = TarjetaRecordatorios();
            raiz.Controls.Add(rec, 0, 3);
            raiz.SetColumnSpan(rec, 2);
        }

        Controls.Add(raiz);

        _diaVer.ValueChanged += (s, e) => RecargarDia();
        _grilla.SelectionChanged += (s, e) => PrefillPrecio();
        Load += (s, e) => { CargarCombos(); RecargarDia(); RecargarProximos(); if (Bd.EsAdmin) CargarRecordatorios(); };
    }

    private Control TarjetaAlta()
    {
        _duracion.Text = "30";
        var tl = Pila("NUEVO TURNO");
        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(560, 0) };
        campos.Controls.Add(Tema.Campo("Fecha", _fecha));
        campos.Controls.Add(Tema.Campo("Hora", _hora));
        campos.Controls.Add(Tema.Campo("Cliente", _cliente));
        campos.Controls.Add(Tema.Campo("Email cliente", _clienteEmail));
        campos.Controls.Add(Tema.Campo("Profesional", _prof));
        campos.Controls.Add(Tema.Campo("Servicio", _serv));
        campos.Controls.Add(Tema.Campo("Duración (min)", _duracion));
        campos.Controls.Add(Tema.Campo("Nota", _nota));
        tl.Controls.Add(campos);
        var agregar = Tema.Boton("Agregar turno", primario: true); agregar.Width = 160; agregar.Click += (s, e) => Agregar();
        tl.Controls.Add(Fila(agregar));
        return Card(tl, 560);
    }

    private Control TarjetaRegistrar()
    {
        var tl = Pila("TURNO SELECCIONADO");
        tl.Controls.Add(new Label { Text = "Cambiá el estado o registralo como servicio.", Font = Tema.Normal(8.5f), ForeColor = Tema.Apagado, AutoSize = true, Margin = new Padding(0, 0, 0, 6) });

        var atendido = Tema.Boton("Atendido"); atendido.Width = 110; atendido.Click += (s, e) => Estado("atendido");
        var ausente = Tema.Boton("Ausente"); ausente.Width = 100; ausente.Click += (s, e) => Estado("ausente");
        var cancelar = Tema.BotonPeligro("Cancelar"); cancelar.Width = 110; cancelar.Click += (s, e) => Estado("cancelado");
        tl.Controls.Add(Fila(atendido, ausente, cancelar));

        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(320, 0), Margin = new Padding(0, 6, 0, 0) };
        _precio.PlaceholderText = "0";
        campos.Controls.Add(Tema.Campo("Precio cobrado", _precio));
        campos.Controls.Add(Tema.Campo("Medio de pago", _medio));
        tl.Controls.Add(campos);
        var registrar = Tema.Boton("Registrar servicio", primario: true); registrar.Width = 190; registrar.Click += (s, e) => Registrar();
        tl.Controls.Add(Fila(registrar));
        return Card(tl, 340);
    }

    private Control Agenda()
    {
        var card = new PanelRedondo { Dock = DockStyle.Fill, Radio = 14, ColorBorde = Tema.Borde, AccentoVerde = true, BackColor = Tema.Panel, Padding = new Padding(16, 12, 16, 14), Margin = new Padding(0, 14, 12, 0) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false, BackColor = Tema.Panel };
        top.Controls.Add(new Label { Text = "AGENDA DEL DÍA", Font = Tema.Titulo(11f), ForeColor = Tema.Verde, AutoSize = true, Margin = new Padding(0, 8, 14, 0) });
        top.Controls.Add(Tema.Campo("Ver día", _diaVer));
        card.Controls.Add(_grilla);
        card.Controls.Add(top);
        _grilla.Dock = DockStyle.Fill;
        return card;
    }

    // ------------------------------------------------------------- helpers UI
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
    private static Control Card(Control inner, int ancho)
    {
        var card = new PanelRedondo { AutoSize = true, Radio = 14, ColorBorde = Tema.Borde, AccentoVerde = true, BackColor = Tema.Panel, Padding = new Padding(16, 14, 16, 14), Margin = new Padding(0, 14, 12, 0), MaximumSize = new Size(ancho + 40, 0) };
        card.Controls.Add(inner);
        return card;
    }

    // ------------------------------------------------------------- lógica
    private void CargarCombos()
    {
        var profs = new List<Item> { new(0, "(sin asignar)") };
        profs.AddRange(Repo.Profesionales());
        _prof.DataSource = profs;

        var servs = new List<Item> { new(0, "(sin asignar)") };
        servs.AddRange(Repo.Servicios());
        _serv.DataSource = servs;

        _medio.DataSource = Repo.MediosPago();
    }

    private void RecargarDia()
    {
        _grilla.DataSource = Repo.TurnosDia(_diaVer.Value.Date);
        if (_grilla.Columns["Id"] is { } col) col.Visible = false;
        if (_grilla.Columns["Fecha"] is { } f) f.Visible = false;
    }

    private void RecargarProximos()
    {
        _proximos.DataSource = Repo.ProximosTurnos();
        foreach (var c in new[] { "Id", "Nota", "Estado" })
            if (_proximos.Columns[c] is { } col) col.Visible = false;
    }

    private long? IdSeleccionado()
        => _grilla.CurrentRow?.Cells["Id"].Value is { } v && v != DBNull.Value ? Convert.ToInt64(v) : null;

    private void Agregar()
    {
        long? ProfId() => _prof.SelectedItem is Item i && i.Id != 0 ? i.Id : null;
        long? ServId() => _serv.SelectedItem is Item i && i.Id != 0 ? i.Id : null;
        var dur = int.TryParse(_duracion.Text.Trim(), out var d) && d > 0 ? d : 30;
        try
        {
            Repo.AgregarTurno(_fecha.Value.Date, _hora.Value.ToString("HH:mm"), _cliente.Text.Trim(),
                _clienteEmail.Text.Trim(), ProfId(), ServId(), dur, _nota.Text.Trim());
            _cliente.Clear(); _clienteEmail.Clear(); _nota.Clear();
            if (_fecha.Value.Date == _diaVer.Value.Date) RecargarDia();
            RecargarProximos();
        }
        catch (Exception ex) { Tema.Error("No se pudo agregar el turno.\n\n" + ex.Message); }
    }

    private void Estado(string estado)
    {
        if (IdSeleccionado() is not { } id) { Tema.Error("Elegí un turno de la agenda."); return; }
        try { Repo.CambiarEstadoTurno(id, estado); RecargarDia(); RecargarProximos(); }
        catch (Exception ex) { Tema.Error(ex.Message); }
    }

    private void PrefillPrecio()
    {
        if (_grilla.CurrentRow?.Cells["Servicio"].Value?.ToString() is { Length: > 0 } nombre)
        {
            var serv = Repo.Servicios().FirstOrDefault(s => s.Nombre == nombre);
            if (serv is not null) _precio.Text = Bd.APesos(serv.Cent).ToString("0.##");
        }
    }

    private void Registrar()
    {
        if (IdSeleccionado() is not { } id) { Tema.Error("Elegí un turno de la agenda."); return; }
        if (_medio.SelectedItem is not Item medio) { Tema.Error("Elegí el medio de pago."); return; }
        var cent = decimal.TryParse(_precio.Text.Trim(), out var p) ? Bd.ACentavos(p) : 0;
        if (cent <= 0) { Tema.Error("Escribí el precio cobrado."); return; }
        try
        {
            Repo.RegistrarServicioDesdeTurno(id, cent, medio.Id);
            Tema.Aviso("Turno registrado como servicio en el libro diario.");
            RecargarDia(); RecargarProximos();
        }
        catch (Exception ex) { Tema.Error("No se pudo registrar.\n\n" + ex.Message); }
    }

    // ------------------------------------------------- bot de recordatorios (admin)

    private Control TarjetaRecordatorios()
    {
        _recTexto.Multiline = true; _recTexto.Height = 54; _recTexto.ScrollBars = ScrollBars.Vertical;
        _recActivo.Text = "Enviar recordatorio por email al cliente";
        _recActivo.ForeColor = Tema.Texto; _recActivo.Font = Tema.Normal(9.5f); _recActivo.AutoSize = true;
        _recActivo.Margin = new Padding(0, 0, 0, 6);

        var tl = Pila("BOT DE RECORDATORIOS DE TURNOS");
        tl.Controls.Add(new Label
        {
            Text = "Avisa a los clientes con email cuyo turno cae dentro de las próximas N horas, " +
                   "preguntándoles si van a venir. Usa el mismo correo configurado en \"Reporte diario\". " +
                   "Guardar pide la clave de administrador.",
            Font = Tema.Normal(8.5f), ForeColor = Tema.Apagado, AutoSize = true,
            MaximumSize = new Size(840, 0), Margin = new Padding(0, 0, 0, 8)
        });
        tl.Controls.Add(_recActivo);
        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel };
        fila.Controls.Add(Tema.Campo("Horas antes", _recHoras));
        fila.Controls.Add(Tema.Campo("Mensaje ({cliente}, {fecha}, {hora}, {negocio})", _recTexto));
        tl.Controls.Add(fila);
        var guardar = Tema.Boton("Guardar recordatorios", primario: true); guardar.Width = 220;
        guardar.Click += (s, e) => GuardarRecordatorios();
        tl.Controls.Add(Fila(guardar));
        return Card(tl, 860);
    }

    private void CargarRecordatorios()
    {
        var c = ConfigRecordatorios.Cargar();
        _recActivo.Checked = c.Activo;
        _recHoras.Text = c.HorasAntes.ToString();
        _recTexto.Text = c.Texto;
    }

    private void GuardarRecordatorios()
    {
        if (!int.TryParse(_recHoras.Text.Trim(), out var h) || h <= 0)
        { Tema.Error("Las horas tienen que ser un número mayor a cero."); return; }
        if (!Autorizacion.PedirAdmin(this, "Vas a cambiar la configuración del bot de recordatorios de turnos.")) return;
        try
        {
            new ConfigRecordatorios { Activo = _recActivo.Checked, HorasAntes = h, Texto = _recTexto.Text.Trim() }.Guardar();
            Tema.Aviso("Recordatorios guardados.\nSe envían automáticamente mientras el programa está abierto " +
                       "(requiere el correo configurado en \"Reporte diario\").");
        }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }
}
