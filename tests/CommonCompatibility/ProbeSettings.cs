using System;
using System.Collections.Generic;
using System.Globalization;
using FSO.Common;

namespace FreeSims.Tests
{
    public sealed class ProbeSettings : IniConfig
    {
        private Dictionary<string,string> defaults = new Dictionary<string,string> {
            { "Counter","0" }, { "Marker","synthetic-common-probe" }, { "Enabled","true" }
        };
        public override Dictionary<string,string> DefaultValues { get { return defaults; } set { defaults = value; } }
        public int Counter { get; set; }
        public string Marker { get; set; }
        public bool Enabled { get; set; }
        public ProbeSettings(string path) : base(path) { }

        public void Store(int counter, string marker)
        {
            Counter = counter;
            Marker = marker;
            // IniConfig.Save's existing contract serializes DefaultValues.
            defaults["Counter"] = counter.ToString(CultureInfo.InvariantCulture);
            defaults["Marker"] = marker;
            defaults["Enabled"] = "true";
            Save();
        }
    }
}
