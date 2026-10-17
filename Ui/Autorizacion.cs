using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Pide la contraseña de un administrador antes de una acción que borra o anula
/// datos. Así un usuario común (o alguien que dejó la sesión abierta) no puede
/// eliminar nada sin la autorización del administrador.
/// </summary>
public static class Autorizacion
{
    public static bool PedirAdmin(Control contexto, string accion)
    {
        using var f = new FrmClaveAdmin(accion);
        var dueño = (IWin32Window?)contexto.FindForm() ?? contexto;
        return f.ShowDialog(dueño) == DialogResult.OK;
    }
}

internal sealed class FrmClaveAdmin : Form
{
    private readonly TextBox _clave = Tema.Caja(324);
    private readonly Label _mensaje = new();

    public FrmClaveAdmin(string accion)
    {
        Tema.AplicarA(this);
        Text = "Confirmación del administrador";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(400, 320);

        // Apilado vertical: así el casillero queda SIEMPRE debajo del texto (que
        // es de alto variable) y no lo tapa.
        var col = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Tema.Fondo,
            Padding = new Padding(24, 14, 24, 14)
        };

        col.Controls.Add(new Label
        {
            Text = "ACCIÓN PROTEGIDA",
            Font = Tema.Titulo(12f),
            ForeColor = Tema.Rojo,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        });

        col.Controls.Add(new Label
        {
            Text = accion + "\n\nIngresá la contraseña de un administrador para continuar.",
            Font = Tema.Normal(9f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            MaximumSize = new Size(344, 0),
            Margin = new Padding(0, 0, 0, 10)
        });

        _clave.UseSystemPasswordChar = true;
        _clave.Width = 344;
        _clave.Margin = new Padding(0, 0, 0, 6);
        col.Controls.Add(_clave);

        _mensaje.Font = Tema.Normal(8.5f);
        _mensaje.ForeColor = Tema.Rojo;
        _mensaje.AutoSize = true;
        _mensaje.MaximumSize = new Size(344, 0);
        _mensaje.Margin = new Padding(0, 0, 0, 8);
        col.Controls.Add(_mensaje);

        var confirmar = Tema.Boton("Confirmar", primario: true);
        confirmar.Width = 200;
        confirmar.Margin = new Padding(0, 0, 8, 0);
        confirmar.Click += (s, e) => Intentar();

        var cancelar = Tema.Boton("Cancelar");
        cancelar.Width = 136;
        cancelar.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Fondo, Margin = new Padding(0, 4, 0, 0) };
        fila.Controls.Add(confirmar);
        fila.Controls.Add(cancelar);
        col.Controls.Add(fila);

        Controls.Add(col);
        Controls.Add(Tema.Franja(4));

        AcceptButton = confirmar;
        CancelButton = cancelar;
        ActiveControl = _clave;
    }

    private void Intentar()
    {
        if (Bd.VerificarAdmin(_clave.Text))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        _mensaje.Text = "Contraseña incorrecta o no es de un administrador.";
        _clave.SelectAll();
        _clave.Focus();
    }
}
