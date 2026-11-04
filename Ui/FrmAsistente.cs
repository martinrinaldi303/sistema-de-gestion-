using System.Data;
using System.Globalization;
using System.Text;
using RetroGreenGestion.Datos;

namespace RetroGreenGestion.Ui;

/// <summary>
/// Lo que el asistente responde: un texto y, si corresponde, datos para un
/// gráfico (barras simples o la evolución mensual).
/// </summary>
public sealed record Respuesta(string Texto)
{
    public IReadOnlyList<BarrasHorizontales.Dato>? Barras { get; init; }
    public bool BarrasComoMoneda { get; init; } = true;
    public IReadOnlyList<BarrasMensuales.Mes>? Meses { get; init; }
    public string Titulo { get; init; } = "GRÁFICO";
}

/// <summary>
/// Asistente OFFLINE: entiende la pregunta por palabras clave y arma la consulta
/// contra la base. No usa internet ni ningún servicio externo — los datos del
/// negocio nunca salen de la máquina, igual que el resto del programa. Reconoce
/// un puñado de preguntas frecuentes; para algo nuevo se agrega un caso acá.
/// </summary>
public static class Asistente
{
    public static Respuesta Responder(string pregunta) => Responder(pregunta, DateTime.Today);

    /// <summary>Igual que <see cref="Responder(string)"/> pero con "hoy" fijo (para tests).</summary>
    public static Respuesta Responder(string pregunta, DateTime hoy)
    {
        var q = Normalizar(pregunta);
        if (q.Length == 0) return Ayuda();

        hoy = hoy.Date;
        var periodo = Repo.Periodo(hoy);
        var esMes = Contiene(q, "mes", "mensual");
        var esAyer = Contiene(q, "ayer");
        var semana = Semana(q, hoy);
        var pideCliente = Contiene(q, "cliente", "historial", "preferencia");

        // Un profesional puntual: "qué hizo Pedro esta semana", "cómo le fue a Anahí".
        // Va antes que clientes; para un cliente con el mismo nombre, "cliente pedro".
        if (!pideCliente && BuscarProfesional(q) is { } prof)
            return DeProfesional(prof, semana, hoy);

        // Todos los profesionales en una semana: "qué hizo cada profesional por semana",
        // "quién hizo más la semana pasada", "comisiones de esta semana".
        if (semana is { } sem &&
            (Contiene(q, "profesional", "peluquero", "barbero", "comision", "cada uno", "mejor") ||
             (Contiene(q, "quien") && Contiene(q, "hizo", "trabajo", "mas"))))
            return ProfesionalesSemana(sem, PideVariasSemanas(q));

        // Pregunta sobre un cliente puntual: "qué hizo martín", "cliente ana",
        // "preferencias de pedro", "historial de juan".
        var cliente = IntentarCliente(pregunta, q);
        if (cliente is not null) return cliente;

        // Gráficos (del mes en curso).
        if (Contiene(q, "profesional", "peluquero", "barbero", "comision", "mejor") ||
            (Contiene(q, "quien") && Contiene(q, "hizo", "trabajo", "mas")))
            return PorProfesional(periodo);

        if (Contiene(q, "medio", "efectivo", "transfer", "tarjeta") ||
            (Contiene(q, "como") && Contiene(q, "pag")))
            return PorMedioPago(periodo);

        if (Contiene(q, "evolucion", "tendencia", "venimos", "historico", "comparar") ||
            (Contiene(q, "ultimos", "ultimo") && Contiene(q, "mes", "meses")))
            return Evolucion();

        if (Contiene(q, "por dia", "dia a dia", "cada dia", "ultima semana", "semana") ||
            (Contiene(q, "ultimos", "ultimo") && Contiene(q, "dia", "dias")))
            return PorDia();

        if ((Contiene(q, "servicio", "corte") && Contiene(q, "mas", "top", "popular", "ranking", "pedido", "pedidos", "vendido"))
            || Contiene(q, "ranking"))
            return TopServicios(periodo);

        // Números.
        if (Contiene(q, "gast", "egreso"))
            return Egresos(periodo, esMes, esAyer, hoy);

        if (Contiene(q, "neto", "ganancia", "gane", "rinde", "rentab", "quedo"))
            return Neto(periodo);

        if (Contiene(q, "factur", "ingres", "cobr", "recaud", "vend", "caja", "entro"))
            return Ingresos(periodo, esMes, esAyer, hoy);

        if (Contiene(q, "corte", "servicio", "cuantos", "cuantas", "cliente", "atend", "hice", "hicimos", "trabaj"))
            return Cantidad(periodo, esMes, esAyer, hoy);

        return Ayuda();
    }

    // ------------------------------------------------------------- números

    private static Respuesta Cantidad(string periodo, bool esMes, bool esAyer, DateTime hoy)
    {
        if (esMes)
        {
            var t = Repo.Totales(periodo);
            return new Respuesta(
                $"Este mes se hicieron {t.CantServicios} servicios por {Bd.Plata(t.IngresosCent)}.")
            { Barras = BarrasPorDia(), BarrasComoMoneda = false, Titulo = "SERVICIOS POR DÍA — ÚLTIMOS 14 DÍAS" };
        }

        var dia = esAyer ? hoy.AddDays(-1) : hoy;
        var (cant, ing) = Repo.ServiciosDia(dia);
        var cuando = esAyer ? "Ayer" : "Hoy";
        var txt = cant == 0
            ? $"{cuando} no hay servicios cargados todavía."
            : $"{cuando} se hicieron {cant} servicios por {Bd.Plata(ing)}.";
        return new Respuesta(txt)
        { Barras = BarrasPorDia(), BarrasComoMoneda = false, Titulo = "SERVICIOS POR DÍA — ÚLTIMOS 14 DÍAS" };
    }

    private static Respuesta Ingresos(string periodo, bool esMes, bool esAyer, DateTime hoy)
    {
        if (esMes)
        {
            var t = Repo.Totales(periodo);
            return new Respuesta($"Este mes facturaste {Bd.Plata(t.IngresosCent)} en {t.CantServicios} servicios.")
            { Meses = MesesEvolucion(), Titulo = "EVOLUCIÓN — ÚLTIMOS MESES" };
        }

        var dia = esAyer ? hoy.AddDays(-1) : hoy;
        var (cant, ing) = Repo.ServiciosDia(dia);
        var cuando = esAyer ? "Ayer" : "Hoy";
        return new Respuesta($"{cuando} facturaste {Bd.Plata(ing)} en {cant} servicios.")
        { Barras = BarrasPorDia(), BarrasComoMoneda = false, Titulo = "SERVICIOS POR DÍA — ÚLTIMOS 14 DÍAS" };
    }

    private static Respuesta Egresos(string periodo, bool esMes, bool esAyer, DateTime hoy)
    {
        // Solo damos el gasto del día si lo pidió explícitamente ("hoy"/"ayer");
        // si no, el gasto se mira por mes, que es lo habitual.
        if (esAyer)
            return new Respuesta($"Ayer gastaste {Bd.Plata(Repo.EgresosDia(hoy.AddDays(-1)))}.");

        var t = Repo.Totales(periodo);
        return new Respuesta($"Este mes gastaste {Bd.Plata(t.EgresosCent)}.")
        { Barras = BarrasDesde(Repo.PorCategoria(periodo), "Categoria", "Total"),
          Titulo = "GASTOS POR CATEGORÍA — ESTE MES" };
    }

    private static Respuesta Neto(string periodo)
    {
        var t = Repo.Totales(periodo);
        var signo = t.NetoCent >= 0 ? "a favor" : "en contra";
        return new Respuesta(
            $"Este mes:\n• Ingresos: {Bd.Plata(t.IngresosCent)}\n• Egresos: {Bd.Plata(t.EgresosCent)}\n" +
            $"• Neto: {Bd.Plata(Math.Abs(t.NetoCent))} {signo}.")
        { Meses = MesesEvolucion(), Titulo = "EVOLUCIÓN — ÚLTIMOS MESES" };
    }

    // ------------------------------------------------------------- gráficos

    private static Respuesta PorProfesional(string periodo)
    {
        var dt = Repo.PorProfesional(periodo);
        if (dt.Rows.Count == 0) return new Respuesta("Este mes no hay servicios cargados todavía.");

        var barras = dt.Rows.Cast<DataRow>()
            .Select(r => new BarrasHorizontales.Dato(
                r["Profesional"].ToString()!, Dec(r, "Total"),
                $"{Convert.ToInt32(r["Servicios"])} serv."))
            .ToList();

        var primero = dt.Rows[0];
        return new Respuesta(
            $"Este mes, quien más facturó fue {primero["Profesional"]} con {Plata(Dec(primero, "Total"))}.")
        { Barras = barras, Titulo = "POR PROFESIONAL — ESTE MES" };
    }

    // ------------------------------------------------------- por semana

    /// <summary>Una semana de lunes a domingo, con cómo nombrarla en la respuesta.</summary>
    private readonly record struct Rango(DateTime Desde, DateTime Hasta, string Nombre)
    {
        public string Fechas => $"{Desde:dd/MM} al {Hasta:dd/MM}";
    }

    /// <summary>
    /// La semana que pide la pregunta (lunes como inicio, igual que la pantalla de
    /// Servicios), o null si no habla de semanas.
    /// </summary>
    private static Rango? Semana(string q, DateTime hoy)
    {
        if (!Contiene(q, "semana")) return null;
        var lunes = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        var pasada = Contiene(q, "semana pasada", "semana anterior", "anterior semana");
        if (pasada) lunes = lunes.AddDays(-7);
        return new Rango(lunes, lunes.AddDays(6), pasada ? "la semana pasada" : "esta semana");
    }

    private static bool PideVariasSemanas(string q)
        => Contiene(q, "por semana", "cada semana", "semanal", "semanas");

    /// <summary>Lo que hizo cada profesional en la semana; con "por semana" suma las 3 anteriores.</summary>
    private static Respuesta ProfesionalesSemana(Rango sem, bool variasSemanas)
    {
        var dt = Repo.PorProfesionalRango(sem.Desde, sem.Hasta);
        var sb = new StringBuilder();
        var titulo = $"POR PROFESIONAL — {sem.Nombre.ToUpperInvariant()} ({sem.Fechas})";

        if (dt.Rows.Count == 0)
            sb.Append($"No hay servicios cargados {sem.Nombre} ({sem.Fechas}).");
        else
        {
            sb.Append($"{Capital(sem.Nombre)} ({sem.Fechas}):");
            foreach (DataRow r in dt.Rows)
            {
                sb.Append($"\n• {r["Profesional"]}: {Convert.ToInt32(r["Servicios"])} serv. · {Plata(Dec(r, "Total"))}");
                if (Dec(r, "Comisión") > 0) sb.Append($" · comisión {Plata(Dec(r, "Comisión"))}");
            }
            var cant = dt.Rows.Cast<DataRow>().Sum(r => Convert.ToInt32(r["Servicios"]));
            var total = dt.Rows.Cast<DataRow>().Sum(r => Dec(r, "Total"));
            sb.Append($"\nTotal: {cant} servicios por {Plata(total)}.");
        }

        if (variasSemanas)
        {
            sb.Append("\n\nSemanas anteriores:");
            for (var i = 1; i <= 3; i++)
            {
                var desde = sem.Desde.AddDays(-7 * i);
                var hasta = desde.AddDays(6);
                var ant = Repo.PorProfesionalRango(desde, hasta);
                var detalle = ant.Rows.Count == 0
                    ? "sin servicios"
                    : string.Join(" · ", ant.Rows.Cast<DataRow>().Select(r =>
                        $"{r["Profesional"]} {Plata(Dec(r, "Total"))} ({Convert.ToInt32(r["Servicios"])})"));
                sb.Append($"\n• {desde:dd/MM}–{hasta:dd/MM}: {detalle}");
            }
        }

        var barras = dt.Rows.Cast<DataRow>()
            .Select(r => new BarrasHorizontales.Dato(
                r["Profesional"].ToString()!, Dec(r, "Total"),
                $"{Convert.ToInt32(r["Servicios"])} serv."))
            .ToList();
        return new Respuesta(sb.ToString()) { Barras = barras.Count > 0 ? barras : null, Titulo = titulo };
    }

    /// <summary>
    /// El profesional nombrado en la pregunta (sin importar acentos/mayúsculas), o null.
    /// Gana el nombre completo; si no, alcanza con una palabra del nombre ("pedro" → "Pedro Gómez").
    /// </summary>
    private static string? BuscarProfesional(string q)
    {
        var palabras = q.Split(SeparadoresPalabra, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var nombres = Repo.NombresProfesionales();

        var completo = nombres.FirstOrDefault(n =>
        {
            var partes = Normalizar(n).Split(SeparadoresPalabra, StringSplitOptions.RemoveEmptyEntries);
            return partes.Length > 0 && partes.All(palabras.Contains);
        });
        if (completo is not null) return completo;

        return nombres.FirstOrDefault(n => Normalizar(n)
            .Split(SeparadoresPalabra, StringSplitOptions.RemoveEmptyEntries)
            .Any(p => p.Length >= 3 && palabras.Contains(p)));
    }

    /// <summary>Lo que hizo un profesional en la semana pedida o, si no dice semana, en el mes.</summary>
    private static Respuesta DeProfesional(string prof, Rango? semana, DateTime hoy)
    {
        var rango = semana ?? new Rango(new DateTime(hoy.Year, hoy.Month, 1),
            new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1).AddDays(-1), "este mes");

        var fila = Repo.PorProfesionalRango(rango.Desde, rango.Hasta).Rows.Cast<DataRow>()
            .FirstOrDefault(r => string.Equals(r["Profesional"].ToString(), prof, StringComparison.OrdinalIgnoreCase));
        if (fila is null)
            return new Respuesta($"{prof} no tiene servicios cargados {rango.Nombre} ({rango.Fechas}).");

        var texto = $"{prof}, {rango.Nombre} ({rango.Fechas}): {Convert.ToInt32(fila["Servicios"])} servicios " +
                    $"por {Plata(Dec(fila, "Total"))} ({Dec(fila, "% del Total"):0.#}% del total del local).";
        if (Dec(fila, "Comisión") > 0)
            texto += $"\nComisión ({Dec(fila, "% Comisión"):0.#}%): {Plata(Dec(fila, "Comisión"))}.";

        var suyos = Repo.ListarServiciosRango(rango.Desde, rango.Hasta).Rows.Cast<DataRow>()
            .Where(r => string.Equals(r["Profesional"].ToString(), prof, StringComparison.OrdinalIgnoreCase))
            .ToList();

        List<BarrasHorizontales.Dato> barras;
        string titulo;
        if (semana is not null)
        {
            // Semana: un renglón por día, de lunes a domingo (también los días sin servicios).
            barras = Enumerable.Range(0, 7).Select(i =>
            {
                var dia = rango.Desde.AddDays(i);
                var delDia = suyos.Where(r => r["Fecha"].ToString() == Repo.Dia(dia)).ToList();
                return new BarrasHorizontales.Dato(
                    $"{DiasCortos[i]} {dia:dd/MM}", delDia.Count, Plata(delDia.Sum(r => Dec(r, "Precio"))));
            }).ToList();
            titulo = $"{prof.ToUpperInvariant()} — SERVICIOS POR DÍA ({rango.Fechas})";
        }
        else
        {
            barras = suyos.GroupBy(r => r["Servicio"].ToString()!)
                .Select(g => new BarrasHorizontales.Dato(g.Key, g.Count(), Plata(g.Sum(r => Dec(r, "Precio")))))
                .OrderByDescending(d => d.Valor).ToList();
            titulo = $"{prof.ToUpperInvariant()} — SERVICIOS DEL MES";
        }

        return new Respuesta(texto) { Barras = barras, BarrasComoMoneda = false, Titulo = titulo };
    }

    private static readonly string[] DiasCortos = { "lun", "mar", "mié", "jue", "vie", "sáb", "dom" };

    private static readonly char[] SeparadoresPalabra =
        { ' ', '\t', '?', '¿', '!', '¡', '.', ',', ';', ':', '-', '/', '(', ')' };

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static Respuesta TopServicios(string periodo)
    {
        var dt = Repo.TopServicios(periodo);
        if (dt.Rows.Count == 0) return new Respuesta("Este mes no hay servicios cargados todavía.");

        var barras = dt.Rows.Cast<DataRow>()
            .Select(r => new BarrasHorizontales.Dato(
                r["Servicio"].ToString()!, Convert.ToInt32(r["Veces"]),
                Plata(Dec(r, "Total"))))
            .ToList();

        var primero = dt.Rows[0];
        return new Respuesta(
            $"El servicio más pedido este mes es {primero["Servicio"]} ({Convert.ToInt32(primero["Veces"])} veces).")
        { Barras = barras, BarrasComoMoneda = false, Titulo = "SERVICIOS MÁS PEDIDOS — ESTE MES" };
    }

    private static Respuesta PorMedioPago(string periodo)
    {
        var dt = Repo.PorMedioPago(periodo);
        if (dt.Rows.Count == 0) return new Respuesta("Este mes no hay cobros cargados todavía.");

        return new Respuesta("Así se repartieron los cobros de este mes por medio de pago:")
        { Barras = BarrasDesde(dt, "Medio de pago", "Total"),
          Titulo = "MEDIOS DE PAGO — ESTE MES" };
    }

    private static Respuesta PorDia()
    {
        var barras = BarrasPorDia();
        if (barras.Count == 0) return new Respuesta("No hay servicios en los últimos días.");
        return new Respuesta("Servicios por día en las últimas dos semanas:")
        { Barras = barras, BarrasComoMoneda = false, Titulo = "SERVICIOS POR DÍA — ÚLTIMOS 14 DÍAS" };
    }

    private static Respuesta Evolucion()
    {
        var meses = MesesEvolucion();
        if (meses.Count == 0) return new Respuesta("Todavía no hay meses con datos para comparar.");

        var ultimo = meses[^1];
        return new Respuesta(
            $"Último mes ({ultimo.Etiqueta}): ingresos {ultimo.Ingresos:C0}, " +
            $"egresos {ultimo.Egresos:C0}, neto {ultimo.Neto:C0}.")
        { Meses = meses, Titulo = "EVOLUCIÓN — ÚLTIMOS MESES" };
    }

    // ------------------------------------------------------------- helpers

    private static List<BarrasHorizontales.Dato> BarrasPorDia()
        => Repo.UltimosDias(14).Rows.Cast<DataRow>()
            .Select(r => new BarrasHorizontales.Dato(
                FechaCorta(r["Fecha"].ToString()!), Convert.ToInt32(r["Servicios"]),
                Plata(Dec(r, "Total"))))
            .ToList();

    private static List<BarrasMensuales.Mes> MesesEvolucion()
        => Repo.Evolucion(12)
            .Select(m => new BarrasMensuales.Mes(
                m.Periodo, Bd.APesos(m.IngresosCent), Bd.APesos(m.EgresosCent), Bd.APesos(m.NetoCent)))
            .ToList();

    private static List<BarrasHorizontales.Dato> BarrasDesde(DataTable dt, string etiqueta, string valor)
        => dt.Rows.Cast<DataRow>()
            .Select(r => new BarrasHorizontales.Dato(r[etiqueta].ToString()!, Dec(r, valor)))
            .ToList();

    private static Respuesta Ayuda() => new(
        "Puedo responder cosas como:\n" +
        "• ¿Cuántos cortes hoy? / ayer\n" +
        "• ¿Cuánto facturé hoy / este mes?\n" +
        "• ¿Cuánto gasté este mes?\n" +
        "• ¿Cuál es el neto del mes?\n" +
        "• ¿Quién hizo más este mes?\n" +
        "• ¿Qué hizo cada profesional esta semana? / la semana pasada / por semana\n" +
        "• ¿Qué hizo <profesional> esta semana? / este mes\n" +
        "• ¿Qué servicios son los más pedidos?\n" +
        "• ¿Cómo me pagan? (medios de pago)\n" +
        "• ¿Cómo venimos los últimos meses?\n" +
        "• ¿Qué hizo <cliente>? / preferencias de <cliente>\n\n" +
        "Escribí tu pregunta o tocá uno de los botones de arriba.");

    // ------------------------------------------------------------- clientes

    private static readonly HashSet<string> StopCliente = new()
    {
        "que","hizo","hace","hacia","se","de","del","la","el","los","las","un","una","al","a",
        "cliente","clientes","historial","preferencia","preferencias","atendido","atendi","atiende","atendio",
        "suele","vino","hoy","ayer","mes","semana","dia","dias","cosas","mostrame","dame","ver","para","con",
        "y","le","mi","sobre","datos","es","fue","tiene","tuvo","hice","hicimos",
        "cuantos","cuantas","cuanto","cuanta","cuando","como","cual"
    };

    /// <summary>Si la pregunta es por un cliente, la responde; si no, devuelve null.</summary>
    private static Respuesta? IntentarCliente(string original, string q)
    {
        var cue = Contiene(q, "cliente", "historial", "preferencia", "atend", "suele", "vino")
               || (Contiene(q, "hizo", "hace", "hacia") && !Contiene(q, "quien", "mas", "mejor"));
        if (!cue) return null;
        // "qué hizo cada profesional" es del equipo, no de un cliente.
        if (Contiene(q, "profesional", "peluquero", "barbero", "cada uno", "cada una")) return null;

        var nombre = ExtraerNombreCliente(original);
        if (nombre.Length < 2) return null;

        var clientes = Repo.BuscarClientes(nombre);
        if (clientes.Rows.Count == 0)
            return new Respuesta($"No encontré ningún cliente llamado \"{nombre}\". " +
                "El nombre se carga al registrar el servicio, en \"Nombre del cliente\".");
        return RespuestaCliente(clientes);
    }

    private static string ExtraerNombreCliente(string original)
    {
        var tokens = (original ?? "").Split(
            new[] { ' ', '\t', '?', '¿', '!', '¡', '.', ',', ';', ':' },
            StringSplitOptions.RemoveEmptyEntries);
        var resto = tokens.Where(t => !StopCliente.Contains(Normalizar(t)));
        return string.Join(" ", resto).Trim();
    }

    private static Respuesta RespuestaCliente(DataTable clientes)
    {
        var cliente = clientes.Rows[0]["Cliente"].ToString()!;   // el de más visitas
        var det = Repo.HistorialCliente(cliente);

        string Moda(string col) => det.Rows.Cast<DataRow>()
            .Select(r => r[col].ToString()!.Trim()).Where(v => v.Length > 0)
            .GroupBy(v => v).OrderByDescending(g => g.Count())
            .Select(g => g.Key).FirstOrDefault() ?? "—";

        var notas = det.Rows.Cast<DataRow>()
            .Select(r => r["Nota"].ToString()!.Trim()).Where(n => n.Length > 0).Distinct().ToList();
        var total = det.Rows.Cast<DataRow>().Sum(r => Convert.ToDecimal(r["Precio"]));
        var ultima = det.Rows.Count > 0 ? FechaCorta(det.Rows[0]["Fecha"].ToString()!) : "—";

        var texto =
            $"{cliente}: {det.Rows.Count} visitas, última el {ultima}, gastó {Plata(total)}.\n" +
            $"Servicio habitual: {Moda("Servicio")}. Profesional habitual: {Moda("Profesional")}.\n" +
            (notas.Count > 0 ? "Notas / preferencias: " + string.Join(" · ", notas) : "Sin notas cargadas.");
        if (clientes.Rows.Count > 1)
            texto += $"\n(Coinciden {clientes.Rows.Count} clientes; te muestro el de más visitas.)";

        var barras = det.Rows.Cast<DataRow>()
            .GroupBy(r => r["Servicio"].ToString()!)
            .Select(g => new BarrasHorizontales.Dato(g.Key, g.Count()))
            .OrderByDescending(d => d.Valor).ToList();

        return new Respuesta(texto)
        {
            Barras = barras.Count > 0 ? barras : null,
            BarrasComoMoneda = false,
            Titulo = $"SERVICIOS DE {cliente.ToUpperInvariant()}"
        };
    }

    private static decimal Dec(DataRow r, string col) => r[col] is DBNull ? 0m : Convert.ToDecimal(r[col]);
    private static string Plata(decimal v) => v.ToString("C0");

    private static string FechaCorta(string iso)
        => DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture,
               DateTimeStyles.None, out var f) ? f.ToString("dd/MM") : iso;

    private static bool Contiene(string q, params string[] claves) => claves.Any(q.Contains);

    private static string Normalizar(string s)
    {
        s = (s ?? "").ToLowerInvariant().Trim();
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }
}

/// <summary>Pantalla del asistente: conversación a la izquierda, gráfico a la derecha.</summary>
public sealed class FrmAsistente : Form
{
    private readonly FlowLayoutPanel _conversa = new()
    {
        Dock = DockStyle.Left,
        Width = 400,
        BackColor = Tema.Fondo,
        AutoScroll = true,
        WrapContents = false,
        FlowDirection = FlowDirection.TopDown,
        Padding = new Padding(4, 4, 4, 4)
    };

    private readonly Label _tituloGraf = new()
    {
        Dock = DockStyle.Top,
        Height = 28,
        Font = Tema.Titulo(11f),
        ForeColor = Tema.Texto,
        Text = "GRÁFICO"
    };

    private readonly Panel _grafHost = new() { Dock = DockStyle.Fill, BackColor = Tema.Panel };
    private readonly TextBox _entrada = Tema.Caja();

    public FrmAsistente()
    {
        Tema.AplicarA(this);
        BackColor = Tema.Fondo;

        // --- barra de abajo: escribir la pregunta ---
        var barra = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo, Padding = new Padding(0, 12, 0, 0) };
        var filaEntrada = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Tema.Fondo };
        filaEntrada.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filaEntrada.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        _entrada.Dock = DockStyle.Fill;
        _entrada.Font = Tema.Normal(11f);
        _entrada.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Preguntar(_entrada.Text); }
        };
        var preguntar = Tema.Boton("Preguntar", primario: true);
        preguntar.Dock = DockStyle.Fill;
        preguntar.Margin = new Padding(10, 0, 0, 0);
        preguntar.Click += (s, e) => Preguntar(_entrada.Text);

        filaEntrada.Controls.Add(_entrada, 0, 0);
        filaEntrada.Controls.Add(preguntar, 1, 0);
        barra.Controls.Add(filaEntrada);

        // --- chips de preguntas frecuentes ---
        var chips = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(0, 6, 0, 6)
        };
        string[] sugerencias =
        {
            "¿Cuántos cortes hoy?", "¿Cuánto facturé hoy?", "Ingresos del mes", "Gastos del mes",
            "Neto del mes", "¿Quién hizo más?", "Profesionales por semana", "Servicios más pedidos", "Medios de pago", "Últimos meses"
        };
        using (var fuente = Tema.Titulo(10.5f))
            foreach (var texto in sugerencias)
            {
                var chip = Tema.Boton(texto);
                chip.Height = 32;
                chip.AutoSize = false;
                chip.Width = TextRenderer.MeasureText(texto.ToUpperInvariant(), fuente).Width + 34;
                chip.Margin = new Padding(0, 0, 8, 8);
                var copia = texto;
                chip.Click += (s, e) => Preguntar(copia);
                chips.Controls.Add(chip);
            }

        // --- gráfico (derecha) ---
        var grafCard = new PanelRedondo
        {
            Dock = DockStyle.Fill,
            Radio = 14,
            ColorBorde = Tema.Borde,
            AccentoVerde = true,
            BackColor = Tema.Panel,
            Padding = new Padding(16, 14, 16, 16),
            Margin = new Padding(14, 0, 0, 0)
        };
        _grafHost.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Preguntá algo para ver un gráfico.",
            Font = Tema.Normal(9.5f),
            ForeColor = Tema.Apagado,
            TextAlign = ContentAlignment.MiddleCenter
        });
        grafCard.Controls.Add(_grafHost);
        grafCard.Controls.Add(_tituloGraf);

        var cuerpo = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fondo };
        cuerpo.Controls.Add(grafCard);     // fill
        cuerpo.Controls.Add(_conversa);    // left

        // Layout determinístico en filas: encabezado / chips / conversación+gráfico / entrada.
        // (Antes el encabezado estaba en "Fill" y tapaba la conversación y el gráfico.)
        var raiz = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Tema.Fondo,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 20, 24, 16)
        };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        raiz.Controls.Add(Tema.CabeceraPantalla("Asistente", "Consultas"), 0, 0);
        raiz.Controls.Add(chips, 0, 1);
        raiz.Controls.Add(cuerpo, 0, 2);
        raiz.Controls.Add(barra, 0, 3);
        Controls.Add(raiz);

        Load += (s, e) =>
        {
            AgregarBurbuja(
                "Hola 👋 Soy el asistente. Preguntame sobre tus datos: cuántos cortes " +
                "hoy, cuánto facturaste este mes, quién hizo más, etc.", usuario: false);
            _entrada.Focus();
        };
    }

    private void Preguntar(string texto)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length == 0) return;

        AgregarBurbuja(texto, usuario: true);
        _entrada.Clear();

        Respuesta r;
        try { r = Asistente.Responder(texto); }
        catch (Exception ex) { r = new Respuesta("No pude consultar eso: " + ex.Message); }

        AgregarBurbuja(r.Texto, usuario: false);
        MostrarGrafico(r);
        _entrada.Focus();
    }

    private void AgregarBurbuja(string texto, bool usuario)
    {
        var l = new Label
        {
            Text = texto,
            AutoSize = true,
            MaximumSize = new Size(332, 0),
            Font = Tema.Normal(9.75f),
            ForeColor = usuario ? Tema.SobreVerde : Tema.Texto,
            BackColor = usuario ? Tema.Verde : Tema.Superficie,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(usuario ? 44 : 4, 4, usuario ? 4 : 44, 4)
        };
        _conversa.Controls.Add(l);
        _conversa.ScrollControlIntoView(l);
    }

    private void MostrarGrafico(Respuesta r)
    {
        _grafHost.Controls.Clear();
        _tituloGraf.Text = r.Titulo;

        if (r.Meses is { Count: > 0 } meses)
        {
            _grafHost.Controls.Add(new BarrasMensuales { Dock = DockStyle.Fill, Datos = meses.ToList() });
        }
        else if (r.Barras is { Count: > 0 } barras)
        {
            _grafHost.Controls.Add(new BarrasHorizontales
            {
                Dock = DockStyle.Fill,
                ComoMoneda = r.BarrasComoMoneda,
                Datos = barras.ToList()
            });
        }
        else
        {
            _grafHost.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Esta consulta no tiene gráfico.",
                Font = Tema.Normal(9.5f),
                ForeColor = Tema.Apagado,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
    }
}
