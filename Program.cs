using System.Globalization;
using RetroGreenGestion.Datos;
using RetroGreenGestion.Ui;

namespace RetroGreenGestion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Registra el motor SQLite con SQLCipher (el que permite encriptar).
        SQLitePCL.Batteries_V2.Init();

        // Pesos argentinos y fechas dd/mm/aaaa en todo el programa.
        var cultura = new CultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentCulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
        Thread.CurrentThread.CurrentCulture = cultura;

        ApplicationConfiguration.Initialize();

        // El login ya deja fijada la identidad (Bd.Usuario / Bd.EsAdmin): no hace
        // falta volver a preguntar "¿quién sos?".
        using (var login = new FrmLogin())
            if (login.ShowDialog() != DialogResult.OK) return;

        Application.Run(new FrmPrincipal());
    }
}
