using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace asdIRC.Helpers
{
    /// <summary>
    /// Simple Configurtion Manager
    /// </summary>
    public class pConfigManager : IDisposable
    {
        private readonly Dictionary<string, string> entriesRaw = new Dictionary<string, string>();
        private readonly Dictionary<string, object> entriesParsed = new Dictionary<string, object>();
        private string configFilename;
        private bool dirty;
        public bool WriteOnChange { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="pConfigManager"/> class with a custom filename.
        /// </summary>
        /// <param name="filename">The filename.</param>
        public pConfigManager(string filename, bool forceCreate = false)
        {
            LoadConfig(filename, forceCreate);
        }

        ~pConfigManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (dirty) SaveConfig();
        }

        public T GetValue<T>(string key, T defaultValue)
        {

            if (entriesParsed.TryGetValue(key, out object obj))
                return (T)obj;

            if (!entriesRaw.TryGetValue(key, out string raw))
            {
                //If we don't have a value, we should set the default to be written back to the config file.
                SetValue(key, defaultValue);
                return defaultValue;
            }

            string ty = typeof(T).Name;

            switch (ty)
            {
                case @"Boolean":
                    obj = raw[0] == '1';
                    break;
                case @"Int32":
                    obj = Int32.Parse(raw);
                    break;
                case @"Int64":
                    obj = Int64.Parse(raw);
                    break;
                case @"String":
                    obj = raw;
                    break;
            }

            entriesParsed[key] = obj;
            entriesRaw[key] = raw;

            return (T)obj;
        }

        public void SetValue<T>(string key, T value)
        {
            switch (typeof(T).Name)
            {
                default:
                    entriesRaw[key] = value?.ToString();
                    break;
                case @"Boolean":
                    entriesRaw[key] = value.ToString() == @"True" ? @"1" : @"0";
                    break;
            }

            entriesParsed[key] = value;

            dirty = true;

            if (WriteOnChange) SaveConfig();

        }

        public void LoadConfig(string configName, bool forceCreate = false)
        {
            entriesRaw.Clear();
            entriesParsed.Clear();

            if (ReadConfigFile(configName) || forceCreate)
                configFilename = configName;
        }

        private bool ReadConfigFile(string filename)
        {
            if (!File.Exists(filename)) return false;

            try
            {
                using (StreamReader r = File.OpenText(filename))
                    while (!r.EndOfStream)
                    {
                        string line = r.ReadLine();
                        if (line.Length < 2) continue;
                        if (line.StartsWith(@"#")) continue;

                        int equals = line.IndexOf('=');
                        string key = line.Remove(equals).Trim();
                        string value = line.Substring(equals + 1).Trim();
                        entriesRaw[key] = value;
                    }
            }
            catch { }

            return true;
        }

        public void SaveConfig()
        {
            if (configFilename == null)
                return;
            WriteConfigFile(configFilename);

            dirty = false;
        }

        private bool WriteConfigFile(string filename)
        {
            try
            {
                StringBuilder w = new StringBuilder();

                foreach (KeyValuePair<string, string> p in entriesRaw)
                    w.AppendLine(p.Key + @" = " + p.Value);

                File.WriteAllText(filename, w.ToString());
            }
            catch
            {
                return false;
            }
            return true;
        }
    }
}
