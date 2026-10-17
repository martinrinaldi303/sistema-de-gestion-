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
```

La carpeta de exportación se puede cambiar desde Cierre de mes. **Conviene apuntarla
al pendrive o a Drive**: el Excel no está encriptado, y si queda en la notebook es el
eslabón más fácil de llevarse.

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
- Si quiere **cierre de caja diario** además del mensual.
- **Propinas**: hoy no se registran.
- En qué **formato quiere el Excel el contador** — preguntarle a él directamente.

---

## Pila

.NET 9 · WinForms · SQLite + SQLCipher (`Microsoft.Data.Sqlite.Core` +
`SQLitePCLRaw.bundle_e_sqlcipher`) · ClosedXML para el Excel.

ClosedXML y no Excel Interop: Interop necesita Office instalado, es lento y deja
procesos de Excel colgados en memoria.
