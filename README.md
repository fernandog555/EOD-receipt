# Corte de Caja

Small Windows desktop app for the end-of-day cash count (corte de caja). Agents enter the money in, the breakdown, the cheques they cashed, sign on screen, and print on a thermal printer or a normal sheet. Every corte is saved locally.

## Use

1. Fill in **Fecha** and **Agente**.
2. **ENTRADAS**: W (plus the X / initials), + envíos, and any other income (with what it was).
3. **DESGLOSE**: cash, máquina, devolución (person's name + agency), compra (what was bought), otras cosas. Cheques go in the list on the right, one per line with the cheque number; their total fills in automatically.
4. The bottom line shows **✔ CUADRA** when both totals match, or how much is missing / extra.
5. Sign in the box, then **Guardar**, **Imprimir Térmica** or **Imprimir Hoja**. Printing saves first.

**Historial** lists every saved corte (who did it, totals, number and total of cheques). From there you can reopen, reprint or delete one.

**Configuración** sets the business name printed on the receipt, the thermal and sheet printers, and the roll width (80 or 58 mm). Printing opens a preview window where you can pick the printer; the last printer used for each type is remembered. Check "Imprimir directo" to skip the preview and print straight to that printer.

Data is saved as one JSON file per corte in `Documents\CorteDeCaja\cortes`.

## Build

Requires the .NET SDK (any recent version). The app targets .NET Framework 4.8, which is built into Windows 10/11, so the exe runs without installing anything.

```
cd CorteDeCaja
dotnet build -c Release
```

The app is `CorteDeCaja\bin\Release\net48\CorteDeCaja.exe` (copy it together with `CorteDeCaja.exe.config`).

`CorteDeCaja.exe --demo <folder>` writes sample PNGs of the window and both receipt layouts, for checking the layout without printing.
