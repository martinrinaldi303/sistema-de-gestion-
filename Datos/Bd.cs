using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace RetroGreenGestion.Datos;

/// <summary>
/// Conexion a la base encriptada. La clave no se guarda en ningun lado:
/// se escribe al abrir el programa y vive en memoria mientras esta abierto.
/// Si alguien copia el archivo .db a un pendrive, no lo puede abrir con nada.
/// </summary>
public static class Bd
{
    private static string _clave = "";

    /// <summary>
    /// Solo para pruebas: si se fija, la base y el llavero viven acá en vez de en
    /// Documentos\Retro Green. Permite correr tests contra una base temporal sin
    /// tocar los datos reales. En produccion siempre queda en null.
    /// </summary>
    internal static string? CarpetaBaseOverride;

    public static string Carpeta
    {
        get
        {
            var c = CarpetaBaseOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Retro Green");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    public static string Ruta => Path.Combine(Carpeta, "gestion.db");

    public static string CarpetaCopias
    {
        get
        {
            var c = Path.Combine(Carpeta, "Copias de seguridad");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    public static bool YaExiste => File.Exists(Ruta);

    /// <summary>Quien esta usando el programa en esta sesion.</summary>
    public static string Usuario { get; set; } = "";

    /// <summary>Si el usuario de esta sesion es administrador (Ezequiel): puede crear/eliminar usuarios y tocar la config.</summary>
    public static bool EsAdmin { get; private set; }

    /// <summary>
    /// Codigos de recuperacion recien emitidos (al crear el admin o al migrar una
    /// base vieja). La pantalla de login los muestra UNA vez y despues los limpia.
    /// </summary>
    public static IReadOnlyList<string>? CodigosNuevos { get; private set; }

    // ---------------------------------------------------------------- login

    /// <summary>¿Ya hay usuarios creados? Si no, hay que crear el primer admin.</summary>
    public static bool HayUsuarios => Llavero.Existe && Llavero.HayUsuarios;

    /// <summary>Base vieja (cifrada con la clave directa) que todavia no tiene usuarios.</summary>
    public static bool BaseVieja => YaExiste && (!Llavero.Existe || !Llavero.HayUsuarios);

    /// <summary>Usuarios para el combo del login. De paso migra un llavero viejo de clave unica.</summary>
    public static IReadOnlyList<(string nombre, bool admin)> Usuarios()
    {
        if (Llavero.Existe) Llavero.MigrarEsquema();   // clave unica vieja -> Ezequiel admin
        return Llavero.ListaUsuarios();
    }

    /// <summary>Entra con un usuario existente y su contraseña.</summary>
    public static bool EntrarUsuario(string nombre, string clave)
    {
        CodigosNuevos = null;
        SqliteConnection.ClearAllPools();
        if (!Llavero.Existe) return false;

        // La contraseña de emergencia se reconoce antes de abrir nada: borra todo
        // y cierra el programa. Sin confirmacion, a proposito.
        if (Llavero.EsEmergencia(clave)) { Autodestruir(); Environment.Exit(0); }

        var r = Llavero.AbrirConUsuario(nombre, clave);
        if (r is not { } u) return false;
        if (!AbrirCon(u.dek)) return false;
        Usuario = nombre;
        EsAdmin = u.admin;
        return true;
    }

    /// <summary>
    /// Primer arranque: crea al administrador (Ezequiel) con su contraseña y abre
    /// la base nueva. Los codigos de recuperacion quedan en CodigosNuevos.
    /// </summary>
    public static bool CrearAdminYEntrar(string nombre, string clave)
    {
        CodigosNuevos = null;
        SqliteConnection.ClearAllPools();

        var (dek, codigos) = Llavero.Crear(nombre, clave, admin: true);
        if (!AbrirCon(dek))
        {
            try { File.Delete(Llavero.Ruta); } catch { /* nada que hacer */ }
            return false;
        }
        Usuario = nombre;
        EsAdmin = true;
        CodigosNuevos = codigos;
        AsegurarProfesional(nombre);
        return true;
    }

    /// <summary>
    /// Migra una base vieja (clave directa, sin usuarios): esa clave pasa a ser la
    /// del administrador. Devuelve false si la clave es incorrecta.
    /// </summary>
    public static bool MigrarYEntrar(string clave, string nombreAdmin = "Ezequiel")
    {
        CodigosNuevos = null;
        SqliteConnection.ClearAllPools();
        if (!YaExiste) return false;

        if (Llavero.Existe && Llavero.EsEmergencia(clave)) { Autodestruir(); Environment.Exit(0); }
        if (!AbrirCon(clave)) return false;   // la clave vieja era la llave de SQLCipher

        var dek = Llavero.NuevaDek();
        using (var cn = Conectar())
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA rekey = " + Literal(dek) + ";";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();
        _clave = dek;

        CodigosNuevos = Llavero.CrearConDek(dek, nombreAdmin, clave, admin: true);
        Usuario = nombreAdmin;
        EsAdmin = true;
        AsegurarProfesional(nombreAdmin);
        return true;
    }

    /// <summary>
    /// Recupera el acceso con un codigo de un solo uso y fija una contraseña nueva
    /// para el administrador. Al terminar, la base queda abierta como ese admin.
    /// </summary>
    public static bool RecuperarConCodigo(string codigo, string nuevaClave)
    {
        CodigosNuevos = null;
        SqliteConnection.ClearAllPools();
        if (!Llavero.Existe) return false;

        var r = Llavero.AbrirConCodigo(codigo);
        if (r == null) return false;
        var (dek, indice) = r.Value;
        if (!AbrirCon(dek)) return false;

        Llavero.MarcarCodigoUsado(indice);                 // un solo uso
        var admin = Llavero.PrimerAdmin() ?? "Ezequiel";
        Llavero.RecambiarClaveUsuario(admin, dek, nuevaClave);
        Usuario = admin;
        EsAdmin = true;
        return true;
    }

    /// <summary>Cambia la contraseña del usuario de esta sesion (no re-cifra la base).</summary>
    public static void CambiarClave(string nueva)
    {
        if (!Llavero.Existe || Usuario.Length == 0)
            throw new InvalidOperationException("Iniciá sesión primero.");
        Llavero.RecambiarClaveUsuario(Usuario, _clave, nueva);
    }

    /// <summary>¿Esta clave es la del usuario de esta sesion? (para confirmar antes de cambiarla).</summary>
    public static bool VerificarClave(string clave)
        => Llavero.Existe && Usuario.Length > 0 && Llavero.AbrirConUsuario(Usuario, clave) != null;

    /// <summary>¿Esta clave es la de algún administrador? (para autorizar acciones protegidas).</summary>
    public static bool VerificarAdmin(string clave) => Llavero.EsClaveDeAdmin(clave);

    // ------------------------------------------------------- usuarios (admin)

    public static IReadOnlyList<(string nombre, bool admin)> ListaUsuarios() => Llavero.ListaUsuarios();

    /// <summary>Crea un usuario nuevo (solo admin). Tambien lo deja como profesional activo.</summary>
    public static void CrearUsuario(string nombre, string clave, bool admin)
    {
        ExigirAdmin();
        Llavero.CrearUsuario(_clave, nombre, clave, admin);
        AsegurarProfesional(nombre.Trim());
    }

    /// <summary>Elimina un usuario (solo admin). No se puede eliminar a si mismo ni al ultimo admin.</summary>
    public static void EliminarUsuario(string nombre)
    {
        ExigirAdmin();
        if (nombre.Equals(Usuario, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("No podés eliminar tu propio usuario.");

        var obj = Llavero.ListaUsuarios()
            .FirstOrDefault(u => u.nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        if (obj.nombre is null) return;
        if (obj.admin && Llavero.CantidadAdmins <= 1)
            throw new InvalidOperationException("Tiene que quedar al menos un administrador.");

        Llavero.EliminarUsuario(nombre);
    }

    private static void ExigirAdmin()
    {
        if (!EsAdmin) throw new InvalidOperationException("Solo un administrador puede hacer esto.");
    }

    // Deja un Profesional activo con ese nombre, para poder atribuirle los servicios.
    // Si el usuario despues se elimina, el profesional queda (se conserva el historial).
    private static void AsegurarProfesional(string nombre)
    {
        try
        {
            Ejecutar(@"INSERT INTO Profesionales (nombre) VALUES ($n)
                       ON CONFLICT(nombre) DO UPDATE SET activo = 1;", ("$n", nombre));
        }
        catch { /* si falla, no es critico para el login */ }
    }

    /// <summary>Cuantos codigos de recuperacion quedan sin usar.</summary>
    public static int CodigosDisponibles => Llavero.CodigosDisponibles;

    /// <summary>Descarta los codigos viejos y emite un juego nuevo.</summary>
    public static IReadOnlyList<string> RegenerarCodigos() => Llavero.RegenerarCodigos(_clave);

    // ----------------------------------------------------- emergencia (panico)

    public static bool HayEmergencia => Llavero.HayEmergencia;

    /// <summary>
    /// Configura la contraseña de emergencia. La rechaza si coincide con la de
    /// algun usuario o con un codigo: eso evita que un login legitimo dispare el
    /// borrado por accidente (el peligro mas serio de esta funcion).
    /// </summary>
    public static void FijarEmergencia(string clave, bool borraCopias)
    {
        ExigirAdmin();
        if (!Llavero.Existe)
            throw new InvalidOperationException("No hay llavero: iniciá sesión primero.");
        if (Llavero.AlgunUsuario(clave))
            throw new ArgumentException("La contraseña de emergencia no puede ser la de ningún usuario.");
        if (Llavero.AbrirConCodigo(clave) != null)
            throw new ArgumentException("Esa combinación ya es un código de recuperación. Elegí otra.");

        Llavero.FijarEmergencia(clave, borraCopias);
    }

    public static void QuitarEmergencia() { ExigirAdmin(); Llavero.QuitarEmergencia(); }

    /// <summary>
    /// Borra la base y todo lo que permite abrirla. Primero el llavero (sin el,
    /// la DEK no existe y la base queda como ruido cifrado irrecuperable), despues
    /// la base y sus auxiliares, y las copias si asi se configuro.
    /// </summary>
    public static void Autodestruir()
    {
        var borraCopias = false;
        try { borraCopias = Llavero.EmergenciaBorraCopias; } catch { /* seguimos igual */ }

        SqliteConnection.ClearAllPools();   // suelta cualquier lock sobre los archivos
        _clave = "";

        // 1) El llavero PRIMERO: es la unica copia envuelta de la DEK.
        BorrarArchivo(Llavero.Ruta);

        // 2) La base y los auxiliares que deja SQLite / SQLCipher.
        BorrarArchivo(Ruta);
        BorrarArchivo(Ruta + "-wal");
        BorrarArchivo(Ruta + "-shm");
        BorrarArchivo(Ruta + "-journal");

        // 3) Las copias de seguridad (y sus llaveros), si se pidio.
        if (borraCopias)
        {
            try
            {
                foreach (var f in Directory.GetFiles(CarpetaCopias))
                    BorrarArchivo(f);
            }
            catch { /* best-effort */ }
        }
    }

    // Sobrescribe el contenido con ruido antes de borrar. Es best-effort: en un
    // SSD el borrado fisico no se puede garantizar, pero no hace falta: lo que
    // vuelve irrecuperable a la base es que el llavero (las llaves) ya no existe.
    private static void BorrarArchivo(string ruta)
    {
        try
        {
            if (!File.Exists(ruta)) return;
            try
            {
                var largo = new FileInfo(ruta).Length;
                if (largo > 0)
                {
                    using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Write, FileShare.None);
                    var buffer = new byte[64 * 1024];
                    long escrito = 0;
                    while (escrito < largo)
                    {
                        RandomNumberGenerator.Fill(buffer);
                        var n = (int)Math.Min(buffer.Length, largo - escrito);
                        fs.Write(buffer, 0, n);
                        escrito += n;
                    }
                    fs.Flush(flushToDisk: true);
                }
            }
            catch { /* si no se puede sobrescribir, igual lo borramos */ }
            File.Delete(ruta);
        }
        catch { /* best-effort: no hay a quien avisarle en una emergencia */ }
    }

    // Abre la base con una llave dada (DEK). Deja _clave puesta solo si funciono.
    private static bool AbrirCon(string dek)
    {
        var anterior = _clave;
        _clave = dek;
        try
        {
            using var cn = Conectar();
            CrearSiHaceFalta(cn);
            Migrar(cn);
            return true;
        }
        catch (SqliteException)
        {
            // Con SQLCipher, una llave incorrecta se ve como "file is not a database".
            _clave = anterior;
            return false;
        }
    }

    private static string Literal(string s) => "'" + s.Replace("'", "''") + "'";

    // ------------------------------------------------------------- conexion

    public static SqliteConnection Conectar()
    {
        var cad = new SqliteConnectionStringBuilder
        {
            DataSource = Ruta,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = _clave,          // esto activa SQLCipher
            ForeignKeys = true
        }.ToString();

        var cn = new SqliteConnection(cad);
        cn.Open();

        // Esta consulta es la que falla si la clave esta mal.
        using (var prueba = cn.CreateCommand())
        {
            prueba.CommandText = "SELECT count(*) FROM sqlite_master;";
            prueba.ExecuteScalar();
        }
        return cn;
    }

    // -------------------------------------------------------------- helpers

    public static int Ejecutar(string sql, params (string, object?)[] par)
    {
        using var cn = Conectar();
        return Ejecutar(cn, sql, par);
    }

    public static int Ejecutar(SqliteConnection cn, string sql, params (string, object?)[] par)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in par) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public static object? Escalar(string sql, params (string, object?)[] par)
    {
        using var cn = Conectar();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in par) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        var r = cmd.ExecuteScalar();
        return r == DBNull.Value ? null : r;
    }

    public static long EscalarLong(string sql, params (string, object?)[] par)
        => Convert.ToInt64(Escalar(sql, par) ?? 0L);

    public static DataTable Tabla(string sql, params (string, object?)[] par)
    {
        using var cn = Conectar();
        return Tabla(cn, sql, par);
    }

    public static DataTable Tabla(SqliteConnection cn, string sql, params (string, object?)[] par)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in par) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        using var lector = cmd.ExecuteReader();

        var cols = lector.FieldCount;
        var nombres = new string[cols];
        for (var i = 0; i < cols; i++) nombres[i] = lector.GetName(i);

        // Leemos TODO primero y recién después inferimos el tipo de cada columna
        // a partir de los valores reales. No usamos GetFieldType: con SQLite, una
        // columna cuyo primer valor es NULL se reporta como byte[], y entonces el
        // DataGridView arma una columna de IMAGEN y explota al mostrar un número.
        var filas = new List<object[]>();
        while (lector.Read())
        {
            var vals = new object[cols];
            lector.GetValues(vals);
            filas.Add(vals);
        }

        var dt = new DataTable();
        for (var i = 0; i < cols; i++)
            dt.Columns.Add(new DataColumn(nombres[i], InferirTipo(filas, i)) { AllowDBNull = true });

        foreach (var f in filas) dt.Rows.Add(f);
        return dt;
    }

    // Tipo seguro para una columna según sus valores: si hay texto -> string;
    // si hay decimales -> double; si solo enteros -> long; nunca byte[]/object.
    private static Type InferirTipo(List<object[]> filas, int col)
    {
        bool hayTexto = false, hayDouble = false, hayEntero = false;
        foreach (var f in filas)
        {
            switch (f[col])
            {
                case null or DBNull: break;
                case double or float or decimal: hayDouble = true; break;
                case long or int or short or byte or bool: hayEntero = true; break;
                default: hayTexto = true; break;
            }
        }
        if (hayTexto) return typeof(string);
        if (hayDouble) return typeof(double);
        if (hayEntero) return typeof(long);
        return typeof(string);   // columna toda NULL
    }

    /// <summary>Copia consistente de la base, con la encriptacion puesta.</summary>
    public static string CopiaDeSeguridad(string etiqueta)
    {
        var destino = Path.Combine(CarpetaCopias,
            $"gestion_{etiqueta}_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        using (var cn = Conectar())
        using (var cmd = cn.CreateCommand())
        {
            // VACUUM INTO da una copia integra. File.Copy puede llevarse
            // el archivo a medio escribir y dejarte un backup roto.
            cmd.CommandText = "VACUUM INTO " + Literal(destino) + ";";
            cmd.ExecuteNonQuery();
        }

        // El backup del .db no sirve de nada sin el llavero: sin el no se puede
        // desenvolver la DEK que lo abre. Lo copiamos pegado a la copia.
        if (Llavero.Existe)
        {
            try { File.Copy(Llavero.Ruta, destino + ".llave", overwrite: true); }
            catch { /* la copia de la base ya se hizo; no la invalidamos por esto */ }
        }

        LimpiarCopiasViejas();
        return destino;
    }

    private static void LimpiarCopiasViejas()
    {
        var viejas = new DirectoryInfo(CarpetaCopias)
            .GetFiles("gestion_*.db")
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(15);

        foreach (var f in viejas)
        {
            try { f.Delete(); } catch { /* si no se puede borrar, no es grave */ }
            // Se lleva tambien el llavero que viaja con esa copia.
            try { File.Delete(f.FullName + ".llave"); } catch { /* idem */ }
        }
    }

    // ------------------------------------------------------------- creacion

    private static void CrearSiHaceFalta(SqliteConnection cn)
    {
        var hay = Convert.ToInt64(Escalar(cn, "SELECT count(*) FROM sqlite_master WHERE type='table';"));
        if (hay > 0) return;

        using var tx = cn.BeginTransaction();
        Ejecutar(cn, Esquema.Sql);
        Sembrar(cn);
        tx.Commit();
    }

    /// <summary>
    /// Aplica las tablas agregadas despues de la primera version. Es idempotente
    /// (CREATE TABLE IF NOT EXISTS), asi que correrlo en cada apertura no molesta:
    /// una base vieja recibe las tablas nuevas y una nueva no cambia.
    /// </summary>
    private static void Migrar(SqliteConnection cn) => Ejecutar(cn, Esquema.SqlReportes);

    private static object? Escalar(SqliteConnection cn, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    /// <summary>Datos iniciales, tomados de la planilla que ya usan.</summary>
    private static void Sembrar(SqliteConnection cn)
    {
        foreach (var n in new[] { "Anahí", "Ezequiel", "Pedro" })
            Ejecutar(cn, "INSERT INTO Profesionales (nombre) VALUES ($n);", ("$n", n));

        var servicios = new (string Nombre, long Cent)[]
        {
            ("Corte", 1500000),
            ("Corte + Barba", 2000000),
            ("Barba", 800000),
            ("Color", 4500000),
            ("Reflejos", 6000000),
            ("Lavado y peinado", 900000)
        };
        foreach (var s in servicios)
            Ejecutar(cn, "INSERT INTO Servicios (nombre, precio_cent) VALUES ($n, $p);",
                ("$n", s.Nombre), ("$p", s.Cent));

        foreach (var n in new[] { "Efectivo", "Transferencia", "Tarjeta" })
            Ejecutar(cn, "INSERT INTO MediosPago (nombre) VALUES ($n);", ("$n", n));

        var categorias = new[] { "Alquiler", "Supermercado", "Productos y Herramientas", "Mantenimiento Local" };
        foreach (var n in categorias)
            Ejecutar(cn, "INSERT INTO CategoriasGasto (nombre) VALUES ($n);", ("$n", n));

        Ejecutar(cn, "INSERT INTO Config (clave, valor) VALUES ('borrar_al_exportar', '0');");
        Ejecutar(cn, "INSERT INTO Config (clave, valor) VALUES ('carpeta_export', $c);",
            ("$c", Path.Combine(Carpeta, "Exportaciones")));
    }

    // --------------------------------------------------------------- config

    public static string Config(string clave, string porDefecto = "")
        => Escalar("SELECT valor FROM Config WHERE clave = $c;", ("$c", clave))?.ToString() ?? porDefecto;

    public static void GuardarConfig(string clave, string valor)
        => Ejecutar("INSERT INTO Config (clave, valor) VALUES ($c, $v) " +
                    "ON CONFLICT(clave) DO UPDATE SET valor = $v;", ("$c", clave), ("$v", valor));

    // ---------------------------------------------------------------- plata

    public static long ACentavos(decimal m) => (long)Math.Round(m * 100m, MidpointRounding.AwayFromZero);
    public static decimal APesos(long cent) => cent / 100m;
    public static string Plata(long cent) => APesos(cent).ToString("C2");
}
