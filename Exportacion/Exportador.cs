using System.Data;
using ClosedXML.Excel;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Exportacion;

public sealed record ResultadoExport(string Archivo, int FilasServicios, int FilasEgresos);

/// <summary>
/// Genera el Excel del mes para el contador.
/// Usa ClosedXML: escribe .xlsx nativo y NO necesita que haya Office instalado.
/// (Con Excel Interop harian falta Office y procesos de Excel colgados en memoria.)
/// </summary>
public static class Exportador
{
    // Los mismos colores del sitio, para que el archivo se vea de la marca.
    private static readonly XLColor Verde = XLColor.FromHtml("#00A651");
    private static readonly XLColor Oscuro = XLColor.FromHtml("#121212");
    private static readonly XLColor Suave = XLColor.FromHtml("#EFEFEF");

    public static ResultadoExport Generar(string periodo, string carpetaDestino)
    {
        Directory.CreateDirectory(carpetaDestino);
        var archivo = Path.Combine(carpetaDestino, $"Retro Green {periodo}.xlsx");

        var servicios = Repo.ListarServicios(periodo);
        var egresos = Repo.ListarEgresos(periodo);
        var totales = Repo.Totales(periodo);

        using var libro = new XLWorkbook();

        // ------------------------------------------------------------ Resumen
        var h = libro.Worksheets.Add("Resumen");
        Titulo(h, "A1:E1", $"RETRO GREEN - BARBERÍA & PELUQUERÍA");
        Titulo(h, "A2:E2", $"Resumen de {NombreMes(periodo)}", 12);

        h.Cell("A4").Value = "Total ingresos";
        h.Cell("B4").Value = Bd.APesos(totales.IngresosCent);
        h.Cell("A5").Value = "Total egresos";
        h.Cell("B5").Value = Bd.APesos(totales.EgresosCent);
        h.Cell("A6").Value = "Resultado neto";
        h.Cell("B6").Value = Bd.APesos(totales.NetoCent);
        h.Cell("A7").Value = "Servicios realizados";
        h.Cell("B7").Value = totales.CantServicios;

        h.Range("A4:A7").Style.Font.Bold = true;
        h.Range("B4:B6").Style.NumberFormat.Format = "$ #,##0.00";
        h.Cell("B6").Style.Font.Bold = true;
        h.Cell("B6").Style.Font.FontColor = totales.NetoCent >= 0 ? XLColor.DarkGreen : XLColor.DarkRed;

        var fila = 9;
        fila = Bloque(h, fila, "Rendimiento por profesional", Repo.PorProfesional(periodo));
        fila = Bloque(h, fila + 1, "Ingresos por medio de pago", Repo.PorMedioPago(periodo));
        Bloque(h, fila + 1, "Gastos por categoría", Repo.PorCategoria(periodo));

        h.Columns().AdjustToContents();

        // ------------------------------------------------------- Libro diario
        Hoja(libro, "Libro Diario", servicios, "Precio");
        Hoja(libro, "Egresos", egresos, "Monto");

        // ----------------------------------------------------------- Historico
        var hist = Repo.Historico();
        if (hist.Rows.Count > 0) Hoja(libro, "Histórico mensual", hist, "Ingresos", "Egresos", "Neto");

        libro.SaveAs(archivo);
        return new ResultadoExport(archivo, servicios.Rows.Count, egresos.Rows.Count);
    }

    // -------------------------------------------------------------- helpers

    private static void Titulo(IXLWorksheet h, string rango, string texto, double tam = 15)
    {
        var r = h.Range(rango).Merge();
        r.Value = texto;
        r.Style.Font.Bold = true;
        r.Style.Font.FontSize = tam;
        r.Style.Font.FontColor = XLColor.White;
        r.Style.Fill.BackgroundColor = tam >= 15 ? Oscuro : Verde;
        r.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        h.Row(r.FirstRow().RowNumber()).Height = 22;
    }

    private static int Bloque(IXLWorksheet h, int fila, string titulo, DataTable datos)
    {
        h.Cell(fila, 1).Value = titulo.ToUpperInvariant();
        h.Cell(fila, 1).Style.Font.Bold = true;
        h.Cell(fila, 1).Style.Font.FontColor = XLColor.FromHtml("#00A651");
        fila++;

        if (datos.Rows.Count == 0)
        {
            h.Cell(fila, 1).Value = "(sin datos)";
            return fila + 1;
        }

        for (var c = 0; c < datos.Columns.Count; c++)
        {
            var celda = h.Cell(fila, c + 1);
            celda.Value = datos.Columns[c].ColumnName;
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = Suave;
        }
        fila++;

        foreach (DataRow r in datos.Rows)
        {
            for (var c = 0; c < datos.Columns.Count; c++)
            {
                var celda = h.Cell(fila, c + 1);
                Poner(celda, r[c]);
                var nombre = datos.Columns[c].ColumnName;
                if (nombre is "Total" or "Comision" or "Comisión")
                    celda.Style.NumberFormat.Format = "$ #,##0.00";
            }
            fila++;
        }
        return fila;
    }

    private static void Hoja(XLWorkbook libro, string nombre, DataTable datos, params string[] columnasPlata)
    {
        var h = libro.Worksheets.Add(nombre);

        var visibles = datos.Columns.Cast<DataColumn>()
            .Where(c => c.ColumnName != "Id").ToList();

        for (var c = 0; c < visibles.Count; c++)
        {
            var celda = h.Cell(1, c + 1);
            celda.Value = visibles[c].ColumnName;
            celda.Style.Font.Bold = true;
            celda.Style.Font.FontColor = XLColor.White;
            celda.Style.Fill.BackgroundColor = Oscuro;
        }

        var fila = 2;
        foreach (DataRow r in datos.Rows)
        {
            for (var c = 0; c < visibles.Count; c++)
            {
                var celda = h.Cell(fila, c + 1);
                Poner(celda, r[visibles[c].ColumnName]);
                if (columnasPlata.Contains(visibles[c].ColumnName))
                    celda.Style.NumberFormat.Format = "$ #,##0.00";
            }
            fila++;
        }

        if (datos.Rows.Count > 0)
            h.Range(1, 1, fila - 1, visibles.Count).SetAutoFilter();

        h.SheetView.FreezeRows(1);
        h.Columns().AdjustToContents();
    }

    private static void Poner(IXLCell celda, object valor)
    {
        switch (valor)
        {
            case null or DBNull: celda.Value = ""; break;
            case double d: celda.Value = d; break;
            case decimal m: celda.Value = m; break;
            case long l: celda.Value = l; break;
            case int i: celda.Value = i; break;
            default: celda.Value = valor.ToString(); break;
        }
    }

    public static string NombreMes(string periodo)
    {
        if (DateTime.TryParse(periodo + "-01", out var f))
            return f.ToString("MMMM yyyy");
        return periodo;
    }
}
