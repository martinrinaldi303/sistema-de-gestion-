using System.Data;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Administración de usuarios (solo la ve el administrador). Cada usuario tiene
/// su propia contraseña, que abre la misma base. Ezequiel crea y elimina usuarios.
/// </summary>
public sealed class FrmUsuarios : Form
{
    private readonly TextBox _nombre = Tema.Caja(240);
    private readonly TextBox _clave = Tema.Caja(240);
    private readonly TextBox _repetir = Tema.Caja(240);
    private readonly CheckBox _admin = new();
    private readonly DataGridView _grilla = Tema.Grilla();

    public FrmUsuarios()
    {
        Tema.AplicarA(this);
        Text = "Usuarios";

        _clave.UseSystemPasswordChar = _repetir.UseSystemPasswordChar = true;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 20, 24, 20)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(Tema.CabeceraPantalla("Usuarios", "Administración"), 0, 0);
        raiz.Controls.Add(TarjetaAlta(), 0, 1);
        raiz.Controls.Add(Tema.TarjetaContenedor("Usuarios creados", _grilla), 0, 2);

        Controls.Add(raiz);
        Recargar();
    }

    private Control TarjetaAlta()
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

        // Pila vertical que crece con su contenido (si no, se cortan los campos y botones).
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
            Text = "CREAR USUARIO",
            Font = Tema.Titulo(10.5f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });
        tl.Controls.Add(new Label
        {
            Text = "La contraseña la pone el administrador; después cada uno puede cambiarla desde \"Mi contraseña\".",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        var campos = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Panel };
        campos.Controls.Add(Tema.Campo("Nombre", _nombre));
        campos.Controls.Add(Tema.Campo("Contraseña", _clave));
        campos.Controls.Add(Tema.Campo("Repetir", _repetir));
        tl.Controls.Add(campos);

        _admin.Text = "Es administrador (puede crear/eliminar usuarios y ver la configuración)";
        _admin.ForeColor = Tema.Texto;
        _admin.Font = Tema.Normal(9.5f);
        _admin.AutoSize = true;
        _admin.Margin = new Padding(2, 4, 0, 6);
        tl.Controls.Add(_admin);

        var crear = Tema.Boton("Crear usuario", primario: true);
        crear.Width = 170;
        crear.Click += (s, e) => Crear();

        var eliminar = Tema.BotonPeligro("Eliminar seleccionado");
        eliminar.Width = 220;
        eliminar.Click += (s, e) => Eliminar();

        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel, Margin = new Padding(0, 8, 0, 0) };
        crear.Margin = eliminar.Margin = new Padding(0, 4, 10, 4);
        fila.Controls.Add(crear);
        fila.Controls.Add(eliminar);
        tl.Controls.Add(fila);

        card.Controls.Add(tl);
        return card;
    }

    private void Recargar()
    {
        var dt = new DataTable();
        dt.Columns.Add("Usuario");
        dt.Columns.Add("Rol");
        foreach (var (nombre, admin) in Bd.ListaUsuarios())
            dt.Rows.Add(nombre, admin ? "Administrador" : "Usuario");
        _grilla.DataSource = dt;
    }

    private void Crear()
    {
        var nombre = _nombre.Text.Trim();
        if (nombre.Length == 0) { Tema.Error("Falta el nombre."); _nombre.Focus(); return; }
        if (_clave.Text.Length < 4) { Tema.Error("La contraseña tiene que tener al menos 4 caracteres."); return; }
        if (_clave.Text != _repetir.Text) { Tema.Error("Las dos contraseñas no coinciden."); return; }

        try
        {
            Bd.CrearUsuario(nombre, _clave.Text, _admin.Checked);
            _nombre.Clear();
            _clave.Clear();
            _repetir.Clear();
            _admin.Checked = false;
            Recargar();
        }
        catch (Exception ex) { Tema.Error(ex.Message); }
    }

    private void Eliminar()
    {
        if (_grilla.CurrentRow?.Cells["Usuario"].Value is not string nombre)
        {
            Tema.Error("Elegí primero un usuario de la lista.");
            return;
        }
        if (!Tema.Confirmar($"¿Eliminar al usuario \"{nombre}\"?\nNo va a poder entrar más.")) return;

        try
        {
            Bd.EliminarUsuario(nombre);
            Recargar();
        }
        catch (Exception ex) { Tema.Error(ex.Message); }
    }
}

/// <summary>Cualquier usuario cambia su propia contraseña (confirma con la actual).</summary>
public sealed class FrmMiClave : Form
{
    private readonly TextBox _actual = Tema.Caja(320);
    private readonly TextBox _nueva = Tema.Caja(320);
    private readonly TextBox _repetir = Tema.Caja(320);
    private readonly Label _mensaje = new();

    public FrmMiClave()
    {
        Tema.AplicarA(this);
        Text = "Mi contraseña";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(380, 320);
        Controls.Add(Tema.Franja(4));

        _actual.UseSystemPasswordChar = _nueva.UseSystemPasswordChar = _repetir.UseSystemPasswordChar = true;

        var c0 = Tema.Campo("Contraseña actual", _actual); c0.Location = new Point(28, 24);
        var c1 = Tema.Campo("Contraseña nueva", _nueva); c1.Location = new Point(28, 86);
        var c2 = Tema.Campo("Repetila", _repetir); c2.Location = new Point(28, 148);
        Controls.Add(c0);
        Controls.Add(c1);
        Controls.Add(c2);

        _mensaje.Font = Tema.Normal(8.5f);
        _mensaje.ForeColor = Tema.Rojo;
        _mensaje.AutoSize = true;
        _mensaje.MaximumSize = new Size(324, 0);
        _mensaje.Location = new Point(28, 212);
        Controls.Add(_mensaje);

        var b = Tema.Boton("Cambiar", primario: true);
        b.Location = new Point(28, 250);
        b.Width = 320;
        b.Click += (s, e) => Cambiar();
        Controls.Add(b);

        AcceptButton = b;
        ActiveControl = _actual;
    }

    private void Cambiar()
    {
        _mensaje.Text = "";
        if (!Bd.VerificarClave(_actual.Text)) { _mensaje.Text = "La contraseña actual no es correcta."; return; }
        if (_nueva.Text.Length < 4) { _mensaje.Text = "La nueva tiene que tener al menos 4 caracteres."; return; }
        if (_nueva.Text != _repetir.Text) { _mensaje.Text = "Las dos nuevas no coinciden."; return; }

        try
        {
            Bd.CambiarClave(_nueva.Text);
            Tema.Aviso("Listo. Tu contraseña nueva ya funciona.");
            Close();
        }
        catch (Exception ex) { Tema.Error("No se pudo cambiar.\n\n" + ex.Message); }
    }
}
