using System.Data;
using System.Text;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Reportes;

/// <summary>
/// El contenido del reporte de un día: los totales y el resumen HTML que va en el
/// cuerpo del correo. Es lógica pura (no toca SMTP ni la UI); los números salen de
/// las mismas consultas de Repo que usa el resto del programa, así no hay dos
/// verdades. El HTML NO incluye nombres de clientes ni notas: datos personales que
/// no hacen falta en un reporte económico.
/// </summary>
public sealed class ReporteDiario
{
    public DateTime Dia { get; }
    public long IngresosCent { get; }
    public long EgresosCent { get; }
    public long NetoCent => IngresosCent - EgresosCent;
    public int CantServicios { get; }
    public DataTable PorProfesional { get; }
    public DataTable PorMedioPago { get; }
    public DataTable PorCategoria { get; }

    public ReporteDiario(DateTime dia, long ingresosCent, long egresosCent, int cantServicios,
        DataTable porProfesional, DataTable porMedioPago, DataTable porCategoria)
    {
        Dia = dia.Date;
        IngresosCent = ingresosCent;
        EgresosCent = egresosCent;
        CantServicios = cantServicios;
        PorProfesional = porProfesional;
        PorMedioPago = porMedioPago;
        PorCategoria = porCategoria;
    }

    /// <summary>Arma el reporte leyendo los números del día desde la base.</summary>
    public static ReporteDiario DeLaBase(DateTime dia)
    {
        var (cant, ingresos) = Repo.ServiciosDia(dia);
        var egresos = Repo.EgresosDia(dia);
        return new ReporteDiario(dia, ingresos, egresos, cant,
            Repo.PorProfesionalDia(dia), Repo.PorMedioPagoDia(dia), Repo.PorCategoriaDia(dia));
    }

    public bool HuboMovimiento => CantServicios > 0 || EgresosCent > 0;

    public string Asunto => $"Retro Green · cierre del {Dia:dd/MM/yyyy}";

    // ------------------------------------------------------------------ HTML

    private const string Verde = "#00A651";
    private const string Oscuro = "#121212";

    public string Html()
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#1a1a1a;max-width:640px\">");
        sb.Append($"<div style=\"background:{Oscuro};padding:16px 20px;border-radius:10px 10px 0 0\">")
          .Append($"<span style=\"color:{Verde};font-size:12px;letter-spacing:1px\">RETRO GREEN · GESTIÓN</span><br>")
          .Append($"<span style=\"color:#fff;font-size:20px;font-weight:bold\">Cierre del día {Dia:dd/MM/yyyy}</span></div>");

        sb.Append("<div style=\"border:1px solid #e3e3e3;border-top:none;padding:18px 20px;border-radius:0 0 10px 10px\">");

        // Totales
        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin-bottom:8px\">");
        FilaTotal(sb, "Total ingresos", Bd.Plata(IngresosCent), Verde);
        FilaTotal(sb, "Total egresos", Bd.Plata(EgresosCent), "#b00020");
        FilaTotal(sb, "Resultado neto", Bd.Plata(NetoCent), NetoCent >= 0 ? Verde : "#b00020", negrita: true);
        FilaTotal(sb, "Servicios realizados", CantServicios.ToString(), Oscuro);
        sb.Append("</table>");

        if (!HuboMovimiento)
            sb.Append("<p style=\"color:#777;font-size:13px\">No se registró movimiento este día.</p>");

        Tabla(sb, "Rendimiento por profesional", PorProfesional);
        Tabla(sb, "Ingresos por medio de pago", PorMedioPago);
        Tabla(sb, "Gastos por categoría", PorCategoria);

        sb.Append("<p style=\"color:#999;font-size:11px;margin-top:16px\">")
          .Append("Reporte automático de Retro Green Gestión. El detalle día por día va en el Excel adjunto.")
          .Append("</p>");

        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static void FilaTotal(StringBuilder sb, string etiqueta, string valor, string color, bool negrita = false)
    {
        var peso = negrita ? "font-weight:bold;" : "";
        sb.Append("<tr>")
          .Append($"<td style=\"padding:6px 0;border-bottom:1px solid #eee;{peso}\">{Escapar(etiqueta)}</td>")
          .Append($"<td style=\"padding:6px 0;border-bottom:1px solid #eee;text-align:right;color:{color};{peso}\">{Escapar(valor)}</td>")
          .Append("</tr>");
    }

    private static void Tabla(StringBuilder sb, string titulo, DataTable datos)
    {
        sb.Append($"<h3 style=\"color:{Verde};font-size:13px;margin:18px 0 6px;text-transform:uppercase;letter-spacing:.5px\">{Escapar(titulo)}</h3>");

        if (datos.Rows.Count == 0)
        {
            sb.Append("<p style=\"color:#999;font-size:12px;margin:0\">(sin datos)</p>");
            return;
        }

        sb.Append("<table style=\"width:100%;border-collapse:collapse;font-size:13px\"><tr>");
        foreach (DataColumn c in datos.Columns)
            sb.Append($"<th style=\"text-align:left;padding:5px 6px;background:#f2f2f2;border-bottom:2px solid {Verde};font-size:11px\">{Escapar(c.ColumnName)}</th>");
        sb.Append("</tr>");

        foreach (DataRow r in datos.Rows)
        {
            sb.Append("<tr>");
            foreach (DataColumn c in datos.Columns)
            {
                var alinear = c.ColumnName is "Total" or "Comisión" or "Servicios" or "% del Total" or "% Comisión"
                    ? "right" : "left";
                sb.Append($"<td style=\"padding:5px 6px;border-bottom:1px solid #eee;text-align:{alinear}\">{Escapar(Celda(r[c], c.ColumnName))}</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");
    }

    // Formatea cada celda: plata con $ y % donde corresponde; el resto, tal cual.
    private static string Celda(object valor, string columna)
    {
        if (valor is null or DBNull) return "";
        if (columna is "Total" or "Comisión" && valor is double montoPesos)
            return Bd.Plata((long)Math.Round(montoPesos * 100));
        if (columna is "% del Total" or "% Comisión")
            return Convert.ToString(valor, System.Globalization.CultureInfo.CurrentCulture) + "%";
        return Convert.ToString(valor, System.Globalization.CultureInfo.CurrentCulture) ?? "";
    }

    private static string Escapar(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
