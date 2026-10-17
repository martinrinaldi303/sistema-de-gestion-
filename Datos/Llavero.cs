using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RetroGreenGestion.Datos;

/// <summary>
/// El "llavero": un archivo chico (gestion.llave) que vive al lado de la base.
///
/// La base NO se cifra con la contraseña del negocio, sino con una llave larga
/// al azar (la DEK). Esa DEK se guarda acá adentro, pero NUNCA en claro: se
/// guarda envuelta (cifrada) varias veces —
///   - una vez con la contraseña del negocio,
///   - una vez con cada codigo de recuperacion.
///
/// Para abrir la base hay que desenvolver la DEK con la contraseña o con un
/// codigo. El que copie este archivo a un pendrive no saca nada: para desenvolver
/// la DEK necesita la contraseña o un codigo, y cada intento pasa por PBKDF2
/// (210.000 vueltas) + AES-GCM, asi que probar a lo bruto es carisimo.
///
/// Esto es lo que hace POSIBLE la recuperacion sin romper el cifrado: la
/// contraseña se puede cambiar o recuperar porque la llave real de la base (la
/// DEK) no cambia, solo cambia con que se la envuelve.
/// </summary>
public static class Llavero
{
    private const int Iteraciones    = 210_000;  // PBKDF2-SHA256 (recomendacion OWASP)
    private const int TamSalt        = 16;        // 128 bits
    private const int TamKek         = 32;        // 256 bits (clave AES)
    private const int TamNonce       = 12;        // GCM
    private const int TamTag         = 16;        // GCM
    private const int CantidadCodigos = 10;

    // Alfabeto sin caracteres que se confunden al leer a mano (0/O, 1/I/L).
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Ruta => Path.Combine(Bd.Carpeta, "gestion.llave");
    public static bool Existe => File.Exists(Ruta);

    // ----------------------------------------------------------------- crear

    /// <summary>Instalacion nueva: genera DEK, el primer usuario (admin) y codigos.</summary>
    public static (string dek, IReadOnlyList<string> codigos) Crear(string nombre, string clave, bool admin)
    {
        var dek = NuevaDek();
        var codigos = CrearConDek(dek, nombre, clave, admin);
        return (dek, codigos);
    }

    /// <summary>
    /// Migracion de una base vieja: ya tenemos la DEK (lo que antes era la clave
    /// directa, re-encriptada), solo falta escribir el llavero con su primer usuario.
    /// </summary>
    public static IReadOnlyList<string> CrearConDek(string dek, string nombre, string clave, bool admin)
    {
        var codigos = GenerarCodigos();
        var arch = new Archivo { Iteraciones = Iteraciones };
        arch.Usuarios.Add(new Usuario { Nombre = nombre.Trim(), EsAdmin = admin, Sobre = Envolver(dek, clave) });
        foreach (var c in codigos)
            arch.Codigos.Add(Envolver(dek, Normalizar(c)));

        Guardar(arch);
        return codigos;
    }

    public static string NuevaDek() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // --------------------------------------------------------------- usuarios

    /// <summary>
    /// Pasa un llavero viejo (una sola clave compartida) al esquema de usuarios:
    /// esa clave queda como el admin (Ezequiel). Devuelve true si migro algo.
    /// </summary>
    public static bool MigrarEsquema(string nombreAdmin = "Ezequiel")
    {
        var arch = Cargar();
        if (arch.Usuarios.Count > 0) return false;                 // ya esta migrado
        if (arch.Clave is not { } vieja || vieja.Cifrado.Length == 0) return false;

        arch.Usuarios.Add(new Usuario { Nombre = nombreAdmin, EsAdmin = true, Sobre = vieja });
        arch.Clave = null;
        Guardar(arch);
        return true;
    }

    public static bool HayUsuarios => Existe && Cargar().Usuarios.Count > 0;

    public static IReadOnlyList<(string nombre, bool admin)> ListaUsuarios()
        => Existe
            ? Cargar().Usuarios.Select(u => (u.Nombre, u.EsAdmin)).ToList()
            : new List<(string, bool)>();

    public static int CantidadAdmins => Existe ? Cargar().Usuarios.Count(u => u.EsAdmin) : 0;

    public static string? PrimerAdmin()
        => Existe ? Cargar().Usuarios.FirstOrDefault(u => u.EsAdmin)?.Nombre : null;

    /// <summary>Agrega un usuario nuevo: su contraseña envuelve la misma DEK.</summary>
    public static void CrearUsuario(string dek, string nombre, string clave, bool admin)
    {
        nombre = nombre.Trim();
        var arch = Cargar();
        if (arch.Usuarios.Any(u => u.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Ya existe un usuario \"{nombre}\".");
        arch.Usuarios.Add(new Usuario { Nombre = nombre, EsAdmin = admin, Sobre = Envolver(dek, clave) });
        Guardar(arch);
    }

    public static void EliminarUsuario(string nombre)
    {
        var arch = Cargar();
        arch.Usuarios.RemoveAll(u => u.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase));
        Guardar(arch);
    }

    // ----------------------------------------------------------------- abrir

    /// <summary>Desenvuelve la DEK con el usuario y su contraseña. null = no coincide.</summary>
    public static (string dek, bool admin)? AbrirConUsuario(string nombre, string clave)
    {
        var arch = Cargar();
        var u = arch.Usuarios.FirstOrDefault(
            x => x.Nombre.Equals((nombre ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        if (u is null) return null;
        var dek = Desenvolver(u.Sobre, clave, arch.Iteraciones);
        return dek is null ? null : (dek, u.EsAdmin);
    }

    /// <summary>true si la clave abre la DEK de ALGUN usuario (para validar la de emergencia).</summary>
    public static bool AlgunUsuario(string clave)
    {
        var arch = Cargar();
        return arch.Usuarios.Any(u => Desenvolver(u.Sobre, clave, arch.Iteraciones) != null);
    }

    /// <summary>true si la clave es la de algún usuario ADMINISTRADOR.</summary>
    public static bool EsClaveDeAdmin(string clave)
    {
        if (!Existe || string.IsNullOrEmpty(clave)) return false;
        var arch = Cargar();
        return arch.Usuarios.Any(u => u.EsAdmin && Desenvolver(u.Sobre, clave, arch.Iteraciones) != null);
    }

    /// <summary>
    /// Prueba el codigo contra cada sobre no usado. Devuelve la DEK y el indice
    /// del sobre que abrio (para marcarlo despues), o null si el codigo no sirve.
    /// </summary>
    public static (string dek, int indice)? AbrirConCodigo(string codigo)
    {
        var arch = Cargar();
        var normal = Normalizar(codigo);
        if (normal.Length == 0) return null;

        for (var i = 0; i < arch.Codigos.Count; i++)
        {
            if (arch.Codigos[i].Usado) continue;
            var dek = Desenvolver(arch.Codigos[i], normal, arch.Iteraciones);
            if (dek != null) return (dek, i);
        }
        return null;
    }

    // -------------------------------------------------------------- mantener

    /// <summary>Marca un codigo como usado (un solo uso).</summary>
    public static void MarcarCodigoUsado(int indice)
    {
        var arch = Cargar();
        if (indice >= 0 && indice < arch.Codigos.Count)
        {
            arch.Codigos[indice].Usado = true;
            Guardar(arch);
        }
    }

    /// <summary>
    /// Vuelve a envolver la DEK de un usuario bajo una contraseña nueva. NO toca
    /// la base: es instantaneo. La contraseña vieja de ese usuario deja de servir.
    /// </summary>
    public static void RecambiarClaveUsuario(string nombre, string dek, string nuevaClave)
    {
        var arch = Cargar();
        var u = arch.Usuarios.FirstOrDefault(x => x.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No existe el usuario \"{nombre}\".");
        u.Sobre = Envolver(dek, nuevaClave);
        Guardar(arch);
    }

    /// <summary>Descarta los codigos viejos y emite un juego nuevo.</summary>
    public static IReadOnlyList<string> RegenerarCodigos(string dek)
    {
        var arch = Cargar();
        var codigos = GenerarCodigos();
        arch.Codigos.Clear();
        foreach (var c in codigos)
            arch.Codigos.Add(Envolver(dek, Normalizar(c)));
        Guardar(arch);
        return codigos;
    }

    public static int CodigosDisponibles => Existe ? Cargar().Codigos.Count(c => !c.Usado) : 0;

    // ----------------------------------------------------- emergencia (panico)

    /// <summary>¿Hay una contraseña de emergencia configurada?</summary>
    public static bool HayEmergencia => Existe && Cargar().Emergencia != null;

    /// <summary>Si al dispararse tambien tiene que borrar las copias de seguridad.</summary>
    public static bool EmergenciaBorraCopias => Existe && Cargar().EmergenciaBorraCopias;

    /// <summary>
    /// Guarda SOLO un verificador (hash salteado) de la contraseña de emergencia,
    /// nunca la contraseña en si. No envuelve ninguna llave: no hay nada que abrir,
    /// solo reconocerla al vuelo en el login.
    /// </summary>
    public static void FijarEmergencia(string clave, bool borraCopias)
    {
        var arch = Cargar();
        var salt = RandomNumberGenerator.GetBytes(TamSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(clave, salt, Iteraciones, HashAlgorithmName.SHA256, TamKek);
        arch.Emergencia = new Verificador
        {
            Salt = Convert.ToBase64String(salt),
            Hash = Convert.ToBase64String(hash)
        };
        arch.EmergenciaBorraCopias = borraCopias;
        Guardar(arch);
    }

    public static void QuitarEmergencia()
    {
        var arch = Cargar();
        arch.Emergencia = null;
        arch.EmergenciaBorraCopias = false;
        Guardar(arch);
    }

    /// <summary>true si la clave tipeada es la contraseña de emergencia. Nunca lanza.</summary>
    public static bool EsEmergencia(string clave)
    {
        if (!Existe || string.IsNullOrEmpty(clave)) return false;
        var arch = Cargar();
        if (arch.Emergencia is not { } v) return false;
        try
        {
            var salt = Convert.FromBase64String(v.Salt);
            var esperado = Convert.FromBase64String(v.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(clave, salt, arch.Iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(actual, esperado);
        }
        catch { return false; }
    }

    // -------------------------------------------------------------- codigos

    private static List<string> GenerarCodigos()
    {
        var lista = new List<string>(CantidadCodigos);
        for (var i = 0; i < CantidadCodigos; i++) lista.Add(NuevoCodigo());
        return lista;
    }

    /// <summary>Un codigo tipo "A7K3-9MNP-XW4R" (~59 bits de entropia).</summary>
    private static string NuevoCodigo()
    {
        var sb = new StringBuilder(14);
        for (var i = 0; i < 12; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)]);
        }
        return sb.ToString();
    }

    /// <summary>Deja el codigo como se guarda y se compara: mayusculas, sin guiones ni espacios.</summary>
    public static string Normalizar(string entrada)
        => new((entrada ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    // -------------------------------------------------------------- cripto

    private static Sobre Envolver(string dek, string secreto)
    {
        var salt = RandomNumberGenerator.GetBytes(TamSalt);
        var kek = Rfc2898DeriveBytes.Pbkdf2(secreto, salt, Iteraciones, HashAlgorithmName.SHA256, TamKek);

        var datos = Encoding.UTF8.GetBytes(dek);
        var nonce = RandomNumberGenerator.GetBytes(TamNonce);
        var cifrado = new byte[datos.Length];
        var tag = new byte[TamTag];

        using (var gcm = new AesGcm(kek, TamTag))
            gcm.Encrypt(nonce, datos, cifrado, tag);

        // El sobre guarda cifrado + tag pegados, asi es un solo blob.
        var blob = new byte[cifrado.Length + TamTag];
        Buffer.BlockCopy(cifrado, 0, blob, 0, cifrado.Length);
        Buffer.BlockCopy(tag, 0, blob, cifrado.Length, TamTag);

        return new Sobre
        {
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Cifrado = Convert.ToBase64String(blob)
        };
    }

    private static string? Desenvolver(Sobre sobre, string secreto, int iteraciones)
    {
        try
        {
            var salt = Convert.FromBase64String(sobre.Salt);
            var nonce = Convert.FromBase64String(sobre.Nonce);
            var blob = Convert.FromBase64String(sobre.Cifrado);
            if (blob.Length < TamTag) return null;

            var kek = Rfc2898DeriveBytes.Pbkdf2(secreto, salt, iteraciones, HashAlgorithmName.SHA256, TamKek);

            var cifrado = new byte[blob.Length - TamTag];
            var tag = new byte[TamTag];
            Buffer.BlockCopy(blob, 0, cifrado, 0, cifrado.Length);
            Buffer.BlockCopy(blob, cifrado.Length, tag, 0, TamTag);

            var datos = new byte[cifrado.Length];
            using (var gcm = new AesGcm(kek, TamTag))
                gcm.Decrypt(nonce, cifrado, tag, datos);   // tag que no cuadra => lanza

            return Encoding.UTF8.GetString(datos);
        }
        catch (CryptographicException)
        {
            // Tag invalido = secreto incorrecto. No es un error a mostrar.
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // -------------------------------------------------------------- archivo

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private static Archivo Cargar()
    {
        if (!Existe) throw new InvalidOperationException("No existe el llavero (gestion.llave).");
        var arch = JsonSerializer.Deserialize<Archivo>(File.ReadAllText(Ruta), Json);
        return arch ?? throw new InvalidOperationException("El llavero (gestion.llave) está dañado.");
    }

    private static void Guardar(Archivo arch)
        => File.WriteAllText(Ruta, JsonSerializer.Serialize(arch, Json));

    // Modelo serializado a JSON. Todo en base64; nada sensible en claro.
    private sealed class Archivo
    {
        public int Version { get; set; } = 2;
        public string Kdf { get; set; } = "PBKDF2-SHA256";
        public int Iteraciones { get; set; } = Llavero.Iteraciones;
        public List<Usuario> Usuarios { get; set; } = new();
        public Sobre? Clave { get; set; }          // solo sobrevive para migrar llaveros viejos
        public List<Sobre> Codigos { get; set; } = new();
        public Verificador? Emergencia { get; set; }
        public bool EmergenciaBorraCopias { get; set; }
    }

    private sealed class Usuario
    {
        public string Nombre { get; set; } = "";
        public bool EsAdmin { get; set; }
        public Sobre Sobre { get; set; } = new();
    }

    private sealed class Sobre
    {
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Cifrado { get; set; } = "";
        public bool Usado { get; set; }
    }

    // Solo reconoce una contraseña; no guarda ninguna llave que se pueda desenvolver.
    private sealed class Verificador
    {
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
    }
}
