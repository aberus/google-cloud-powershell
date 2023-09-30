using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    public static class GCloudMetadataClient
    {
        /// <summary>
        /// The Metadata flavor header name.
        /// </summary>
        internal const string MetadataFlavor = "Metadata-Flavor";

        /// <summary>
        /// The Metadata header response indicating Google.
        /// </summary>
        internal const string GoogleMetadataHeader = "Google";

        internal static HttpClient Client = new HttpClient();

        // Constant strings to avoid duplication below.
        // IP address instead of name to avoid DNS resolution
        private const string DefaultMetadataAddress = "169.254.169.254";

        internal const string DefaultMetadataServerUrl = "http://" + DefaultMetadataAddress;
        private const string ComputeDefaultProjectIdSuffix = "/computeMetadata/v1/project/project-id";

        /// <summary>
        /// The effective Compute Engine default service account email URL.
        /// This takes account of the GCE_METADATA_HOST environment variable.
        /// </summary>
        internal static string EffectiveComputeDefaultProjectIdUrl =>
            GetEffectiveMetadataUrl(ComputeDefaultProjectIdSuffix, DefaultMetadataServerUrl + ComputeDefaultProjectIdSuffix);

        /// <summary>
        /// The effective Compute Engine metadata token server URL (with no path).
        /// This takes account of the GCE_METADATA_HOST environment variable.
        /// </summary>
        internal static string EffectiveMetadataServerUrl => GetEffectiveMetadataUrl(null, DefaultMetadataServerUrl);

        private static string GetEffectiveMetadataUrl(string suffix, string defaultValue)
        {
            string env = Environment.GetEnvironmentVariable("GCE_METADATA_HOST");
            return string.IsNullOrEmpty(env) ? defaultValue : "http://" + env + suffix;
        }

        public static string GetProjectId()
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, EffectiveComputeDefaultProjectIdUrl);
            httpRequest.Headers.Add(MetadataFlavor, GoogleMetadataHeader);

            var response = Client.SendAsync(httpRequest).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string projectId = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return projectId;
        }
    }
}
