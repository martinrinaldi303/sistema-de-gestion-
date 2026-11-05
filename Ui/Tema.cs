using System.Drawing.Drawing2D;
using System.Drawing.Text;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Paleta y tipografia de Retro Green Gestion, alineada al boceto de Stitch
/// (code.html): fondo casi negro, tarjetas y controles redondeados, verde
/// neon de marca con texto oscuro, acento violeta y rojo suave.
/// </summary>
public static class Tema
{
    // ---- Paleta tomada de los design tokens del boceto (code.html) --------
    public static readonly Color Fondo       = ColorTranslator.FromHtml("#0A0A0A");  // canvas-base
    public static readonly Color Sidebar     = ColorTranslator.FromHtml("#0E0E10");  // surface-container-lowest
    public static readonly Color Panel       = ColorTranslator.FromHtml("#18181B");  // surface-card
    public static readonly Color Superficie  = ColorTranslator.FromHtml("#201F21");  // surface-container (inputs)
    public static readonly Color SuperficieBaja = ColorTranslator.FromHtml("#1B1B1D"); // surface-container-low (cabeceras)
    public static readonly Color Elevada     = ColorTranslator.FromHtml("#1F1F23");  // surface-elevated (hover)
    // Acentos: cambian con la gama que elige el negocio (ver AplicarAcento / CargarGama).
    // El fondo oscuro no se toca; sólo el color primario, sus derivados y el secundario.
    public static Color Verde       { get; private set; } = ColorTranslator.FromHtml("#4BE277");  // primary (acento)
    public static Color VerdeFuerte { get; private set; } = ColorTranslator.FromHtml("#22C55E");  // hover
    public static Color VerdeClaro  { get; private set; } = ColorTranslator.FromHtml("#6BFF8F");  // seleccion
    public static Color Violeta     { get; private set; } = ColorTranslator.FromHtml("#C084FC");  // secundario
    public static readonly Color Apagado     = ColorTranslator.FromHtml("#BCCBB9");  // on-surface-variant
    public static readonly Color Borde       = ColorTranslator.FromHtml("#27272A");  // border-subtle
    public static readonly Color BordeFuerte = ColorTranslator.FromHtml("#3F3F46");  // border-prominent
    public static readonly Color Texto       = ColorTranslator.FromHtml("#E5E1E4");  // on-surface
    public static readonly Color Rojo        = ColorTranslator.FromHtml("#F87171");  // crimson-muted
    public static Color SobreVerde  { get; private set; } = ColorTranslator.FromHtml("#0A0A0A");  // texto sobre el acento

    // Space Grotesk para titulos / Outfit para texto, con alternativas si el
    // equipo no tiene esas fuentes instaladas (se bajan en la web via Google Fonts).
    private static readonly string FamiliaTitulo = Elegir("Space Grotesk", "Oswald", "Segoe UI Semibold", "Segoe UI");
    private static readonly string FamiliaTexto  = Elegir("Outfit", "Montserrat", "Segoe UI");

    private static string Elegir(params string[] candidatas)
    {
        using var instaladas = new InstalledFontCollection();
        var nombres = instaladas.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidatas)
            if (nombres.Contains(c)) return c;
        return "Segoe UI";
    }

    public static Font Titulo(float tam, FontStyle estilo = FontStyle.Bold)
        => new(FamiliaTitulo, tam, estilo);

    public static Font Normal(float tam = 9.75f, FontStyle estilo = FontStyle.Regular)
        => new(FamiliaTexto, tam, estilo);

    // ------------------------------------------------------------ gama de colores

    /// <summary>Una gama: color de acento primario y secundario.</summary>
    public sealed record Gama(string Nombre, string AcentoHex, string SecundarioHex);

    /// <summary>Las gamas que el negocio puede elegir (más, opcional, un acento propio).</summary>
    public static readonly IReadOnlyList<Gama> Gamas = new[]
    {
        new Gama("Verde neón", "#4BE277", "#C084FC"),
        new Gama("Azul",       "#38BDF8", "#C084FC"),
        new Gama("Violeta",    "#C084FC", "#4BE277"),
        new Gama("Naranja",    "#FB923C", "#38BDF8"),
        new Gama("Coral",      "#F87171", "#FBBF24"),
        new Gama("Turquesa",   "#2DD4BF", "#C084FC"),
    };

    private const string KGama = "tema_gama";
    private const string KAcento = "tema_acento_hex";

    public static string GamaActual => Bd.Config(KGama, Gamas[0].Nombre);
    public static string? AcentoCustom
    {
        get { var h = Bd.Config(KAcento, "").Trim(); return h.Length > 0 ? h : null; }
    }

    /// <summary>Lee la gama guardada en Config y la aplica. Se llama al arrancar (tras el login).</summary>
    public static void CargarGama()
    {
        try
        {
            if (AcentoCustom is { } custom) { AplicarAcento(custom, Gamas[0].SecundarioHex); return; }
            var g = Gamas.FirstOrDefault(x => x.Nombre == GamaActual) ?? Gamas[0];
            AplicarAcento(g.AcentoHex, g.SecundarioHex);
        }
        catch { /* si algo falla, quedan los colores por defecto */ }
    }

    /// <summary>Guarda la elección (gama por nombre y, opcional, un acento propio) y la aplica.</summary>
    public static void GuardarGama(string nombreGama, string? acentoHex)
    {
        Bd.GuardarConfig(KGama, nombreGama);
        Bd.GuardarConfig(KAcento, acentoHex ?? "");
        CargarGama();
    }

    private static void AplicarAcento(string acentoHex, string secundarioHex)
    {
        var acento = ColorTranslator.FromHtml(acentoHex);
        Verde = acento;
        VerdeFuerte = Oscurecer(acento, 0.14);
        VerdeClaro = Aclarar(acento, 0.22);
        SobreVerde = Luminancia(acento) > 0.6 ? ColorTranslator.FromHtml("#0A0A0A") : Color.White;
        try { Violeta = ColorTranslator.FromHtml(secundarioHex); } catch { /* deja el actual */ }
    }

    private static double Luminancia(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
    private static Color Oscurecer(Color c, double f)
        => Color.FromArgb(c.A, (int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));
    private static Color Aclarar(Color c, double f)
        => Color.FromArgb(c.A,
            (int)(c.R + (255 - c.R) * f), (int)(c.G + (255 - c.G) * f), (int)(c.B + (255 - c.B) * f));

    // --------------------------------------------------------------- piezas

    public static void AplicarA(Form f)
    {
        f.BackColor = Fondo;
        f.ForeColor = Texto;
        f.Font = Normal();
        f.StartPosition = FormStartPosition.CenterScreen;
        if (Icono is not null) f.Icon = Icono;
    }

    private static Icon? _icono;
    private static bool _iconoBuscado;

    /// <summary>Ícono de ventana (barra de tareas y título): el logo del negocio si hay; si no, el genérico embebido.</summary>
    public static Icon? Icono
    {
        get
        {
            if (_iconoBuscado) return _icono;
            _iconoBuscado = true;
            try
            {
                // Sólo el logo del negocio; si no hay, el ícono genérico de .NET (sin marca fija).
                if (Marca.Logo is { } logo)
                {
                    using var bmp = new Bitmap(logo, 64, 64);
                    _icono = Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch { _icono = null; }
            return _icono;
        }
    }

    public static Label Encabezado(string texto, float tam = 20f)
        => new()
        {
            Text = texto.ToUpperInvariant(),
            Font = Titulo(tam),
            ForeColor = Texto,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };

    public static Label Etiqueta(string texto)
        => new()
        {
            Text = texto.ToUpperInvariant(),
            Font = Normal(8.25f, FontStyle.Bold),
            ForeColor = Apagado,
            AutoSize = true
        };

    public static Button Boton(string texto, bool primario = false)
    {
        var b = new BotonRedondo
        {
            Text = texto.ToUpperInvariant(),
            Font = Titulo(10.5f),
            Height = 40,
            Radio = 10,
            Cursor = Cursors.Hand
        };

        if (primario)
        {
            b.BackColor = Verde;
            b.ForeColor = SobreVerde;
            b.Hover = VerdeFuerte;
            b.GrosorBorde = 0;
        }
        else
        {
            b.BackColor = Superficie;
            b.ForeColor = Texto;
            b.ColorBorde = Borde;
            b.GrosorBorde = 1;
            b.Hover = Elevada;
        }
        return b;
    }

    public static Button BotonPeligro(string texto)
    {
        var b = (BotonRedondo)Boton(texto);
        b.BackColor = Panel;
        b.ForeColor = Rojo;
        b.ColorBorde = Rojo;
        b.GrosorBorde = 1;
        b.Hover = Elevada;
        return b;
    }

    public static TextBox Caja(int ancho = 220)
    {
        var t = new TextBox
        {
            Width = ancho,
            BackColor = Superficie,
            ForeColor = Texto,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Normal(10f)
        };
        return t;
    }

    public static ComboBox Combo(int ancho = 220)
        => new()
        {
            Width = ancho,
            BackColor = Superficie,
            ForeColor = Texto,
            FlatStyle = FlatStyle.Flat,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Normal(10f)
        };

    public static DateTimePicker Fecha()
        => new()
        {
            Width = 140,
            Format = DateTimePickerFormat.Short,
            CalendarMonthBackground = Panel,
            CalendarForeColor = Texto,
            CalendarTitleBackColor = Verde,
            CalendarTitleForeColor = SobreVerde
        };

    public static DataGridView Grilla()
    {
        var g = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Panel,
            BorderStyle = BorderStyle.None,
            GridColor = Borde,
            EnableHeadersVisualStyles = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            Font = Normal(9.5f)
        };

        // Cabecera estilo tabla del boceto: fondo bajo, texto apagado en mayusculas.
        g.ColumnHeadersDefaultCellStyle.BackColor = SuperficieBaja;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Apagado;
        g.ColumnHeadersDefaultCellStyle.Font = Titulo(9.5f);
        g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = SuperficieBaja;
        g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Apagado;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeight = 38;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        g.DefaultCellStyle.BackColor = Panel;
        g.DefaultCellStyle.ForeColor = Texto;
        g.DefaultCellStyle.SelectionBackColor = Elevada;
        g.DefaultCellStyle.SelectionForeColor = VerdeClaro;
        g.DefaultCellStyle.Padding = new Padding(8, 5, 8, 5);

        g.RowTemplate.Height = 32;
        return g;
    }

    /// <summary>Tarjeta redondeada con un numero grande, como los KPIs del boceto.</summary>
    public static Panel Tarjeta(string titulo, string valor, Color? color = null)
    {
        var p = new PanelRedondo
        {
            Radio = 12,
            ColorBorde = Borde,
            AccentoVerde = true,
            BackColor = Panel,
            Padding = new Padding(18, 14, 18, 14),
            Margin = new Padding(0, 0, 14, 14),
            Width = 236,
            Height = 92
        };

        p.Controls.Add(new Label
        {
            Text = valor,
            Name = "valor",
            Font = Titulo(20f),
            ForeColor = color ?? Texto,
            AutoSize = true,
            Location = new Point(18, 38)
        });
        p.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Normal(8f, FontStyle.Bold),
            ForeColor = Apagado,
            AutoSize = true,
            Location = new Point(18, 15)
        });
        return p;
    }

    public static void ValorTarjeta(Panel tarjeta, string valor)
    {
        if (tarjeta.Controls["valor"] is Label l) l.Text = valor;
    }

    /// <summary>Tarjeta con una línea extra abajo: la comparación contra el mes anterior.</summary>
    public static Panel TarjetaKpi(string titulo, string valor, Color? color = null)
    {
        var p = (PanelRedondo)Tarjeta(titulo, valor, color);
        p.Height = 116;
        p.Width = 256;
        p.Controls.Add(new Label
        {
            Name = "pie",
            Text = "",
            Font = Normal(8.5f),
            ForeColor = Apagado,
            AutoSize = true,
            Location = new Point(18, 80)
        });
        return p;
    }

    public static void PieTarjeta(Panel tarjeta, string texto, Color? color = null)
    {
        if (tarjeta.Controls["pie"] is not Label l) return;
        l.Text = texto;
        l.ForeColor = color ?? Apagado;
    }

    /// <summary>Bloque con título verde y un control adentro, como las secciones del boceto.</summary>
    public static Panel Bloque(string titulo, Control contenido, int margenDerecho = 16)
    {
        var p = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, margenDerecho, 16) };
        contenido.Dock = DockStyle.Fill;
        p.Controls.Add(contenido);
        p.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Titulo(11),
            ForeColor = Verde,
            Dock = DockStyle.Top,
            Height = 26
        });
        return p;
    }

    /// <summary>Barra verde fina, el detalle de marca del boceto.</summary>
    public static Panel Franja(int alto = 3)
        => new() { Height = alto, Dock = DockStyle.Top, BackColor = Verde };

    /// <summary>Etiqueta chiquita arriba, control abajo. Para armar formularios.</summary>
    public static Panel Campo(string etiqueta, Control control)
    {
        var p = new Panel
        {
            Width = control.Width,
            Height = control.Height + 24,
            Margin = new Padding(0, 0, 14, 10)
        };
        var l = Etiqueta(etiqueta);
        l.Location = new Point(0, 0);
        control.Location = new Point(0, 22);
        p.Controls.Add(l);
        p.Controls.Add(control);
        return p;
    }

    /// <summary>Encabezado de pantalla: breadcrumb chico en verde + título grande, como el boceto.</summary>
    public static Control CabeceraPantalla(string titulo, string seccion = "Operaciones diarias")
    {
        var cab = new Panel { Dock = DockStyle.Fill, BackColor = Fondo, Margin = new Padding(0, 0, 0, 14), Height = 64 };
        cab.Controls.Add(new Label
        {
            Text = $"{Marca.Nombre.ToUpperInvariant()}   /   {seccion.ToUpperInvariant()}",
            Font = Normal(8f, FontStyle.Bold),
            ForeColor = Verde,
            AutoSize = true,
            Location = new Point(2, 0)
        });
        var t = Encabezado(titulo, 24);
        t.Location = new Point(0, 18);
        cab.Controls.Add(t);
        return cab;
    }

    /// <summary>Tarjeta redondeada con un título verde y una lista vertical de campos (etiqueta + control).</summary>
    public static PanelRedondo TarjetaCampos(string titulo, params (string et, Control ctrl)[] campos)
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Borde,
            AccentoVerde = true,
            BackColor = Panel,
            Padding = new Padding(18, 16, 18, 16),
            Margin = new Padding(0, 0, 14, 0)
        };

        var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Panel, AutoScroll = false };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        tl.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Titulo(10.5f),
            ForeColor = Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        foreach (var (et, ctrl) in campos)
        {
            tl.Controls.Add(new Label
            {
                Text = et.ToUpperInvariant(),
                Font = Normal(7.75f, FontStyle.Bold),
                ForeColor = Apagado,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 2)
            });
            ctrl.Dock = DockStyle.Fill;
            ctrl.Margin = new Padding(0, 0, 0, 8);
            tl.Controls.Add(ctrl);
        }

        card.Controls.Add(tl);
        return card;
    }

    /// <summary>Barra redondeada con controles alineados a la izquierda (botones, checkbox, etc.).</summary>
    public static PanelRedondo BarraCard(params Control[] controles)
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 12,
            ColorBorde = Borde,
            BackColor = Panel,
            Padding = new Padding(14, 8, 14, 8),
            Margin = new Padding(0, 14, 0, 0)
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Panel,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight
        };
        foreach (var c in controles)
        {
            c.Margin = new Padding(0, 4, 10, 4);
            flow.Controls.Add(c);
        }
        card.Controls.Add(flow);
        return card;
    }

    /// <summary>Tarjeta redondeada con un título arriba y un control (p. ej. una grilla) que llena el resto.</summary>
    public static PanelRedondo TarjetaContenedor(string titulo, Control contenido, Color? colorTitulo = null, bool accento = false)
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Borde,
            AccentoVerde = accento,
            BackColor = Panel,
            Padding = new Padding(16, 14, 16, 16),
            Margin = new Padding(0, 14, 0, 0)
        };
        contenido.Dock = DockStyle.Fill;
        card.Controls.Add(contenido);
        card.Controls.Add(new Label
        {
            Text = titulo.ToUpperInvariant(),
            Font = Titulo(11f),
            ForeColor = colorTitulo ?? Texto,
            Dock = DockStyle.Top,
            Height = 28
        });
        return card;
    }

    // ------------------------------------------------------------------ logo

    /// <summary>El logo del negocio (lo carga cada negocio desde Personalización). null si no hay.</summary>
    public static Image? Logo => Marca.Logo;

    /// <summary>Pone el logo como adorno fijo arriba a la derecha del formulario.</summary>
    public static PictureBox? AdornoLogo(Form f, int tam = 52, int margen = 12)
    {
        if (Logo is null) return null;
        var pb = new PictureBox
        {
            Image = Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(tam, tam),
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Math.Max(0, f.ClientSize.Width - tam - margen), margen)
        };
        f.Controls.Add(pb);
        pb.BringToFront();
        return pb;
    }

    /// <summary>El logo centrado, para la portada del login.</summary>
    public static PictureBox LogoCentral(int tam = 120)
        => new()
        {
            Image = Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(tam, tam),
            BackColor = Color.Transparent
        };

    public static void Aviso(string mensaje, string? titulo = null)
        => MessageBox.Show(mensaje, titulo ?? Marca.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(string mensaje, string? titulo = null)
        => MessageBox.Show(mensaje, titulo ?? Marca.Nombre, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirmar(string mensaje, string titulo = "Confirmar")
        => MessageBox.Show(mensaje, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question)
           == DialogResult.Yes;

    // ------------------------------------------------------------- utilidades

    internal static GraphicsPath RutaRedonda(Rectangle r, int radio)
    {
        var p = new GraphicsPath();
        if (radio <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
        int d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>Panel con esquinas redondeadas, borde fino y un acento verde arriba.</summary>
public sealed class PanelRedondo : Panel
{
    public int Radio { get; set; } = 12;
    public Color ColorBorde { get; set; } = Tema.Borde;
    public bool AccentoVerde { get; set; }

    public PanelRedondo()
    {
        DoubleBuffered = true;
        BackColor = Tema.Panel;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var ruta = Tema.RutaRedonda(new Rectangle(0, 0, Width, Height), Radio);
        Region = new Region(ruta);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        using (var ruta = Tema.RutaRedonda(r, Radio))
        using (var lapiz = new Pen(ColorBorde))
            e.Graphics.DrawPath(lapiz, ruta);

        if (AccentoVerde)
        {
            using var acento = new Pen(Tema.Verde, 2f);
            int m = Radio;
            e.Graphics.DrawLine(acento, m, 1, Width - m, 1);
        }
    }
}

/// <summary>Botón plano con esquinas redondeadas: relleno, texto y borde propios.</summary>
public sealed class BotonRedondo : Button
{
    public int Radio { get; set; } = 10;
    public Color ColorBorde { get; set; } = Color.Transparent;
    public int GrosorBorde { get; set; }
    public Color? Hover { get; set; }

    private bool _encima;

    public BotonRedondo()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _encima = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Tema.Fondo);

        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        var relleno = _encima && Hover.HasValue ? Hover.Value : BackColor;

        using (var ruta = Tema.RutaRedonda(r, Radio))
        {
            using (var brocha = new SolidBrush(relleno))
                g.FillPath(brocha, ruta);
            if (GrosorBorde > 0)
                using (var lapiz = new Pen(ColorBorde, GrosorBorde))
                    g.DrawPath(lapiz, ruta);
        }

        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        flags |= TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.TopLeft
                 || TextAlign == ContentAlignment.BottomLeft
            ? TextFormatFlags.Left
            : TextFormatFlags.HorizontalCenter;

        var caja = new Rectangle(
            Padding.Left + 2, Padding.Top,
            Width - Padding.Horizontal - 4, Height - Padding.Vertical);
        TextRenderer.DrawText(g, Text, Font, caja, ForeColor, flags);
    }
}
