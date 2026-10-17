# Retro Green — Gestión económica

Programa de escritorio para la peluquería. **No maneja stock**: sólo lo económico.
Libro diario de servicios, egresos, resumen mensual y exportación a Excel para el contador.

Mismos colores y tipografía que [peluqueriagreen.com](https://peluqueriagreen.com/).

---

## Cómo abrirlo

1. Abrí `RetroGreenGestion.sln` con Visual Studio.
2. F5.

La primera vez te pide **crear la clave del negocio**. Esa clave encripta la base entera.

> **Anotala.** Si se pierde, los datos no se recuperan: no hay forma de abrir el archivo sin ella.
> Eso es justamente lo que lo hace seguro.

Para probarlo sin Visual Studio:

```bash
dotnet run --project "RetroGreenGestion.csproj"
```

---

## Las pantallas

| Pantalla | Para qué |
|---|---|
| **Servicios del día** | El libro diario: Pedro hizo tal servicio por tal precio, pagado así. |
| **Egresos** | Los gastos, con las cuatro categorías de la planilla. |
| **Resumen mensual** | El mismo panel que tiene hoy en Google Sheets, sin `#DIV/0!`. |
| **Catálogos** | Servicios, profesionales, medios de pago y categorías. Todo editable. |
| **Cierre de mes** | Genera el Excel para el contador y, opcionalmente, purga el detalle. |
| **Cambiar clave** | Vuelve a encriptar la base con una clave nueva. |

---

## Lo que resuelve de lo que pidió el dueño

**Encriptación real.** El archivo de datos está cifrado con SQLCipher. Si alguien se
lleva la notebook o copia el `.db` a un pendrive, no lo puede abrir con nada. Probado:
el archivo ni siquiera empieza con la firma `SQLite format 3`.

**Borrado del detalle, pero sin perder el histórico.** El interruptor está en Cierre de
mes y arranca **apagado**. Cuando se enciende, la secuencia es siempre:

1. copia de seguridad de la base entera
2. se genera el Excel
3. se verifica que el archivo exista y tenga contenido real
4. **se graban los totales del mes** — esto sobrevive a la purga
5. recién ahí se borra el detalle
6. queda el registro de quién exportó, cuándo y cuántas filas

Si cualquier paso falla, **no se borra nada**. Y aunque se purgue, el Resumen mensual
sigue mostrando el mes y el histórico sigue comparando contra los anteriores.

**Medios de pago.** Campo obligatorio en servicios y gastos, con su propio bloque en
el resumen. No es lo mismo plata en el cajón que plata en el banco.

**Combos y descuentos.** Son un servicio más en el catálogo. "Corte + Barba" con su
precio. Nada de lógica especial.

**Comisiones.** Hay un `% de comisión` por profesional en Catálogos. Se calcula al
mostrar, y al cerrar el mes se congela el monto en el resumen — así, si mañana cambia
la regla, los meses ya cerrados no se mueven.

**Batería.** No hay ningún servicio de Windows corriendo (por eso SQLite y no SQL
Server Express), no hay timers de refresco, y las conexiones se abren y se cierran.
Con el programa cerrado, el consumo es cero.

---

## Decisiones que conviene que sepas

**La plata se guarda en centavos, como número entero.** Nunca como decimal con coma.
Si no, los redondeos se acumulan y el total te da `1.589.999,97` en vez de `1.590.000`.
Hay una prueba que verifica que la suma de las filas coincide al centavo con el total.

**Nada se borra, se anula.** Los servicios y gastos mal cargados se marcan `anulado`
y dejan de sumar, pero queda el rastro. Los catálogos se desactivan, no se eliminan:
así los registros viejos siguen teniendo sentido.

**El precio se congela.** El libro diario guarda lo que se cobró, no lo que dice el
catálogo. Si en diciembre sube el corte, octubre sigue diciendo lo de octubre.

**Una sola clave, no un PIN por persona.** Como los tres tienen los mismos permisos,
un PIN por cabeza no agrega seguridad y suma fricción. La clave abre la base; después
se elige quién está usando el programa, y eso queda registrado en cada carga.

---

## Dónde quedan los archivos

```
Documentos\Retro Green\
    gestion.db                  la base, encriptada
    Copias de seguridad\        las últimas 15, automáticas
    Exportaciones\              los Excel para el contador
    Reportes diarios\           los Excel de cada cierre del día (se adjuntan al email)
```

La carpeta de exportación se puede cambiar desde Cierre de mes. **Conviene apuntarla
al pendrive o a Drive**: el Excel no está encriptado, y si queda en la notebook es el
eslabón más fácil de llevarse.

---

## Reporte diario por email

El programa puede mandarle al dueño un **reporte del día** por correo: un resumen en el
cuerpo del mail (ingresos, egresos, neto, rendimiento por profesional, medios de pago y
gastos) y un **Excel con el detalle del día** adjunto. Es un módulo aparte: **si el correo
falla, no afecta en nada el cierre económico ni el resto del programa**.

### Cómo configurarlo

En el menú, **Reporte diario** (sólo lo ve el administrador). Ahí se cargan:

- **Correo de destino** y **hora de envío**.
- Los datos del servidor **SMTP**: host, puerto, TLS, usuario y remitente, más la contraseña.

**Cambiar cualquiera de estos datos pide la contraseña de administrador.** Con Gmail: host
`smtp.gmail.com`, puerto `587`, TLS activado, y como contraseña una
[**contraseña de aplicación**](https://support.google.com/accounts/answer/185833) (no la
del correo; requiere verificación en dos pasos). Outlook/Office365: `smtp.office365.com`,
puerto `587`, TLS.

### Cómo se protege la contraseña

No se guarda en claro. Se cifra con **DPAPI de Windows** (atada al usuario que la configuró)
y recién ese texto cifrado se guarda en la base, que además ya está encriptada con SQLCipher.
Consecuencia: la contraseña **sólo se puede leer desde el mismo Windows y el mismo usuario**
donde se cargó. Si se cambia de notebook o de cuenta de Windows, hay que volver a escribirla.

### Cuándo se envía (y por qué no gasta batería)

- **A la hora configurada**, mientras el programa esté abierto. Es el único timer del
  programa (revisa una vez por minuto); el trabajo de red va fuera del hilo de la pantalla,
  así que no la traba. Con el programa cerrado, sigue sin consumir nada.
- Si a esa hora el programa estaba cerrado, **al abrirlo pone al día** los días que quedaron
  pendientes (hasta 3 días para atrás, desde que se activó).
- Nunca manda **dos veces el mismo día**. Si un envío falla (ej. sin internet), queda marcado
  y **se reintenta** en la próxima vuelta o al reabrir (hasta 5 intentos por día).

### Cómo probarlo

En la misma pantalla, botón **"Enviar el reporte de hoy ahora"**: arma y manda el reporte del
día en el momento (sirve también como "cierre del día" manual). Abajo se ve el estado del día
(enviado / error con el detalle / todavía no).

### Qué NO se manda

Nunca sale la base de datos, el llavero ni las claves. El resumen y el Excel del día van
**sin nombres de clientes ni notas**: son datos personales que no hacen falta en un reporte
económico.

### Si algo falla

- *"Falta la contraseña SMTP (o no se puede leer)"*: se configuró en otra máquina/usuario de
  Windows. Volvé a escribirla y guardá desde este Windows.
- El estado del día dice **error**: ahí figura el motivo (host mal escrito, sin internet,
  contraseña rechazada…). Corregí y usá "Enviar el reporte de hoy ahora", o esperá a que
  reintente solo.
- Para **desactivar** el envío automático: destildá "Enviar el reporte diario automáticamente"
  y guardá (pide la contraseña de administrador).

---

## Para instalarlo en la notebook de la peluquería

Generá una versión que no necesite instalar nada:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Queda un `.exe` que se copia y anda. Sin .NET, sin Office, sin drivers de base de datos.
Es la razón por la que elegí SQLite sobre Access: con Access habría que instalar el
*Access Database Engine* en esa máquina y pelearse con 32 vs 64 bits.

---

## Lo que falta definir con el dueño

- El **porcentaje de comisión** real (el campo ya está, falta el número).
- Si la **ficha de clientes** de la planilla también va al programa.
- El **cierre diario** quedó cubierto en parte por el reporte diario por email (resumen +
  Excel del día). Falta definir si además quiere un arqueo de caja en pantalla.
- **Propinas**: hoy no se registran.
- En qué **formato quiere el Excel el contador** — preguntarle a él directamente.

---

## Pila

.NET 9 · WinForms · SQLite + SQLCipher (`Microsoft.Data.Sqlite.Core` +
`SQLitePCLRaw.bundle_e_sqlcipher`) · ClosedXML para el Excel · `System.Net.Mail` para el
correo (SMTP+TLS, sin dependencias extra) · DPAPI (`System.Security.Cryptography.ProtectedData`)
para la contraseña del correo · pruebas con xUnit en `Tests\`.

ClosedXML y no Excel Interop: Interop necesita Office instalado, es lento y deja
procesos de Excel colgados en memoria.
