using System.Drawing.Drawing2D;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Gráficos dibujados a mano con GDI+.
///
/// Sin librerías ni controles de terceros: WinForms moderno ya no trae control de
/// gráficos, y agregar uno sumaría peso y dependencias para cuatro barras. Además
/// esto no consume nada: se dibuja cuando hay que redibujar, no hay ningún timer.
/// </summary>
public sealed class BarrasHorizontales : Control
{
    public sealed record Dato(string Etiqueta, decimal Valor, string? Pie = null);

    private List<Dato> _datos = new();
    public Color ColorBarra { get; set; } = Tema.Verde;
    public string VacioTexto { get; set; } = "Sin datos todavía";

    /// <summary>Si el valor es plata (formato $) o un conteo simple (ej. cantidad de servicios).</summary>
    public bool ComoMoneda { get; set; } = true;

    public BarrasHorizontales()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Panel;
        ForeColor = Tema.Texto;
    }

    public List<Dato> Datos
    {
        get => _datos;
        set { _datos = value ?? new List<Dato>(); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_datos.Count == 0)
        {
            TextRenderer.DrawText(g, VacioTexto, Tema.Normal(9), ClientRectangle,
                Tema.Apagado, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var maximo = _datos.Max(d => d.Valor);
        if (maximo <= 0) maximo = 1;

        const int izquierda = 12;
        var anchoEtiqueta = Math.Min(150, Math.Max(90, Width / 4));
        var altoFila = Math.Min(42, Math.Max(24, (Height - 16) / _datos.Count));
        var y = 10;

        using var fuenteEtiqueta = Tema.Normal(9.5f);
        using var fuenteValor = Tema.Normal(9.5f, FontStyle.Bold);
        using var fuentePie = Tema.Normal(8f);

        foreach (var d in _datos)
        {
            if (y + altoFila > Height) break;

            var zonaBarra = new Rectangle(
                izquierda + anchoEtiqueta + 8, y + 3,
                Math.Max(10, Width - izquierda - anchoEtiqueta - 150), altoFila - 12);

            // Riel de fondo, para que se vea la proporción aunque la barra sea chica.
            using (var riel = new SolidBrush(Tema.Superficie))
                g.FillRectangle(riel, zonaBarra);

            var ancho = (int)(zonaBarra.Width * (double)(d.Valor / maximo));
            if (ancho > 0)
            {
                var barra = new Rectangle(zonaBarra.X, zonaBarra.Y, ancho, zonaBarra.Height);
                using var pincel = new LinearGradientBrush(
                    barra, ColorBarra, Mezclar(ColorBarra, Color.White, 0.25f),
                    LinearGradientMode.Horizontal);
                g.FillRectangle(pincel, barra);
            }

            TextRenderer.DrawText(g, d.Etiqueta,
                fuenteEtiqueta,
                new Rectangle(izquierda, y, anchoEtiqueta, altoFila - 6),
                Tema.Texto,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            var textoValor = ComoMoneda ? d.Valor.ToString("C0") : d.Valor.ToString("#,0");
            TextRenderer.DrawText(g, textoValor, fuenteValor,
                new Rectangle(zonaBarra.Right + 8, y, 134, altoFila - 6),
                ColorBarra,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (d.Pie is { Length: > 0 })
                TextRenderer.DrawText(g, d.Pie, fuentePie,
                    new Rectangle(zonaBarra.Right + 8, y + altoFila / 2, 134, altoFila / 2),
                    Tema.Apagado, TextFormatFlags.Left | TextFormatFlags.Top);

            y += altoFila;
        }
    }

    internal static Color Mezclar(Color a, Color b, float cuanto)
        => Color.FromArgb(
            (int)(a.R + (b.R - a.R) * cuanto),
            (int)(a.G + (b.G - a.G) * cuanto),
            (int)(a.B + (b.B - a.B) * cuanto));
}

/// <summary>Evolución mes a mes: ingresos y egresos en barras, el neto como línea.</summary>
public sealed class BarrasMensuales : Control
{
    public sealed record Mes(string Etiqueta, decimal Ingresos, decimal Egresos, decimal Neto);

    private List<Mes> _datos = new();

    public BarrasMensuales()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Panel;
    }

    public List<Mes> Datos
    {
        get => _datos;
        set { _datos = value ?? new List<Mes>(); Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        if (_datos.Count == 0)
        {
            TextRenderer.DrawText(g, "Todavía no hay meses cargados", Tema.Normal(9),
                ClientRectangle, Tema.Apagado,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        const int margenAbajo = 46;
        const int margenArriba = 34;
        var alto = Height - margenAbajo - margenArriba;
        if (alto < 30) return;

        var maximo = _datos.Max(m => Math.Max(m.Ingresos, m.Egresos));
        if (maximo <= 0) maximo = 1;

        var anchoMes = (float)Width / _datos.Count;
        var anchoBarra = Math.Min(26f, anchoMes / 3.4f);

        using var fuenteMes = Tema.Normal(8f);
        using var fuenteTope = Tema.Normal(8f, FontStyle.Bold);
        using var lapizGuia = new Pen(Tema.Borde);

        // Guías horizontales
        for (var i = 1; i <= 3; i++)
        {
            var y = margenArriba + alto - alto * i / 4;
            g.DrawLine(lapizGuia, 0, y, Width, y);
        }

        var puntosNeto = new List<PointF>();

        for (var i = 0; i < _datos.Count; i++)
        {
            var m = _datos[i];
            var centro = anchoMes * i + anchoMes / 2;

            var altoIng = (float)(alto * (double)(m.Ingresos / maximo));
            var altoEgr = (float)(alto * (double)(m.Egresos / maximo));

            var rIng = new RectangleF(centro - anchoBarra - 2, margenArriba + alto - altoIng,
                anchoBarra, Math.Max(1, altoIng));
            var rEgr = new RectangleF(centro + 2, margenArriba + alto - altoEgr,
                anchoBarra, Math.Max(1, altoEgr));

            using (var p = new LinearGradientBrush(
                       new RectangleF(rIng.X, margenArriba, rIng.Width, alto),
                       BarrasHorizontales.Mezclar(Tema.Verde, Color.White, 0.2f), Tema.Verde,
                       LinearGradientMode.Vertical))
                g.FillRectangle(p, rIng);

            using (var p = new SolidBrush(Tema.Violeta))
                g.FillRectangle(p, rEgr);

            puntosNeto.Add(new PointF(centro,
                margenArriba + alto - (float)(alto * (double)(m.Neto / maximo))));

            TextRenderer.DrawText(g, EtiquetaMes(m.Etiqueta), fuenteMes,
                new Rectangle((int)(anchoMes * i), Height - margenAbajo + 6, (int)anchoMes, 18),
                Tema.Apagado, TextFormatFlags.HorizontalCenter);

            TextRenderer.DrawText(g, Corto(m.Ingresos), fuenteTope,
                new Rectangle((int)(anchoMes * i), Height - margenAbajo + 24, (int)anchoMes, 16),
                Tema.Texto, TextFormatFlags.HorizontalCenter);
        }

        // Línea del neto
        if (puntosNeto.Count > 1)
        {
            using var lapiz = new Pen(Color.White, 2f) { DashStyle = DashStyle.Dot };
            g.DrawLines(lapiz, puntosNeto.ToArray());
        }
        foreach (var p in puntosNeto)
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, p.X - 3, p.Y - 3, 6, 6);

        // Referencias
        Referencia(g, 8, 10, Tema.Verde, "Ingresos");
        Referencia(g, 96, 10, Tema.Violeta, "Egresos");
        Referencia(g, 184, 10, Color.White, "Neto");
    }

    private static void Referencia(Graphics g, int x, int y, Color color, string texto)
    {
        using (var b = new SolidBrush(color)) g.FillRectangle(b, x, y + 4, 10, 10);
        using var f = Tema.Normal(8.5f);
        TextRenderer.DrawText(g, texto, f, new Point(x + 14, y + 2), Tema.Apagado);
    }

    private static string EtiquetaMes(string periodo)
        => DateTime.TryParse(periodo + "-01", out var f)
            ? f.ToString("MMM").TrimEnd('.') + " " + f.ToString("yy")
            : periodo;

    /// <summary>1.590.000 -> "1,6 M". Para que entre abajo de la barra.</summary>
    internal static string Corto(decimal v)
        => v >= 1_000_000 ? (v / 1_000_000).ToString("0.#") + " M"
         : v >= 1_000 ? (v / 1_000).ToString("0") + " K"
         : v.ToString("0");
}
