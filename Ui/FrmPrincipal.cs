using RetroGreenGestion.Datos;
using RetroGreenGestion.Reportes;

namespace RetroGreenGestion.Ui;

public sealed class FrmPrincipal : Form
{
    private readonly Panel _contenedor = new() { Dock = DockStyle.Fill, BackColor = Tema.Fondo };
    private readonly Panel _nav = new() { Dock = DockStyle.Left, Width = 232, BackColor = Tema.Panel };
    private readonly Label _aviso = new();
    private Form? _actual;
    private Button? _activo;

    // Timer liviano del reporte diario: revisa una vez por minuto si hay que enviar.
    // Es el único timer del programa; el trabajo de red se hace fuera del hilo de UI.
    private readonly System.Windows.Forms.Timer _timerReportes = new() { Interval = 60_000 };
    private volatile bool _enviandoReportes;

    public FrmPrincipal()
    {
        Tema.AplicarA(this);
        Text = $"{Marca.Nombre} - Gestión";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 700);

        Controls.Add(_contenedor);
        Controls.Add(_nav);
        Controls.Add(Tema.Franja(4));

        ArmarNav();
        Abrir("Servicios del día", () => new FrmServicios());
        Tema.AdornoLogo(this, 46, 16);   // adorno fijo arriba a la derecha
        Load += (s, e) =>
        {
            RevisarExportacionesPendientes();
            RevisarReportes();            // catch-up al abrir: pone al día lo pendiente
        };
        _timerReportes.Tick += (s, e) => RevisarReportes();
        _timerReportes.Start();
        FormClosed += (s, e) => _timerReportes.Stop();
    }

    private void ArmarNav()
    {
        _nav.Controls.Add(new Label
        {
            Text = Marca.Nombre.ToUpperInvariant(),
            Font = Tema.Titulo(19),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 26)
        });
        _nav.Controls.Add(new Label
        {
            Text = "GESTIÓN",
            Font = Tema.Titulo(10),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Location = new Point(22, 56)
        });

        var y = 100;
        void Opcion(string texto, Func<Form> crear)
        {
            var b = Tema.Boton(texto);
            b.Location = new Point(16, y);
            b.Width = 200;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(12, 0, 0, 0);
            b.Click += (s, e) => { Marcar(b); Abrir(texto, crear); };
            _nav.Controls.Add(b);
            y += 46;
            if (_activo is null) Marcar(b);
        }

        void BotonAccion(string texto, Action al)
        {
            var b = Tema.Boton(texto);
            b.Location = new Point(16, y);
            b.Width = 200;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(12, 0, 0, 0);
            b.Click += (s, e) => al();
            _nav.Controls.Add(b);
            y += 46;
        }

        // Operación diaria: la ve todo el mundo.
        Opcion("Servicios del día", () => new FrmServicios());
        Opcion("Egresos", () => new FrmEgresos());
        Opcion("Stock", () => new FrmStock());
        Opcion("Turnos", () => new FrmTurnos());
        Opcion("Clientes", () => new FrmClientes());
        Opcion("Resumen mensual", () => new FrmResumen());
        Opcion("Asistente", () => new FrmAsistente());

        // Configuración y administración: solo el administrador (Ezequiel).
        if (Bd.EsAdmin)
        {
            Opcion("Catálogos", () => new FrmCatalogos());
            Opcion("Cierre de mes", () => new FrmCierre());
            Opcion("Reporte diario", () => new FrmReportes());
            Opcion("Usuarios", () => new FrmUsuarios());
            Opcion("Personalización", () => new FrmPersonalizacion());
        }

        y += 10;
        BotonAccion("Mi contraseña", () => { using var f = new FrmMiClave(); f.ShowDialog(this); });
        if (Bd.EsAdmin)
            BotonAccion("Seguridad", CambiarClave);

        _aviso.Font = Tema.Normal(8.5f);
        _aviso.ForeColor = Tema.Violeta;
        _aviso.AutoSize = false;
        _aviso.Size = new Size(200, 70);
        _aviso.Location = new Point(16, y + 60);
        _nav.Controls.Add(_aviso);

        var pie = new Label
        {
            Text = $"Sesión: {Bd.Usuario}{(Bd.EsAdmin ? " (admin)" : "")}\nBase encriptada",
            Font = Tema.Normal(8f),
            ForeColor = Tema.Apagado,
            AutoSize = false,
            Size = new Size(200, 44),
            Dock = DockStyle.Bottom,
            Padding = new Padding(18, 0, 0, 14)
        };
        _nav.Controls.Add(pie);
    }

    private void Marcar(Button b)
    {
        if (_activo is not null)
        {
            _activo.BackColor = Tema.Panel;
            _activo.ForeColor = Tema.Texto;
            _activo.FlatAppearance.BorderColor = Tema.Borde;
        }
        _activo = b;
        b.BackColor = Tema.Verde;
        b.ForeColor = Color.Black;
        b.FlatAppearance.BorderColor = Tema.Verde;
    }

    private void Abrir(string titulo, Func<Form> crear)
    {
        _actual?.Close();
        _actual?.Dispose();

        var f = crear();
        f.TopLevel = false;
        f.FormBorderStyle = FormBorderStyle.None;
        f.Dock = DockStyle.Fill;
        _contenedor.Controls.Clear();
        _contenedor.Controls.Add(f);
        f.Show();
        _actual = f;
        Text = $"{Marca.Nombre} - {titulo}";
    }

    private void RevisarExportacionesPendientes()
    {
        var pendientes = Repo.MesesCerradosSinExportar();
        if (pendientes.Count == 0) { _aviso.Text = ""; return; }

        _aviso.Text = pendientes.Count == 1
            ? $"⚠ {pendientes[0]} terminó y todavía no se exportó para el contador."
            : $"⚠ Hay {pendientes.Count} meses sin exportar para el contador.";
    }

    private void CambiarClave()
    {
        using var f = new FrmCambiarClave();
        f.ShowDialog(this);
    }

    /// <summary>
    /// Revisa si corresponde mandar el reporte diario (hoy pasada la hora, o días
    /// que quedaron pendientes) y lo hace en segundo plano. Nunca molesta al usuario
    /// ni frena el programa: si falla, queda registrado y se reintenta en la próxima
    /// vuelta o al reabrir. Que el correo salga o no es independiente del resto.
    /// </summary>
    private void RevisarReportes()
    {
        if (_enviandoReportes) return;   // evita solaparse con la vuelta anterior
        try
        {
            var cfg = ConfigCorreo.Cargar();
            if (!cfg.EstaConfigurada) return;   // sin correo configurado no hay reporte ni recordatorios

            var pass = ConfigCorreo.LeerContrasena();
            if (pass is null) return;    // sin contraseña legible no se puede enviar

            _enviandoReportes = true;
            var enviador = new SmtpEnviador(cfg, pass);
            var ahora = DateTime.Now;
            Task.Run(() =>
            {
                // Cada servicio decide si corresponde actuar (reporte diario / recordatorios de turnos).
                try { ServicioReportes.ProcesarAutomatico(enviador, cfg, ahora); }
                catch { /* ServicioReportes ya registra sus errores */ }
                try { ServicioRecordatorios.ProcesarAutomatico(enviador, cfg, ahora); }
                catch { /* best-effort: no molestar al usuario con esto */ }
                finally { _enviandoReportes = false; }
            });
        }
        catch { _enviandoReportes = false; /* nunca romper la UI por el correo */ }
    }
}

public sealed class FrmCambiarClave : Form
{
    public FrmCambiarClave()
    {
        Tema.AplicarA(this);
        Text = "Clave y recuperación";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(380, 352);
        Controls.Add(Tema.Franja(4));

        var nueva = Tema.Caja(320);
        var repetir = Tema.Caja(320);
        nueva.UseSystemPasswordChar = repetir.UseSystemPasswordChar = true;

        var c1 = Tema.Campo("Clave nueva", nueva);
        c1.Location = new Point(28, 30);
        var c2 = Tema.Campo("Repetila", repetir);
        c2.Location = new Point(28, 92);
        Controls.Add(c1);
        Controls.Add(c2);

        var b = Tema.Boton("Cambiar la clave", primario: true);
        b.Location = new Point(28, 160);
        b.Width = 320;
        b.Click += (s, e) =>
        {
            if (nueva.Text.Length < 4) { Tema.Error("La clave tiene que tener al menos 4 caracteres."); return; }
            if (nueva.Text != repetir.Text) { Tema.Error("Las dos claves no coinciden."); return; }

            try
            {
                Bd.CambiarClave(nueva.Text);
                Tema.Aviso("Listo. Anotá la clave nueva en un lugar seguro.\n" +
                           "Si la olvidás, podés recuperar el acceso con un código\n" +
                           "de recuperación.");
                Close();
            }
            catch (Exception ex) { Tema.Error("No se pudo cambiar la clave.\n\n" + ex.Message); }
        };
        Controls.Add(b);

        var regenerar = Tema.Boton("Regenerar códigos de recuperación");
        regenerar.Location = new Point(28, 212);
        regenerar.Width = 320;
        regenerar.Click += (s, e) =>
        {
            if (!Tema.Confirmar(
                    "Esto anula TODOS los códigos de recuperación actuales y\n" +
                    "genera 10 nuevos. Los viejos dejan de servir.\n\n¿Seguir?"))
                return;
            try
            {
                var codigos = Bd.RegenerarCodigos();
                using var f = new FrmCodigos(codigos, recienCreados: false);
                f.ShowDialog(this);
            }
            catch (Exception ex) { Tema.Error("No se pudieron regenerar.\n\n" + ex.Message); }
        };
        Controls.Add(regenerar);

        var estadoCodigos = new Label
        {
            Text = $"Te quedan {Bd.CodigosDisponibles} códigos de recuperación sin usar.",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(28, 258)
        };
        Controls.Add(estadoCodigos);

        var emergencia = Tema.BotonPeligro("Contraseña de emergencia…");
        emergencia.Location = new Point(28, 288);
        emergencia.Width = 320;
        emergencia.Click += (s, e) =>
        {
            using var f = new FrmEmergencia();
            f.ShowDialog(this);
        };
        Controls.Add(emergencia);

        AcceptButton = b;
        Tema.AdornoLogo(this, 40, 10);
    }
}

/// <summary>
/// Configura (o desactiva) la contraseña de emergencia: la que, al escribirla en
/// el login, borra la base al instante y cierra el programa. Sin confirmación: por
/// eso acá se explica bien y se valida que no choque con la clave normal.
/// </summary>
public sealed class FrmEmergencia : Form
{
    private readonly TextBox _clave = Tema.Caja(320);
    private readonly TextBox _repetir = Tema.Caja(320);
    private readonly CheckBox _copias = new();
    private readonly Label _mensaje = new();

    public FrmEmergencia()
    {
        Tema.AplicarA(this);
        Text = "Contraseña de emergencia";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(400, 430);
        Controls.Add(Tema.Franja(4));

        Controls.Add(new Label
        {
            Text = "CONTRASEÑA DE EMERGENCIA",
            Font = Tema.Titulo(13),
            ForeColor = Tema.Rojo,
            AutoSize = true,
            Location = new Point(28, 22)
        });

        Controls.Add(new Label
        {
            Text = "Si la escribís en la pantalla de inicio, el programa BORRA\n" +
                   "toda la base al instante y se cierra. No pide confirmación\n" +
                   "y no se puede deshacer. Es para destruir los datos si te\n" +
                   "obligan a abrir el programa.",
            Font = Tema.Normal(8.75f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(28, 52)
        });

        var estado = new Label
        {
            Text = Bd.HayEmergencia
                ? "Estado: configurada."
                : "Estado: no configurada.",
            Font = Tema.Normal(9f, FontStyle.Bold),
            ForeColor = Bd.HayEmergencia ? Tema.Verde : Tema.Apagado,
            AutoSize = true,
            Location = new Point(28, 118)
        };
        Controls.Add(estado);

        _clave.UseSystemPasswordChar = _repetir.UseSystemPasswordChar = true;
        var c1 = Tema.Campo("Contraseña de emergencia", _clave);
        c1.Location = new Point(28, 146);
        var c2 = Tema.Campo("Repetila", _repetir);
        c2.Location = new Point(28, 208);
        Controls.Add(c1);
        Controls.Add(c2);

        _copias.Text = "También borrar las copias de seguridad";
        _copias.ForeColor = Tema.Texto;
        _copias.Font = Tema.Normal(9f);
        _copias.AutoSize = true;
        _copias.Location = new Point(28, 270);
        Controls.Add(_copias);

        Controls.Add(new Label
        {
            Text = "Elegí algo que JAMÁS escribirías por error y que sea\n" +
                   "distinto de tu clave normal (acá se valida que no choquen).",
            Font = Tema.Normal(8.25f),
            ForeColor = Tema.Violeta,
            AutoSize = true,
            Location = new Point(28, 298)
        });

        _mensaje.Font = Tema.Normal(8.5f);
        _mensaje.ForeColor = Tema.Rojo;
        _mensaje.AutoSize = true;
        _mensaje.MaximumSize = new Size(344, 0);
        _mensaje.Location = new Point(28, 334);
        Controls.Add(_mensaje);

        var guardar = Tema.Boton("Guardar", primario: true);
        guardar.Location = new Point(28, 376);
        guardar.Width = 200;
        guardar.Click += (s, e) => Guardar();
        Controls.Add(guardar);

        var quitar = Tema.BotonPeligro("Desactivar");
        quitar.Location = new Point(236, 376);
        quitar.Width = 112;
        quitar.Enabled = Bd.HayEmergencia;
        quitar.Click += (s, e) =>
        {
            if (!Autorizacion.PedirAdmin(this, "Vas a DESACTIVAR la contraseña de emergencia.")) return;
            Bd.QuitarEmergencia();
            Tema.Aviso("Contraseña de emergencia desactivada.");
            Close();
        };
        Controls.Add(quitar);
        Tema.AdornoLogo(this, 40, 10);
    }

    private void Guardar()
    {
        _mensaje.Text = "";
        if (_clave.Text.Length < 6)
        {
            _mensaje.Text = "Usá al menos 6 caracteres.";
            return;
        }
        if (_clave.Text != _repetir.Text)
        {
            _mensaje.Text = "Las dos no coinciden.";
            return;
        }
        if (!Autorizacion.PedirAdmin(this,
                "Vas a configurar/cambiar la CONTRASEÑA DE EMERGENCIA (la que borra la base)."))
            return;

        try
        {
            Bd.FijarEmergencia(_clave.Text, _copias.Checked);
            Tema.Aviso("Contraseña de emergencia configurada.\n" +
                       "Probala mentalmente: si la escribís en el inicio, se borra todo.");
            Close();
        }
        catch (ArgumentException ex) { _mensaje.Text = ex.Message; }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }
}
