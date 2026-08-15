// Copyright 2024 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Stores the module's own configuration (active account, default project, zone and region) in a JSON
    /// file under the user's configuration directory. This replaces the previous behavior of shelling out to
    /// "gcloud config" for these values, so the module no longer requires the Google Cloud SDK to be installed.
    ///
    /// The location of the configuration directory can be overridden with the
    /// GOOGLE_CLOUD_POWERSHELL_CONFIG environment variable. Otherwise it defaults to
    /// "%APPDATA%\gcloud-powershell" on Windows and "$HOME/.config/gcloud-powershell" elsewhere.
    /// </summary>
    public sealed class GCloudPowerShellConfig
    {
        /// <summary>Well-known setting keys.</summary>
        public const string ProjectKey = "project";
        public const string ZoneKey = "zone";
        public const string RegionKey = "region";
        public const string AccountKey = "account";

        private const string ConfigDirectoryEnvironmentVariable = "GOOGLE_CLOUD_POWERSHELL_CONFIG";
        private const string ConfigDirectoryName = "gcloud-powershell";
        private const string ConfigFileName = "config.json";

        private static readonly Lazy<GCloudPowerShellConfig> s_default =
            new Lazy<GCloudPowerShellConfig>(() => new GCloudPowerShellConfig());

        /// <summary>The shared configuration instance.</summary>
        public static GCloudPowerShellConfig Default => s_default.Value;

        /// <summary>
        /// When set, settings are read from and written to this dictionary instead of disk. This is used by
        /// unit tests to inject fake settings without touching the file system.
        /// </summary>
        internal static IDictionary<string, string> InMemoryOverride { get; set; }

        private readonly object _fileLock = new object();

        /// <summary>The directory in which the module stores its configuration and credentials.</summary>
        public static string ConfigDirectory { get; } = ResolveConfigDirectory();

        private static string ConfigFilePath => Path.Combine(ConfigDirectory, ConfigFileName);

        private static string ResolveConfigDirectory()
        {
            string overrideDir = Environment.GetEnvironmentVariable(ConfigDirectoryEnvironmentVariable);
            if (!string.IsNullOrEmpty(overrideDir))
            {
                return overrideDir;
            }

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string baseDir = isWindows
                ? Environment.GetEnvironmentVariable("APPDATA")
                : Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? string.Empty, ".config");

            return Path.Combine(baseDir ?? ".", ConfigDirectoryName);
        }

        /// <summary>
        /// Returns the value for the given setting, or null if it is not set. Lookups are case-insensitive.
        /// </summary>
        public string GetSetting(string key)
        {
            if (key == null)
            {
                return null;
            }
            key = key.ToLowerInvariant();

            if (InMemoryOverride != null)
            {
                InMemoryOverride.TryGetValue(key, out string overrideValue);
                return string.IsNullOrEmpty(overrideValue) ? null : overrideValue;
            }

            lock (_fileLock)
            {
                JObject config = Load();
                JToken token = config[key];
                if (token == null || token.Type == JTokenType.Null)
                {
                    return null;
                }
                return token.Value<string>();
            }
        }

        /// <summary>
        /// Sets the value for the given setting. Passing a null or empty value removes the setting.
        /// </summary>
        public void SetSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key));
            }
            key = key.ToLowerInvariant();

            if (InMemoryOverride != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    InMemoryOverride.Remove(key);
                }
                else
                {
                    InMemoryOverride[key] = value;
                }
                return;
            }

            lock (_fileLock)
            {
                JObject config = Load();
                if (string.IsNullOrEmpty(value))
                {
                    config.Remove(key);
                }
                else
                {
                    config[key] = value;
                }
                Save(config);
            }
        }

        private static JObject Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    return JObject.Parse(File.ReadAllText(ConfigFilePath));
                }
            }
            catch
            {
                // Treat an unreadable or corrupt config file as empty rather than failing every cmdlet.
            }
            return new JObject();
        }

        private static void Save(JObject config)
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigFilePath, config.ToString());
        }
    }
}
