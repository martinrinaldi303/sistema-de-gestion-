using System.Diagnostics;
using RetroGreenGestion.Datos;
using RetroGreenGestion.Exportacion;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Cierre de mes: genera el Excel para el contador y, si está configurado,
/// borra el detalle de ese mes.
///
/// La secuencia es siempre la misma y si algo falla NO se borra nada:
///   1. copia de seguridad de la base entera
///   2. se genera el Excel
///   3. se verifica que el archivo exista y tenga contenido
///   4. se graban los totales del mes (esto sobrevive a la purga)
///   5. recién ahí se borra el detalle
///   6. queda registrada la exportación
/// </summary>
public sealed class FrmCierre : Form
{
    private readonly ComboBox _periodo = Tema.Combo(170);
    private readonly TextBox _carpeta = Tema.Caja(520);
    private readonly CheckBox _borrar = new();
    private readonly Label _detalle = new();
    private readonly DataGridView _historial = Tema.Grilla();

    public FrmCierre()
    {
        Tema.AplicarA(this);
        Text = "Cierre de mes";

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
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        raiz.Controls.Add(Tema.CabeceraPantalla("Cierre de mes y exportación", "Cierre"), 0, 0);
        raiz.Controls.Add(TarjetaExportar(), 0, 1);
        raiz.Controls.Add(Tema.TarjetaContenedor("Exportaciones ya hechas", _historial, Tema.Verde), 0, 2);

        Controls.Add(raiz);

        _carpeta.Text = Bd.Config("carpeta_export", Path.Combine(Bd.Carpeta, "Exportaciones"));
        _periodo.DataSource = Repo.PeriodosConDatos();
        _periodo.SelectedIndexChanged += (s, e) => MostrarDetalle();
        MostrarDetalle();
        RecargarHistorial();
    }

    private Control TarjetaExportar()
    {
        var card = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(18, 16, 18, 16)
        };

        var tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Tema.Panel };
        tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        tl.Controls.Add(new Label
        {
            Text = "EXPORTAR EL MES PARA EL CONTADOR",
            Font = Tema.Titulo(11f),
            ForeColor = Tema.Verde,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        });
        tl.Controls.Add(new Label
        {
            Text = "Genera el Excel del mes. Guardalo en el pendrive o en Drive: si queda sólo en esta " +
                   "notebook, es el eslabón más fácil de perder.",
            Font = Tema.Normal(9),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        });

        var elegir = Tema.Boton("Elegir...");
        elegir.Width = 110;
        elegir.Margin = new Padding(0, 22, 0, 0);
        elegir.Click += (s, e) => ElegirCarpeta();

        var filaCampos = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, BackColor = Tema.Panel,
            Margin = new Padding(0, 0, 0, 6)
        };
        filaCampos.Controls.Add(Tema.Campo("Mes a exportar", _periodo));
        filaCampos.Controls.Add(Tema.Campo("Carpeta destino", _carpeta));
        filaCampos.Controls.Add(elegir);
        tl.Controls.Add(filaCampos);

        _detalle.Font = Tema.Normal(10);
        _detalle.ForeColor = Tema.Texto;
        _detalle.AutoSize = false;
        _detalle.Size = new Size(860, 48);
        _detalle.Margin = new Padding(0, 4, 0, 6);
        tl.Controls.Add(_detalle);

        _borrar.Text = "Borrar el detalle de ese mes después de exportarlo";
        _borrar.ForeColor = Tema.Violeta;
        _borrar.Font = Tema.Normal(9.5f);
        _borrar.AutoSize = true;
        _borrar.Checked = Bd.Config("borrar_al_exportar", "0") == "1";
        _borrar.CheckedChanged += (s, e) =>
            Bd.GuardarConfig("borrar_al_exportar", _borrar.Checked ? "1" : "0");
        tl.Controls.Add(_borrar);

        tl.Controls.Add(new Label
        {
            Text = "Los totales del mes se guardan igual, así el histórico y la comparación entre meses no " +
                   "se pierden. Lo que se borra es el detalle día por día.",
            Font = Tema.Normal(8.5f),
            ForeColor = Tema.Apagado,
            AutoSize = true,
            Margin = new Padding(18, 0, 0, 8)
        });

        var exportar = Tema.Boton("Exportar el mes", primario: true);
        exportar.Width = 230; exportar.Height = 42;
        exportar.Margin = new Padding(0, 0, 10, 0);
        exportar.Click += (s, e) => Exportar();

        var abrir = Tema.Boton("Abrir la carpeta");
        abrir.Width = 180; abrir.Height = 42;
        abrir.Click += (s, e) => AbrirCarpeta();

        var filaBotones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Panel };
        filaBotones.Controls.Add(exportar);
        filaBotones.Controls.Add(abrir);
        tl.Controls.Add(filaBotones);

        card.Controls.Add(tl);
        return card;
    }

    private void ElegirCarpeta()
    {
        using var d = new FolderBrowserDialog { SelectedPath = _carpeta.Text };
        if (d.ShowDialog(this) != DialogResult.OK) return;
        _carpeta.Text = d.SelectedPath;
        Bd.GuardarConfig("carpeta_export", d.SelectedPath);
    }

    private void AbrirCarpeta()
    {
        try
        {
            Directory.CreateDirectory(_carpeta.Text);
            Process.Start(new ProcessStartInfo(_carpeta.Text) { UseShellExecute = true });
        }
        catch (Exception ex) { Tema.Error("No se pudo abrir la carpeta.\n\n" + ex.Message); }
    }

    private void MostrarDetalle()
    {
        if (_periodo.SelectedItem is not string periodo) return;
        var t = Repo.Totales(periodo);
        var egresos = Repo.ListarEgresos(periodo).Rows.Count;

        _detalle.Text =
            $"{Exportador.NombreMes(periodo)}:  {t.CantServicios} servicios  ·  {egresos} gastos\n" +
            $"Ingresos {Bd.Plata(t.IngresosCent)}   ·   Egresos {Bd.Plata(t.EgresosCent)}   ·   " +
            $"Neto {Bd.Plata(t.NetoCent)}";
    }

    private void RecargarHistorial()
        => _historial.DataSource = Bd.Tabla(@"
            SELECT periodo                            AS Mes,
                   substr(fecha,1,10)                 AS ""Exportado el"",
                   usuario                            AS Usuario,
                   filas_servicios                    AS Servicios,
                   filas_egresos                      AS Gastos,
                   CASE borro_detalle WHEN 1 THEN 'Sí' ELSE 'No' END AS ""Se borró el detalle"",
                   archivo                            AS Archivo
            FROM Exportaciones ORDER BY id DESC;");

    private void Exportar()
    {
        if (_periodo.SelectedItem is not string periodo) return;

        var t = Repo.Totales(periodo);
        if (t.CantServicios == 0 && t.EgresosCent == 0)
        {
            Tema.Error($"{Exportador.NombreMes(periodo)} no tiene nada cargado.");
            return;
        }

        if (periodo == Repo.Periodo(DateTime.Today) && _borrar.Checked)
        {
            if (!Tema.Confirmar(
                "Estás exportando el mes en curso con el borrado activado.\n\n" +
                "Todo lo que carguen después de ahora va a quedar fuera del Excel.\n\n" +
                "¿Seguro?"))
                return;
        }

        var aviso =
            $"Se va a exportar {Exportador.NombreMes(periodo)}:\n\n" +
            $"    {t.CantServicios} servicios por {Bd.Plata(t.IngresosCent)}\n" +
            $"    Egresos por {Bd.Plata(t.EgresosCent)}\n" +
            $"    Neto {Bd.Plata(t.NetoCent)}\n\n" +
            $"El archivo queda en:\n{_carpeta.Text}\n\n";

        aviso += _borrar.Checked
            ? "DESPUÉS SE BORRA EL DETALLE DE ESE MES DE LA BASE.\n" +
              "Los totales se conservan. Se hace una copia de seguridad antes.\n\n¿Confirmás?"
            : "El detalle se conserva en la base.\n\n¿Confirmás?";

        if (!Tema.Confirmar(aviso, "Cierre de mes")) return;

        // Si además se va a BORRAR el detalle, pedimos la clave del administrador.
        if (_borrar.Checked &&
            !Autorizacion.PedirAdmin(this,
                $"Vas a BORRAR el detalle de {Exportador.NombreMes(periodo)} después de exportar.\n" +
                "(Los totales se conservan; el detalle día por día se elimina.)"))
            return;

        Cursor = Cursors.WaitCursor;
        try
        {
            // 1. copia de seguridad, siempre, borre o no
            var copia = Bd.CopiaDeSeguridad("cierre_" + periodo);

            // 2. Excel
            var r = Exportador.Generar(periodo, _carpeta.Text);

            // 3. verificación: si el archivo no quedó bien, no se borra nada
            var info = new FileInfo(r.Archivo);
            if (!info.Exists || info.Length < 1024)
                throw new IOException("El archivo de Excel no se generó correctamente.");

            // 4. totales, que sobreviven a la purga
            Repo.GuardarResumen(periodo);

            // 5. recién ahora, el borrado
            if (_borrar.Checked) Repo.BorrarDetalle(periodo);

            // 6. rastro
            Repo.RegistrarExportacion(periodo, r.Archivo, copia,
                r.FilasServicios, r.FilasEgresos, _borrar.Checked);

            Cursor = Cursors.Default;
            Tema.Aviso(
                $"Listo.\n\n" +
                $"Excel: {r.Archivo}\n" +
                $"{r.FilasServicios} servicios y {r.FilasEgresos} gastos.\n\n" +
                (_borrar.Checked
                    ? $"Se borró el detalle de {Exportador.NombreMes(periodo)}.\n" +
                      $"Copia de seguridad: {copia}"
                    : "El detalle quedó en la base."));

            _periodo.DataSource = Repo.PeriodosConDatos();
            MostrarDetalle();
            RecargarHistorial();
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            Tema.Error("No se pudo completar la exportación.\n\n" + ex.Message +
                       "\n\nNo se borró nada.");
        }
    }
}
