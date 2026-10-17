using RetroGreenGestion.Datos;
using RetroGreenGestion.Reportes;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Configuración del reporte diario por email (sólo la ve el administrador). Acá se
/// elige a qué correo se manda y a qué hora, más los datos del servidor SMTP. Como
/// pidió el dueño, cualquier cambio se confirma con la contraseña de administrador.
///
/// También deja mandar el reporte de hoy a mano ("cerrar el día y enviar"). El envío
/// automático a la hora configurada lo maneja la ventana principal con un timer.
/// </summary>
public sealed class FrmReportes : Form
{
    private readonly CheckBox _activo = new();
    private readonly TextBox _destino = Tema.Caja(320);
    private readonly DateTimePicker _hora = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "HH:mm",
        ShowUpDown = true,
        Width = 90,
        Font = Tema.Normal(10f)
    };
    private readonly TextBox _host = Tema.Caja(260);
    private readonly TextBox _puerto = Tema.Caja(80);
    private readonly CheckBox _ssl = new();
    private readonly TextBox _usuario = Tema.Caja(260);
    private readonly TextBox _remitente = Tema.Caja(260);
    private readonly TextBox _pass = Tema.Caja(260);
    private readonly Label _estado = new();

    public FrmReportes()
    {
        Tema.AplicarA(this);
        Text = "Reporte diario por email";
        AutoScroll = true;

        _pass.UseSystemPasswordChar = true;
        _pass.PlaceholderText = "(dejá vacío para no cambiarla)";

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 20, 24, 20),
            AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(Tema.CabeceraPantalla("Reporte diario por email", "Administración"), 0, 0);
        raiz.Controls.Add(TarjetaConfig(), 0, 1);
        raiz.Controls.Add(TarjetaEstado(), 0, 2);

        Controls.Add(raiz);
        Cargar();
    }

    private Control TarjetaConfig()
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
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0, 0, 0, 14)
        };

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
            Text = "A DÓNDE Y CUÁNDO",
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });
        tl.Controls.Add(new Label
        {
            Text = "El reporte del día se manda a este correo. Para cambiar cualquier dato se pide la " +
                   "contraseña de administrador.",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        _activo.Text = "Enviar el reporte diario automáticamente";
        _activo.ForeColor = Tema.Texto;
        _activo.Font = Tema.Normal(9.5f);
        _activo.AutoSize = true;
        _activo.Margin = new Padding(2, 0, 0, 8);
        tl.Controls.Add(_activo);

        var fila1 = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(760, 0) };
        fila1.Controls.Add(Tema.Campo("Correo de destino", _destino));
        fila1.Controls.Add(Tema.Campo("Hora de envío", _hora));
        tl.Controls.Add(fila1);

        tl.Controls.Add(new Label
        {
            Text = "SERVIDOR DE CORREO (SMTP)",
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 2)
        });
        tl.Controls.Add(new Label
        {
            Text = "Ej. Gmail: host smtp.gmail.com, puerto 587, TLS activado. La contraseña es una " +
                   "\"contraseña de aplicación\", no la del correo. Se guarda cifrada en esta notebook.",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        _ssl.Text = "Usar TLS/STARTTLS";
        _ssl.ForeColor = Tema.Texto;
        _ssl.Font = Tema.Normal(9.5f);
        _ssl.AutoSize = true;
        _ssl.Margin = new Padding(2, 22, 0, 0);

        var fila2 = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(760, 0) };
        fila2.Controls.Add(Tema.Campo("Host SMTP", _host));
        fila2.Controls.Add(Tema.Campo("Puerto", _puerto));
        fila2.Controls.Add(_ssl);
        tl.Controls.Add(fila2);

        var fila3 = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel, MaximumSize = new Size(760, 0) };
        fila3.Controls.Add(Tema.Campo("Usuario SMTP", _usuario));
        fila3.Controls.Add(Tema.Campo("Remitente (De:)", _remitente));
        fila3.Controls.Add(Tema.Campo("Contraseña", _pass));
        tl.Controls.Add(fila3);

        var guardar = Tema.Boton("Guardar cambios", primario: true);
        guardar.Width = 190;
        guardar.Click += (s, e) => Guardar();

        var enviar = Tema.Boton("Enviar el reporte de hoy ahora");
        enviar.Width = 270;
        enviar.Click += (s, e) => EnviarHoy();

        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel, Margin = new Padding(0, 12, 0, 0) };
        guardar.Margin = enviar.Margin = new Padding(0, 4, 10, 4);
        fila.Controls.Add(guardar);
        fila.Controls.Add(enviar);
        tl.Controls.Add(fila);

        card.Controls.Add(tl);
        return card;
    }

    private Control TarjetaEstado()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Radio = 14,
            ColorBorde = Tema.Borde,
            BackColor = Tema.Panel,
            Padding = new Padding(18, 14, 18, 14),
            Margin = new Padding(0, 0, 0, 0)
        };

        _estado.Font = Tema.Normal(9f);
        _estado.ForeColor = Tema.Apagado;
        _estado.AutoSize = true;
        _estado.MaximumSize = new Size(760, 0);
        card.Controls.Add(_estado);
        return card;
    }

    private void Cargar()
    {
        var cfg = ConfigCorreo.Cargar();
        _activo.Checked = cfg.Activo;
        _destino.Text = cfg.Destino;
        _hora.Value = DateTime.Today.Add(cfg.Hora.ToTimeSpan());
        _host.Text = cfg.Host;
        _puerto.Text = cfg.Puerto.ToString();
        _ssl.Checked = cfg.Ssl;
        _usuario.Text = cfg.Usuario;
        _remitente.Text = cfg.Remitente;
        _pass.Clear();
        MostrarEstado();
    }

    private void MostrarEstado()
    {
        var hoy = DateTime.Today;
        var estado = RegistroEnvios.Estado(hoy);
        var con = ConfigCorreo.HayContrasena ? "sí" : "no";

        var texto = $"Contraseña SMTP guardada: {con}.\n";
        texto += estado switch
        {
            "enviado" => $"El reporte de hoy ({hoy:dd/MM/yyyy}) ya se envió.",
            "error"   => $"El último intento de hoy falló: {RegistroEnvios.UltimoError(hoy)}",
            _         => "Hoy todavía no se envió el reporte."
        };
        _estado.Text = texto;
    }

    private void Guardar()
    {
        var destino = _destino.Text.Trim();
        if (!destino.Contains('@') || destino.Length < 5)
        {
            Tema.Error("Escribí un correo de destino válido.");
            _destino.Focus();
            return;
        }
        if (!int.TryParse(_puerto.Text.Trim(), out var puerto) || puerto is < 1 or > 65535)
        {
            Tema.Error("El puerto tiene que ser un número (ej. 587).");
            _puerto.Focus();
            return;
        }
        if (_activo.Checked &&
            (_host.Text.Trim().Length == 0 || _usuario.Text.Trim().Length == 0 || _remitente.Text.Trim().Length == 0))
        {
            Tema.Error("Para enviar automáticamente faltan datos del servidor (host, usuario y remitente).");
            return;
        }

        if (!Autorizacion.PedirAdmin(this, "Vas a cambiar la configuración del reporte diario por email."))
            return;

        try
        {
            var cfg = ConfigCorreo.Cargar();   // conserva 'Desde' si ya existía
            cfg.Activo = _activo.Checked;
            cfg.Destino = destino;
            cfg.Hora = TimeOnly.FromDateTime(_hora.Value);
            cfg.Host = _host.Text.Trim();
            cfg.Puerto = puerto;
            cfg.Ssl = _ssl.Checked;
            cfg.Usuario = _usuario.Text.Trim();
            cfg.Remitente = _remitente.Text.Trim();
            cfg.Guardar();

            if (_pass.Text.Length > 0)
            {
                ConfigCorreo.GuardarContrasena(_pass.Text);
                _pass.Clear();
            }

            Tema.Aviso("Configuración guardada.");
            MostrarEstado();
        }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }

    private void EnviarHoy()
    {
        var cfg = ConfigCorreo.Cargar();
        if (!cfg.EstaConfigurada)
        {
            Tema.Error("Primero completá y guardá los datos del correo.");
            return;
        }
        var pass = ConfigCorreo.LeerContrasena();
        if (pass is null)
        {
            Tema.Error("Falta la contraseña SMTP (o no se puede leer en esta máquina/usuario).\n" +
                       "Guardala de nuevo desde este Windows.");
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            var enviador = new SmtpEnviador(cfg, pass);
            var r = ServicioReportes.EnviarDia(DateTime.Today, enviador, cfg, forzar: true);
            Cursor = Cursors.Default;

            switch (r)
            {
                case ResultadoEnvio.Enviado:
                    Tema.Aviso($"Reporte de hoy enviado a {cfg.Destino}.");
                    break;
                case ResultadoEnvio.Error:
                    Tema.Error("No se pudo enviar.\n\n" + RegistroEnvios.UltimoError(DateTime.Today));
                    break;
                default:
                    Tema.Aviso("No se envió: " + r);
                    break;
            }
            MostrarEstado();
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            Tema.Error("No se pudo enviar.\n\n" + ex.Message);
        }
    }
}
