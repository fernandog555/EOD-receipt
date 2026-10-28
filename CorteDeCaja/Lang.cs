using System;
using System.Globalization;

namespace CorteDeCaja
{
    /// <summary>
    /// Screen language (Spanish or English). Every on-screen string goes through T(es, en).
    /// Printed receipts stay in Spanish and never read this.
    /// </summary>
    public static class Lang
    {
        static readonly CultureInfo EsMx = new CultureInfo("es-MX");
        static readonly CultureInfo EnUs = new CultureInfo("en-US");

        public static bool En { get; private set; }

        /// <summary>Raised after the language changes, so open windows can re-label themselves.</summary>
        public static event Action Changed;

        public static CultureInfo Culture => En ? EnUs : EsMx;

        public static string T(string es, string en) => En ? en : es;

        public static void Init(string code) => En = code == "en";

        public static void Set(bool en)
        {
            if (En == en) return;
            En = en;
            Changed?.Invoke();
        }

        public static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], Culture) + s.Substring(1);

        /// <summary>"Miércoles, 28 de octubre de 2026" / "Wednesday, October 28, 2026".</summary>
        public static string LongDate(DateTime d) => Cap(d.ToString(T("dddd, d 'de' MMMM 'de' yyyy", "dddd, MMMM d, yyyy"), Culture));

        /// <summary>Day name for lists: "Miércoles" / "Wednesday".</summary>
        public static string DayName(DateTime d) => Cap(Culture.DateTimeFormat.GetDayName(d.DayOfWeek));

        /// <summary>"Cuadra" / "Faltan $10.00" / "Sobran $10.00", or the English equivalent.</summary>
        public static string Status(Corte c) =>
            c.Diferencia == 0 ? T("Cuadra", "Balanced") :
            c.Diferencia < 0 ? T("Faltan ", "Short ") + Fmt.Money(-c.Diferencia) :
            T("Sobran ", "Over ") + Fmt.Money(c.Diferencia);

        public static string Checks(int n) => n + (n == 1 ? T(" cheque", " check") : T(" cheques", " checks"));
    }
}
