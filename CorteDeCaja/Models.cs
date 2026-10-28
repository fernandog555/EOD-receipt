using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;

namespace CorteDeCaja
{
    [DataContract]
    public class Cheque
    {
        [DataMember] public string Numero = "";
        [DataMember] public decimal Monto;
    }

    /// <summary>One end-of-day cash count, signed by the agent who did it.</summary>
    [DataContract]
    public class Corte
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Fecha = DateTime.Today.ToString("yyyy-MM-dd");
        [DataMember] public string GuardadoEn;
        [DataMember] public string Agente = "";

        // Entradas (money in)
        [DataMember] public decimal W;
        [DataMember] public string WNota = "";
        [DataMember] public decimal Envios;
        [DataMember] public decimal OtroIngreso;
        [DataMember] public string OtroIngresoNota = "";

        // Desglose (how the money is handed over)
        [DataMember] public decimal Cash;
        [DataMember] public List<Cheque> Cheques = new List<Cheque>();
        [DataMember] public decimal Maquina;
        [DataMember] public decimal Devolucion;
        [DataMember] public string DevolucionPersona = "";
        [DataMember] public string DevolucionAgencia = "";
        [DataMember] public decimal Compra;
        [DataMember] public string CompraNota = "";
        [DataMember] public decimal Otros;
        [DataMember] public string OtrosNota = "";

        [DataMember] public string FirmaPng;

        // The JSON serializer skips field initializers, so fill in anything missing from older files.
        [OnDeserialized]
        void FixNulls(StreamingContext _)
        {
            Cheques = Cheques ?? new List<Cheque>();
            Agente = Agente ?? "";
            WNota = WNota ?? "";
            OtroIngresoNota = OtroIngresoNota ?? "";
            DevolucionPersona = DevolucionPersona ?? "";
            DevolucionAgencia = DevolucionAgencia ?? "";
            CompraNota = CompraNota ?? "";
            OtrosNota = OtrosNota ?? "";
            foreach (var c in Cheques) c.Numero = c.Numero ?? "";
        }

        public DateTime FechaDate => DateTime.ParseExact(Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        public decimal TotalEntradas => W + Envios + OtroIngreso;
        public decimal TotalCheques => Cheques.Sum(c => c.Monto);
        public decimal TotalDesglose => Cash + TotalCheques + Maquina + Devolucion + Compra + Otros;
        public decimal Diferencia => TotalDesglose - TotalEntradas;

        public string Estado =>
            Diferencia == 0 ? "CUADRA" :
            Diferencia < 0 ? "FALTAN " + Fmt.Money(-Diferencia) :
            "SOBRAN " + Fmt.Money(Diferencia);
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember] public string NombreNegocio = "";
        [DataMember] public string ImpresoraTermica = "";
        [DataMember] public string ImpresoraHoja = "";
        [DataMember] public int AnchoTermicaMm = 80;
        // Off by default: every print shows the preview window first.
        [DataMember] public bool ImprimirDirecto;
        // Screen language: "es" or "en". Printed receipts are always in Spanish.
        [DataMember] public string Idioma = "es";

        [OnDeserialized]
        void FixNulls(StreamingContext _)
        {
            NombreNegocio = NombreNegocio ?? "";
            ImpresoraTermica = ImpresoraTermica ?? "";
            ImpresoraHoja = ImpresoraHoja ?? "";
            Idioma = Idioma == "en" ? "en" : "es";
            if (AnchoTermicaMm != 58) AnchoTermicaMm = 80;
        }
    }

    public static class Fmt
    {
        static readonly CultureInfo Es = new CultureInfo("es-MX");

        public static string Money(decimal d) => d.ToString("$#,##0.00;-$#,##0.00", CultureInfo.InvariantCulture);
        public static string Date(DateTime d) => d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        public static string Day(DateTime d) => Es.DateTimeFormat.GetDayName(d.DayOfWeek).ToUpper(Es);

        public static bool TryParseMoney(string s, out decimal value)
        {
            s = (s ?? "").Replace("$", "").Replace(",", "").Trim();
            if (s.Length == 0) { value = 0; return true; }
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }
    }
}
