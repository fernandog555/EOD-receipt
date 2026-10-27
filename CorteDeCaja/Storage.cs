using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CorteDeCaja
{
    /// <summary>Saves each corte as a JSON file under Documents\CorteDeCaja\cortes.</summary>
    public static class Storage
    {
        public static readonly string Root =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CorteDeCaja");
        public static string CortesDir => Path.Combine(Root, "cortes");
        static string SettingsPath => Path.Combine(Root, "config.json");

        public static void SaveCorte(Corte c)
        {
            Directory.CreateDirectory(CortesDir);
            var path = Path.Combine(CortesDir, c.Fecha + "_" + c.Id + ".json");
            // If the date was changed, drop the file saved under the old date.
            foreach (var old in Directory.GetFiles(CortesDir, "*_" + c.Id + ".json"))
                if (!string.Equals(old, path, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
            Write(path, c);
        }

        public static void DeleteCorte(Corte c)
        {
            if (!Directory.Exists(CortesDir)) return;
            foreach (var f in Directory.GetFiles(CortesDir, "*_" + c.Id + ".json")) File.Delete(f);
        }

        public static List<Corte> LoadCortes()
        {
            var list = new List<Corte>();
            if (!Directory.Exists(CortesDir)) return list;
            foreach (var f in Directory.GetFiles(CortesDir, "*.json"))
            {
                try { list.Add(Read<Corte>(f)); }
                catch { /* skip unreadable files rather than block the history */ }
            }
            return list.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.GuardadoEn).ToList();
        }

        public static AppSettings LoadSettings()
        {
            try { if (File.Exists(SettingsPath)) return Read<AppSettings>(SettingsPath); }
            catch { }
            return new AppSettings();
        }

        public static void SaveSettings(AppSettings s)
        {
            Directory.CreateDirectory(Root);
            Write(SettingsPath, s);
        }

        static T Read<T>(string path)
        {
            using (var fs = File.OpenRead(path))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(fs);
        }

        // Write to a temp file first so a crash mid-save never leaves a half-written corte.
        static void Write<T>(string path, T obj)
        {
            var tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
            using (var w = JsonReaderWriterFactory.CreateJsonWriter(fs, Encoding.UTF8, false, true))
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(w, obj);
                w.Flush();
            }
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
