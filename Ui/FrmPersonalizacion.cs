using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Personalización del negocio (solo admin): nombre, logo y gama de colores. Es lo
/// que hace al programa white-label — cada negocio pone su identidad. Guardar los
/// cambios pide la contraseña de administrador. Los colores se ven del todo al reiniciar.
/// </summary>
public sealed class FrmPersonalizacion : Form
{
    private const string Personalizado = "Personalizado";

    private readonly TextBox _nombre = Tema.Caja(320);
    private readonly ComboBox _gama = Tema.Combo(220);
    private readonly TextBox _acento = Tema.Caja(110);
    private readonly PictureBox _preview = new()
    {
        Size = new Size(96, 96),
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Tema.Superficie,
        BorderStyle = BorderStyle.FixedSingle
    };

    // Cambios de logo pendientes de guardar: ruta nueva, o "" para quitarlo, o null = sin cambios.
    private string? _logoPendiente;

    public FrmPersonalizacion()
    {
        Tema.AplicarA(this);
        Text = "Personalización";
        AutoScroll = true;

        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(24, 20, 24, 20),
            AutoScroll = true
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        raiz.Controls.Add(Tema.CabeceraPantalla("Personalización", "Administración"), 0, 0);
        raiz.Controls.Add(Tarjeta(), 0, 1);
        Controls.Add(raiz);

        Cargar();
    }

    private Control Tarjeta()
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

        var tl = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Tema.Panel
        };

        tl.Controls.Add(Titulo("IDENTIDAD DEL NEGOCIO"));
        tl.Controls.Add(Nota("El nombre aparece en los títulos, encabezados, el Excel del contador y los emails."));
        _nombre.PlaceholderText = "Nombre del negocio";
        tl.Controls.Add(Tema.Campo("Nombre del negocio", _nombre));

        // logo: preview + botones
        tl.Controls.Add(Titulo("LOGO"));
        var filaLogo = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel };
        filaLogo.Controls.Add(_preview);
        var botonesLogo = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            BackColor = Tema.Panel, Margin = new Padding(12, 6, 0, 0)
        };
        var elegirLogo = Tema.Boton("Elegir logo…");
        elegirLogo.Width = 170;
        elegirLogo.Click += (s, e) => ElegirLogo();
        var quitarLogo = Tema.Boton("Quitar logo");
        quitarLogo.Width = 170;
        quitarLogo.Margin = new Padding(0, 6, 0, 0);
        quitarLogo.Click += (s, e) => { _logoPendiente = ""; _preview.Image = null; };
        botonesLogo.Controls.Add(elegirLogo);
        botonesLogo.Controls.Add(quitarLogo);
        filaLogo.Controls.Add(botonesLogo);
        tl.Controls.Add(filaLogo);

        // gama de colores
        tl.Controls.Add(Titulo("COLORES"));
        tl.Controls.Add(Nota("Elegí una gama o un color de acento propio. Se mantiene el fondo oscuro. " +
                             "Los colores se ven del todo al reiniciar el programa."));
        foreach (var g in Tema.Gamas) _gama.Items.Add(g.Nombre);
        _gama.Items.Add(Personalizado);
        _gama.SelectedIndexChanged += (s, e) => RefrescarAcento();

        var filaColor = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel };
        filaColor.Controls.Add(Tema.Campo("Gama", _gama));
        _acento.PlaceholderText = "#RRGGBB";
        filaColor.Controls.Add(Tema.Campo("Acento propio", _acento));
        var elegirColor = Tema.Boton("Color…");
        elegirColor.Width = 100;
        elegirColor.Margin = new Padding(0, 22, 0, 0);
        elegirColor.Click += (s, e) => ElegirColor();
        filaColor.Controls.Add(elegirColor);
        tl.Controls.Add(filaColor);

        var guardar = Tema.Boton("Guardar cambios", primario: true);
        guardar.Width = 200;
        guardar.Margin = new Padding(0, 14, 0, 0);
        guardar.Click += (s, e) => Guardar();
        tl.Controls.Add(guardar);

        card.Controls.Add(tl);
        return card;
    }

    private static Label Titulo(string t) => new()
    {
        Text = t, Font = Tema.Titulo(10.5f), ForeColor = Tema.Verde, AutoSize = true,
        Margin = new Padding(0, 12, 0, 2)
    };
    private static Label Nota(string t) => new()
    {
        Text = t, Font = Tema.Normal(8.5f), ForeColor = Tema.Apagado, AutoSize = true,
        MaximumSize = new Size(620, 0), Margin = new Padding(0, 0, 0, 8)
    };

    private void Cargar()
    {
        _nombre.Text = Bd.Config("marca_nombre");
        _preview.Image = Marca.Logo;

        if (Tema.AcentoCustom is { } hex)
        {
            _gama.SelectedItem = Personalizado;
            _acento.Text = hex;
        }
        else
        {
            _gama.SelectedItem = Tema.GamaActual;
            if (_gama.SelectedItem is null && _gama.Items.Count > 0) _gama.SelectedIndex = 0;
        }
        RefrescarAcento();
    }

    private void RefrescarAcento()
    {
        var custom = (_gama.SelectedItem as string) == Personalizado;
        _acento.Enabled = custom;
    }

    private void ElegirLogo()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Elegí el logo del negocio",
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var s = File.OpenRead(ofd.FileName);
            using var tmp = Image.FromStream(s);
            _preview.Image = new Bitmap(tmp);   // copia para el preview
            _logoPendiente = ofd.FileName;
        }
        catch (Exception ex) { Tema.Error("No se pudo leer la imagen.\n\n" + ex.Message); }
    }

    private void ElegirColor()
    {
        using var cd = new ColorDialog { FullOpen = true };
        if (cd.ShowDialog(this) != DialogResult.OK) return;
        _acento.Text = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
        _gama.SelectedItem = Personalizado;
    }

    private void Guardar()
    {
        var nombre = _nombre.Text.Trim();
        if (nombre.Length == 0) { Tema.Error("Escribí el nombre del negocio."); _nombre.Focus(); return; }

        var custom = (_gama.SelectedItem as string) == Personalizado;
        string nombreGama;
        string? acentoHex = null;
        if (custom)
        {
            acentoHex = _acento.Text.Trim();
            try { ColorTranslator.FromHtml(acentoHex); }
            catch { Tema.Error("El color de acento no es válido. Usá formato #RRGGBB."); return; }
            nombreGama = Tema.Gamas[0].Nombre;
        }
        else
        {
            nombreGama = (_gama.SelectedItem as string) ?? Tema.Gamas[0].Nombre;
        }

        if (!Autorizacion.PedirAdmin(this, "Vas a cambiar la personalización del negocio (nombre, logo y colores)."))
            return;

        try
        {
            Marca.GuardarNombre(nombre);
            Tema.GuardarGama(nombreGama, acentoHex);
            if (_logoPendiente is "") Marca.QuitarLogo();
            else if (_logoPendiente is { } ruta) Marca.GuardarLogo(ruta);
            _logoPendiente = null;

            Tema.Aviso("Personalización guardada.\nReiniciá el programa para ver los colores nuevos en todas las pantallas.");
        }
        catch (Exception ex) { Tema.Error("No se pudo guardar.\n\n" + ex.Message); }
    }
}
