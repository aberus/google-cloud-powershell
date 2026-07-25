// Copyright 2024 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Common
{
    /// <summary>
    /// Shared helpers for tests that would otherwise depend on the Google Cloud SDK (gcloud) being installed
    /// and initialized on the machine running the tests.
    ///
    /// Two strategies are offered:
    ///  - <see cref="SeedFakeActiveConfig"/> injects a fake gcloud config into <see cref="ActiveUserConfig"/> so
    ///    tests that only need configuration values (project/zone/region) resolve them without invoking gcloud.
    ///  - <see cref="RequireGcloud"/> ignores the current test when gcloud is not available, so integration
    ///    tests that genuinely shell out to gcloud are skipped (rather than failed) on machines/CI without it.
    /// </summary>
    public static class TestSupport
    {
        public const string FakeProject = "gcloud-powershell-testing";
        public const string FakeZone = "us-central1-f";
        public const string FakeRegion = "us-central1";

        /// <summary>
        /// A fake "gcloud config config-helper" JSON payload. The token has no expiry so it never reports as
        /// expired, which lets token-reading tests run against it deterministically.
        /// </summary>
        public static readonly string FakeConfigJson = ($@"{{
            'configuration': {{
                'active_configuration': 'testing',
                'properties': {{
                    'compute': {{ 'region': '{FakeRegion}', 'zone': '{FakeZone}' }},
                    'core': {{
                        'account': 'testing@google.com',
                        'disable_usage_reporting': 'False',
                        'project': '{FakeProject}'
                    }}
                }}
            }},
            'credential': {{ 'access_token': 'fake-access-token', 'token_expiry': null }},
            'sentinels': {{ 'config_sentinel': 'sentinel.sentinel' }}
        }}").Replace('\'', '"');

        /// <summary>
        /// Seeds <see cref="ActiveUserConfig"/>'s cache with the fake config so config lookups resolve without
        /// invoking gcloud.
        /// </summary>
        public static void SeedFakeActiveConfig()
        {
            ActiveUserConfig.ActiveConfig = new ActiveUserConfig(FakeConfigJson);
        }

        /// <summary>Clears the cached active config so it does not leak into other test fixtures.</summary>
        public static void ClearActiveConfig()
        {
            ActiveUserConfig.ActiveConfig = null;
        }

        private static bool? s_gcloudAvailable;

        /// <summary>
        /// Returns true if gcloud is installed and initialized (an active config can actually be read). The
        /// result is cached for the lifetime of the test run.
        /// </summary>
        public static bool IsGcloudAvailable()
        {
            if (!s_gcloudAvailable.HasValue)
            {
                try
                {
                    string config = GCloudWrapper.GetActiveConfig().GetAwaiter().GetResult();
                    s_gcloudAvailable = !string.IsNullOrWhiteSpace(config);
                }
                catch
                {
                    // gcloud is not on PATH, or is not initialized/authenticated.
                    s_gcloudAvailable = false;
                }
            }
            return s_gcloudAvailable.Value;
        }

        /// <summary>
        /// Ignores the current test when gcloud is not available, so integration tests that require the Google
        /// Cloud SDK are skipped rather than reported as failures on environments without it.
        /// </summary>
        public static void RequireGcloud()
        {
            if (!IsGcloudAvailable())
            {
                Assert.Ignore(
                    "The Google Cloud SDK (gcloud) is not installed/initialized; skipping test that requires it.");
            }
        }
    }
}
