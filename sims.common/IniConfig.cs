using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace FSO.Common
{
    public abstract class IniConfig
    {
        private readonly string ActivePath;

        // The declared schema and fallback values; loading must not replace defaults.
        public abstract Dictionary<string, string> DefaultValues { get; set; }

        private void SetValue(string key, string value)
        {
            if (!DefaultValues.ContainsKey(key)) return;
            var prop = GetType().GetProperty(key);
            if (prop == null || !prop.CanWrite || prop.GetIndexParameters().Length != 0) return;
            try
            {
                var converted = prop.PropertyType == typeof(string) ? value :
                    Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
                prop.SetValue(this, converted, null);
            }
            catch (FormatException) { }
            catch (OverflowException) { }
            catch (InvalidCastException) { }
        }

        public IniConfig(string path)
        {
            ActivePath = path;
            Load();
        }

        public void Load()
        {
            // Initialize properties on the first run and reset missing/invalid values
            // on subsequent loads, before applying the file's valid overrides.
            foreach (var pair in DefaultValues) SetValue(pair.Key, pair.Value);

            if (!File.Exists(ActivePath))
            {
                Save();
                return;
            }

            foreach (var line in File.ReadAllLines(ActivePath))
            {
                var clean = line.Trim();
                if (clean.Length == 0 || clean[0] == '#' || clean[0] == '[') continue;
                var split = clean.IndexOf('=');
                if (split == -1) continue;
                SetValue(clean.Substring(0, split).Trim(), clean.Substring(split + 1).Trim());
            }
        }

        /// <summary>
        /// Writes current properties from the declared schema. I/O failures propagate
        /// so a host can report that settings were not saved.
        /// </summary>
        public void Save()
        {
            // Resolve values before opening/truncating the file. Do not serialize
            // static singleton or schema properties, or mutate the declared defaults.
            var values = DefaultValues.Select(pair => {
                var prop = GetType().GetProperty(pair.Key);
                var value = prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0
                    ? Convert.ToString(prop.GetValue(this, null), CultureInfo.InvariantCulture)
                    : pair.Value;
                return pair.Key + "=" + value;
            }).ToArray();
            using (var stream = new StreamWriter(File.Open(ActivePath, FileMode.Create, FileAccess.Write)))
            {
                stream.WriteLine("# FreeSO Settings File. Properties are self explanatory.");
                foreach (var value in values) stream.WriteLine(value);
            }
        }
    }
}
