using RetroGreenGestion.Ui;
using RetroGreenGestion.Datos;
using Xunit;

namespace RetroGreenGestion.Tests;

/// <summary>Preguntas del asistente sobre lo que hizo cada profesional por semana.</summary>
public sealed class AsistenteSemanaTests : PruebaConBase
{
    // Miércoles 4/11/2026: la semana va del lunes 2/11 al domingo 8/11.
    private static readonly DateTime Hoy = new(2026, 11, 4);

    [Fact]
    public void CadaProfesionalPorSemana_NoSeConfundeConUnCliente()
    {
        AgregarServicio(Hoy, "Pedro", "Corte", 15000m);
        AgregarServicio(Hoy.AddDays(-1), "Anahí", "Barba", 8000m);

        var r = Asistente.Responder("¿Qué hizo cada profesional por semana?", Hoy);

        Assert.DoesNotContain("cliente", r.Texto);
        Assert.Contains("Pedro", r.Texto);
        Assert.Contains("Anahí", r.Texto);
        Assert.Contains("Semanas anteriores", r.Texto);
        Assert.Equal(2, r.Barras!.Count);
    }

    [Fact]
    public void Semana_VaDeLunesADomingo()
    {
        AgregarServicio(new DateTime(2026, 11, 2), "Pedro", "Corte", 10000m);   // lunes: entra
        AgregarServicio(new DateTime(2026, 11, 1), "Pedro", "Corte", 99999m);   // domingo anterior: no

        var r = Asistente.Responder("profesionales esta semana", Hoy);

        Assert.Equal(10000m, r.Barras!.Single().Valor);
    }

    [Fact]
    public void SemanaPasada_TomaLaSemanaAnterior()
    {
        AgregarServicio(new DateTime(2026, 10, 28), "Anahí", "Color", 45000m);
        AgregarServicio(Hoy, "Pedro", "Corte", 10000m);

        var r = Asistente.Responder("¿Quién hizo más la semana pasada?", Hoy);

        var barra = Assert.Single(r.Barras!);
        Assert.Equal("Anahí", barra.Etiqueta);
        Assert.Contains("26/10 al 01/11", r.Texto);
    }

    [Fact]
    public void UnProfesional_EstaSemana_SinAcentos_YConComision()
    {
        Bd.Ejecutar("UPDATE Profesionales SET comision_porcentaje = 10 WHERE nombre = 'Anahí';");
        AgregarServicio(Hoy, "Anahí", "Corte", 20000m);
        AgregarServicio(Hoy, "Pedro", "Corte", 20000m);

        var r = Asistente.Responder("que hizo anahi esta semana", Hoy);

        Assert.StartsWith("Anahí", r.Texto);
        Assert.Contains("50% del total", r.Texto);
        Assert.Contains("Comisión", r.Texto);
        Assert.Equal(7, r.Barras!.Count);            // lunes a domingo
        Assert.Equal(1m, r.Barras[2].Valor);         // miércoles
    }

    [Fact]
    public void ConLaPalabraCliente_SigueBuscandoAlCliente()
    {
        AgregarServicio(Hoy, "Anahí", "Corte", 10000m, cliente: "Pedro Ruiz");

        var r = Asistente.Responder("cliente pedro", Hoy);

        Assert.StartsWith("Pedro Ruiz", r.Texto);
    }
}
