// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Provides the module's default settings (project, zone, region and usage reporting).
    ///
    /// Values are read from the module's own configuration store (see <see cref="GCloudPowerShellConfig"/>),
    /// which is populated by Connect-GcpAccount / Set-GcpConfig. The default project additionally falls back to
    /// the Google Compute Engine metadata server when the module runs on a VM. This no longer depends on the
    /// gcloud CLI.
    /// </summary>
    public class CloudSdkSettings
    {
        public class CommonProperties
        {
            public const string Project = "project";
            public const string Zone = "zone";
            public const string Region = "region";
        }

        /// <summary>Setting name that stores whether anonymous usage reporting is disabled.</summary>
        internal const string DisableUsageReportingSetting = "disable_usage_reporting";

        /// <summary>Name of the file containing the anonymous client ID used for telemetry grouping.</summary>
        private const string ClientIDFileName = ".metricsUUID";

        // Prevent instantiation. Should just be a static utility class.
        private CloudSdkSettings() { }

        /// <summary>
        /// Returns the value for the given setting, or null if it is not set.
        /// </summary>
        public static string GetSettingsValue(string settingName)
        {
            string value = GCloudPowerShellConfig.Default.GetSetting(settingName);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            // When running on a Google Cloud VM, fall back to the metadata server for the default project.
            if (string.Equals(settingName, CommonProperties.Project, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string projectId = GCloudMetadataClient.GetProjectId();
                    return string.IsNullOrEmpty(projectId) ? null : projectId;
                }
                catch
                {
                    // Not running on GCE, or the metadata server is unreachable. Fall through to null.
                }
            }

            return null;
        }

        /// <summary>Returns the default project for the module.</summary>
        public static string GetDefaultProject()
        {
            return GetSettingsValue(CommonProperties.Project);
        }

        /// <summary>
        /// Returns whether or not the user has opted into telemetry reporting.
        /// </summary>
        public static bool GetOptIntoUsageReporting()
        {
            string rawValue = GetSettingsValue(DisableUsageReportingSetting);
            bool value = false;
            // If the disable_usage_reporting value is not set, fall back to the default (report usage).
            if (rawValue == null || Boolean.TryParse(rawValue, out value))
            {
                // Invert the value, because the value stores whether it is *disabled*.
                return !value;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Client ID refers to the random UUID generated to group telemetry reporting. The value is persisted
        /// under the module's configuration directory so that it remains stable across sessions.
        /// </summary>
        public static string GetAnonymousClientID()
        {
            try
            {
                string uuidFile = Path.Combine(GCloudPowerShellConfig.ConfigDirectory, ClientIDFileName);
                if (File.Exists(uuidFile))
                {
                    return File.ReadAllText(uuidFile);
                }

                string uuid = Guid.NewGuid().ToString();
                Directory.CreateDirectory(GCloudPowerShellConfig.ConfigDirectory);
                File.WriteAllText(uuidFile, uuid);
                return uuid;
            }
            catch
            {
                // If we cannot persist the ID, still return a value so telemetry can proceed.
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// True if the module is run on Windows. This is a cache
        /// for IsWindows property.
        /// </summary>
        private static bool? s_isWindows;

        /// <summary>True if the module is run on Windows.</summary>
        public static bool IsWindows
        {
            get
            {
                if (!s_isWindows.HasValue)
                {
                    // RuntimeInformation.IsOSPlatform is only available on .NET Core.
#if NET462_OR_GREATER
                    s_isWindows = true;
#else
                    s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
                }

                return s_isWindows.Value;
            }
        }
    }
}
