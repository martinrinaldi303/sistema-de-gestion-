# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Retro Green Gestión es una app de escritorio **C# / .NET 9 / WinForms** para la gestión
económica de una peluquería, con la base **cifrada (SQLite + SQLCipher)**. El código, los
comentarios y la UI están en español (cultura `es-AR`).

## Comandos

> Es una app **WinForms: sólo compila y corre en Windows**.

```bash
dotnet build RetroGreenGestion.sln            # compilar todo (app + tests)
dotnet run --project RetroGreenGestion.csproj # ejecutar la app (login + base en Documentos\Retro Green)
dotnet test Tests/RetroGreenGestion.Tests.csproj   # correr la suite xUnit
```

Un solo test (filtro por nombre):

```bash
dotnet test Tests/RetroGreenGestion.Tests.csproj --filter "FullyQualifiedName~DuplicadosYErroresTests.NoManda_DosVeces_ElMismoDia"
```

Publicar el ejecutable autocontenido (un `.exe` portable, sin instalar .NET):

```bash
dotnet publish RetroGreenGestion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## Arquitectura

### Capa de datos: estática y global
`Datos/Bd.cs` y `Datos/Repo.cs` son **clases estáticas**. La identidad de la sesión
(`Bd.Usuario`, `Bd.EsAdmin`) la fija el login (`Program.cs` → `Ui/FrmLogin.cs`) **antes**
de abrir `FrmPrincipal`; el resto del código la lee de ahí. No hay inyección de dependencias.
`Bd.Conectar()` abre una conexión nueva por operación (se abren y cierran).

### Cifrado (lo más delicado — `Bd` + `Datos/Llavero.cs`)
La base **no** se cifra con la contraseña del usuario, sino con una **DEK aleatoria**. Esa
DEK se guarda "envuelta" (AES-GCM + PBKDF2, 210k vueltas) en `gestion.llave`: una vez por la
contraseña de cada usuario y una por cada código de recuperación. `Bd._clave` mantiene la DEK
en memoria sólo mientras la app corre. Consecuencia de diseño: **cambiar o recuperar la
contraseña NO re-cifra la base** (sólo se reenvuelve la DEK). La contraseña de emergencia hace
borrado criptográfico (borra el llavero → la base queda irrecuperable).

### Creación del esquema vs. migración (⚠️ trampa al agregar tablas)
`Esquema.Sql` corre **sólo cuando la base está vacía** (`Bd.CrearSiHaceFalta`). Una base ya
creada **nunca** vuelve a ejecutarlo. Por eso, cualquier tabla nueva va en un bloque idempotente
(`Esquema.SqlReportes`, con `CREATE TABLE IF NOT EXISTS`) que se aplica en **cada apertura** vía
`Bd.Migrar()`. Si agregás una tabla y la ponés sólo en `Esquema.Sql`, las bases existentes no
la tendrán.

### Invariantes del dominio (se asumen en todo el código)
- **La plata es SIEMPRE `long` en centavos.** Nunca decimales. Convertí con `Bd.ACentavos` /
  `Bd.APesos` / `Bd.Plata`. Las columnas son `*_cent`. Hay un test que verifica que la suma de
  filas cuadra al centavo con el total.
- **Nada se borra: se anula.** Servicios/egresos se marcan `anulado=1`; los catálogos se
  desactivan (`activo=0`). Todas las consultas filtran por eso.
- **El precio se congela** al vender: `LibroDiario` guarda `descripcion` + `precio_cent`
  copiados, no lo que diga hoy el catálogo.
- Período = `yyyy-MM`, día = `yyyy-MM-dd` (`Repo.Periodo` / `Repo.Dia`).

### Resumen mensual: patrón "vivo vs. cerrado"
Los métodos de `Repo` (`Totales`, `PorProfesional`, `PorMedioPago`, `PorCategoria`, `Evolucion`)
calculan desde el **detalle vivo** (`LibroDiario`/`Egresos`); si el mes se cerró y se purgó el
detalle, caen al **resumen congelado** (`ResumenMensual`, `ResumenProfesional`, etc.).
`Repo.GuardarResumen` congela los totales (incluida la **comisión**, fórmula
`SUM(precio_cent)*comision_porcentaje/100`) **antes** de cualquier purga (`FrmCierre` orquesta:
backup → Excel → verificar → guardar resumen → recién ahí borrar).

### Autorización de admin
Acciones destructivas o de configuración se protegen con
`Autorizacion.PedirAdmin(control, texto)` → `Bd.VerificarAdmin` → `Llavero.EsClaveDeAdmin`.
Las pantallas de admin en el nav se muestran sólo si `Bd.EsAdmin`.

### Módulo de reporte diario por email (`Reportes/`)
- `ReporteDiario` — lógica **pura** (HTML + totales), reusa las consultas de `Repo`.
- `ConfigCorreo` — config en la tabla `Config`; la contraseña SMTP se cifra con **DPAPI**
  (atada al usuario de Windows) y recién ese blob va a `Config`.
- `RegistroEnvios` — tabla `EnviosDiarios`: evita duplicados y permite reintentos/catch-up.
- `IEnviadorCorreo` / `SmtpEnviador` — SMTP+TLS con `System.Net.Mail`. La **interfaz es el seam
  de test** (se mockea sin servidor real).
- `ServicioReportes` — orquesta y **nunca propaga excepción** (el cierre económico no depende
  del correo). El disparo automático es un `System.Windows.Forms.Timer` (1 min) + catch-up en
  `FrmPrincipal.Load`. El HTML y el Excel del día **omiten nombres de clientes** (privacidad).

### UI (`Ui/Tema.cs`)
`Tema` es la única fuente de paleta, tipografía y controles (`Boton`, `Caja`, `Combo`, `Campo`,
`Grilla`, `Tarjeta`, `PanelRedondo`, `CabeceraPantalla`, diálogos `Aviso`/`Error`/`Confirmar`).
Pantallas nuevas deben reusar estos helpers. `FrmPrincipal` aloja cada pantalla como form
embebido (`TopLevel=false`, `Dock=Fill`) dentro del panel de navegación.

## Tests
- Proyecto `Tests/RetroGreenGestion.Tests.csproj` (xUnit, net9.0-windows). La app excluye
  `Tests/**` de su compilación (el proyecto de tests vive bajo la carpeta de la app).
- Los tests que tocan la base corren **en serie** (`DisableTestParallelization`, por el estado
  estático de `Bd`) y cada uno usa una base temporal vía `Bd.CarpetaBaseOverride` (campo
  `internal`, expuesto con `InternalsVisibleTo`). `PruebaConBase` llama a `Batteries_V2.Init()`.
- Al escribir código testeable, dejá la lógica de dominio sin UI (como `ReporteDiario`) e inyectá
  los efectos externos por interfaz (como `IEnviadorCorreo`).
