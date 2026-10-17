using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Login por usuario. Según el estado de la base muestra uno de tres modos:
///   - CrearAdmin: no hay base todavía -> se crea el administrador (Ezequiel).
///   - Migrar:     base vieja de clave única -> esa clave pasa a ser la del admin.
///   - Login:      ya hay usuarios -> se elige el usuario y se pone su contraseña.
/// Cada usuario tiene su propia contraseña; la identidad queda fija para la sesión.
/// </summary>
public sealed class FrmLogin : Form
{
    private enum Modo { CrearAdmin, Migrar, Login }

    private readonly Modo _modo;
    private readonly ComboBox _usuario = Tema.Combo(292);
    private readonly TextBox _nombre = Tema.Caja(292);
    private readonly TextBox _clave = Tema.Caja(292);
    private readonly TextBox _repetir = Tema.Caja(292);
    private readonly Label _mensaje = new();

    public FrmLogin()
    {
        var usuarios = Bd.Usuarios();   // de paso migra un llavero viejo de clave única
        _modo = !Bd.YaExiste ? Modo.CrearAdmin
              : usuarios.Count > 0 ? Modo.Login
              : Modo.Migrar;

        Tema.AplicarA(this);
        Text = "Retro Green - Gestión";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;

        var altoCaja = _modo switch { Modo.CrearAdmin => 346, Modo.Migrar => 300, _ => 300 };
        ClientSize = new Size(420, altoCaja + 126);

        Controls.Add(Tema.Franja(4));

        var caja = new Panel
        {
            BackColor = Tema.Panel,
            Location = new Point(40, 46),
            Size = new Size(340, altoCaja)
        };
        caja.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, caja.ClientRectangle,
            Tema.Borde, ButtonBorderStyle.Solid);
        Controls.Add(caja);

        var logo = Tema.LogoCentral(74);
        logo.Location = new Point((caja.Width - 74) / 2, 10);
        caja.Controls.Add(logo);

        caja.Controls.Add(new Label
        {
            Text = _modo switch
            {
                Modo.CrearAdmin => "CREÁ EL ADMINISTRADOR",
                Modo.Migrar => "CLAVE ACTUAL DEL NEGOCIO",
                _ => "INICIAR SESIÓN"
            },
            Font = Tema.Titulo(11),
            ForeColor = Tema.Verde,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(caja.Width, 20),
            Location = new Point(0, 86)
        });

        _clave.UseSystemPasswordChar = _repetir.UseSystemPasswordChar = true;
        var y = 112;

        if (_modo == Modo.Login)
        {
            y = Campo(caja, y, "Usuario", _usuario);
            foreach (var (n, admin) in Bd.Usuarios())
                _usuario.Items.Add(admin ? $"{n}  (admin)" : n);
            if (_usuario.Items.Count > 0) _usuario.SelectedIndex = 0;
            y = Campo(caja, y, "Contraseña", _clave);
        }
        else if (_modo == Modo.CrearAdmin)
        {
            _nombre.Text = "Ezequiel";
            y = Campo(caja, y, "Nombre del administrador", _nombre);
            y = Campo(caja, y, "Contraseña", _clave);
            y = Campo(caja, y, "Repetí la contraseña", _repetir);
        }
        else // Migrar
        {
            y = Campo(caja, y, "Clave con la que abrís hoy", _clave);
            caja.Controls.Add(new Label
            {
                Text = "Esta clave va a quedar como la de Ezequiel (admin).\nDespués creás los demás usuarios.",
                Font = Tema.Normal(8.25f),
                ForeColor = Tema.Violeta,
                AutoSize = true,
                Location = new Point(24, y)
            });
            y += 34;
        }

        _mensaje.Font = Tema.Normal(8.5f);
        _mensaje.ForeColor = Tema.Rojo;
        _mensaje.AutoSize = true;
        _mensaje.MaximumSize = new Size(292, 0);
        _mensaje.Location = new Point(24, y);
        caja.Controls.Add(_mensaje);

        var textoBoton = _modo == Modo.CrearAdmin ? "Crear y entrar" : "Entrar";
        var entrar = Tema.Boton(textoBoton, primario: true);
        entrar.Location = new Point(24, altoCaja - 48);
        entrar.Width = 292;
        entrar.Click += (s, e) => Intentar();
        caja.Controls.Add(entrar);

        if (_modo == Modo.Login)
        {
            var olvide = new LinkLabel
            {
                Text = "¿Olvidaste tu contraseña?",
                Font = Tema.Normal(8.5f),
                LinkColor = Tema.Verde,
                ActiveLinkColor = Tema.VerdeClaro,
                AutoSize = true,
                Location = new Point(24, altoCaja - 72)
            };
            olvide.LinkClicked += (s, e) => Recuperar();
            caja.Controls.Add(olvide);
        }

        AcceptButton = entrar;
        ActiveControl = _modo == Modo.Login ? _usuario : _modo == Modo.CrearAdmin ? _nombre : _clave;
    }

    /// <summary>Etiqueta chica + control, apilados. Devuelve la Y siguiente.</summary>
    private static int Campo(Panel caja, int y, string etiqueta, Control control)
    {
        caja.Controls.Add(new Label
        {
            Text = etiqueta,
            Font = Tema.Normal(9),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(24, y)
        });
        control.Location = new Point(24, y + 20);
        control.Width = 292;
        caja.Controls.Add(control);
        return y + 54;
    }

    private void Intentar()
    {
        _mensaje.Text = "";
        var clave = _clave.Text;

        if (_modo == Modo.CrearAdmin)
        {
            if (_nombre.Text.Trim().Length == 0) { _mensaje.Text = "Falta el nombre."; return; }
            if (clave.Length < 4) { _mensaje.Text = "La contraseña tiene que tener al menos 4 caracteres."; return; }
            if (clave != _repetir.Text) { _mensaje.Text = "Las dos contraseñas no coinciden."; return; }
        }
        else if (clave.Length == 0) { _mensaje.Text = "Escribí tu contraseña."; return; }

        Cursor = Cursors.WaitCursor;
        bool ok = _modo switch
        {
            Modo.CrearAdmin => Bd.CrearAdminYEntrar(_nombre.Text.Trim(), clave),
            Modo.Migrar => Bd.MigrarYEntrar(clave),
            _ => Bd.EntrarUsuario(NombreUsuarioElegido(), clave)
        };
        Cursor = Cursors.Default;

        if (!ok)
        {
            _mensaje.Text = _modo == Modo.Login ? "Usuario o contraseña incorrectos." : "Clave incorrecta.";
            _clave.SelectAll();
            _clave.Focus();
            return;
        }

        // Admin recién creado o base recién migrada: mostramos los códigos una vez.
        if (Bd.CodigosNuevos is { } codigos)
            using (var f = new FrmCodigos(codigos, recienCreados: true))
                f.ShowDialog(this);

        DialogResult = DialogResult.OK;
        Close();
    }

    private string NombreUsuarioElegido()
    {
        var txt = _usuario.SelectedItem?.ToString() ?? "";
        var corte = txt.IndexOf("  (admin)", StringComparison.Ordinal);
        return corte >= 0 ? txt[..corte] : txt;
    }

    private void Recuperar()
    {
        using var f = new FrmRecuperar();
        if (f.ShowDialog(this) != DialogResult.OK) return;

        // Quedó con la contraseña nueva del admin y la base abierta: seguimos.
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>
/// Muestra los codigos de recuperacion. Se ven una sola vez: despues solo
/// queda su version cifrada en el llavero. Deja copiarlos y guardarlos a archivo.
/// </summary>
public sealed class FrmCodigos : Form
{
    public FrmCodigos(IReadOnlyList<string> codigos, bool recienCreados)
    {
        var texto = string.Join(Environment.NewLine, codigos);

        Tema.AplicarA(this);
        Text = "Códigos de recuperación";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(420, 430);
        Controls.Add(Tema.Franja(4));

        Controls.Add(new Label
        {
            Text = recienCreados ? "GUARDÁ ESTOS CÓDIGOS" : "CÓDIGOS DE RECUPERACIÓN",
            Font = Tema.Titulo(14),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(28, 24)
        });

        Controls.Add(new Label
        {
            Text = "Cada código sirve UNA sola vez para recuperar el acceso\n" +
                   "si olvidás la clave. No se van a volver a mostrar.\n" +
                   "Guardalos en un lugar seguro (no en esta computadora).",
            Font = Tema.Normal(8.75f),
            ForeColor = Tema.Violeta,
            AutoSize = true,
            Location = new Point(28, 56)
        });

        var caja = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Text = texto,
            BackColor = Tema.Superficie,
            ForeColor = Tema.Texto,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font(FontFamily.GenericMonospace, 12f, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Center,
            Location = new Point(28, 118),
            Size = new Size(364, 210)
        };
        Controls.Add(caja);

        var copiar = Tema.Boton("Copiar");
        copiar.Location = new Point(28, 344);
        copiar.Width = 120;
        copiar.Click += (s, e) =>
        {
            try { Clipboard.SetText(texto); Tema.Aviso("Códigos copiados al portapapeles."); }
            catch { Tema.Error("No se pudieron copiar."); }
        };
        Controls.Add(copiar);

        var guardar = Tema.Boton("Guardar en archivo…");
        guardar.Location = new Point(156, 344);
        guardar.Width = 236;
        guardar.Click += (s, e) => GuardarArchivo(texto);
        Controls.Add(guardar);

        var listo = Tema.Boton("Ya los guardé", primario: true);
        listo.Location = new Point(28, 388);
        listo.Width = 364;
        listo.Click += (s, e) => Close();
        Controls.Add(listo);

        AcceptButton = listo;
        Tema.AdornoLogo(this, 40, 10);
    }

    private void GuardarArchivo(string texto)
    {
        using var sfd = new SaveFileDialog
        {
            Title = "Guardar códigos de recuperación",
            Filter = "Texto (*.txt)|*.txt",
            FileName = $"codigos-recuperacion-retro-green-{DateTime.Now:yyyyMMdd}.txt"
        };
        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        var contenido =
            "RETRO GREEN - CÓDIGOS DE RECUPERACIÓN" + Environment.NewLine +
            $"Generados el {DateTime.Now:dd/MM/yyyy HH:mm}" + Environment.NewLine +
            "Cada código sirve una sola vez. Guardalos en un lugar seguro." +
            Environment.NewLine + Environment.NewLine + texto + Environment.NewLine;
        try
        {
            File.WriteAllText(sfd.FileName, contenido);
            Tema.Aviso("Códigos guardados.");
        }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }
}

/// <summary>
/// Recuperar el acceso: el usuario ingresa un código de recuperación y define
/// una clave nueva. Si el código es válido, la base queda abierta.
/// </summary>
public sealed class FrmRecuperar : Form
{
    private readonly TextBox _codigo = Tema.Caja(320);
    private readonly TextBox _nueva = Tema.Caja(320);
    private readonly TextBox _repetir = Tema.Caja(320);
    private readonly Label _mensaje = new();

    public FrmRecuperar()
    {
        Tema.AplicarA(this);
        Text = "Recuperar el acceso";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(380, 330);
        Controls.Add(Tema.Franja(4));

        Controls.Add(new Label
        {
            Text = "Escribí uno de tus códigos de recuperación\ny elegí una clave nueva.",
            Font = Tema.Normal(8.75f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Location = new Point(28, 20)
        });

        _nueva.UseSystemPasswordChar = _repetir.UseSystemPasswordChar = true;

        var cCodigo = Tema.Campo("Código de recuperación", _codigo);
        cCodigo.Location = new Point(28, 58);
        var cNueva = Tema.Campo("Clave nueva", _nueva);
        cNueva.Location = new Point(28, 120);
        var cRepetir = Tema.Campo("Repetí la clave nueva", _repetir);
        cRepetir.Location = new Point(28, 182);
        Controls.Add(cCodigo);
        Controls.Add(cNueva);
        Controls.Add(cRepetir);

        _mensaje.Font = Tema.Normal(8.5f);
        _mensaje.ForeColor = Tema.Rojo;
        _mensaje.AutoSize = true;
        _mensaje.MaximumSize = new Size(324, 0);
        _mensaje.Location = new Point(28, 244);
        Controls.Add(_mensaje);

        var b = Tema.Boton("Recuperar y entrar", primario: true);
        b.Location = new Point(28, 276);
        b.Width = 320;
        b.Click += (s, e) => Intentar();
        Controls.Add(b);

        AcceptButton = b;
        ActiveControl = _codigo;
        Tema.AdornoLogo(this, 40, 10);
    }

    private void Intentar()
    {
        _mensaje.Text = "";

        if (_nueva.Text.Length < 4)
        {
            _mensaje.Text = "La clave nueva tiene que tener al menos 4 caracteres.";
            return;
        }
        if (_nueva.Text != _repetir.Text)
        {
            _mensaje.Text = "Las dos claves nuevas no coinciden.";
            return;
        }

        Cursor = Cursors.WaitCursor;
        var ok = Bd.RecuperarConCodigo(_codigo.Text, _nueva.Text);
        Cursor = Cursors.Default;

        if (!ok)
        {
            _mensaje.Text = "El código no es válido o ya fue usado.";
            _codigo.SelectAll();
            _codigo.Focus();
            return;
        }

        Tema.Aviso($"Listo. Tu clave nueva ya funciona.\nTe quedan {Bd.CodigosDisponibles} códigos de recuperación.");
        DialogResult = DialogResult.OK;
        Close();
    }
}
