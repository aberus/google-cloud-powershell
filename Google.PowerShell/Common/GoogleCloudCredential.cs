// Copyright 2024 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using OAuthTokenResponse = Google.Apis.Auth.OAuth2.Responses.TokenResponse;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// Performs and persists an interactive (browser based) OAuth 2.0 login for a Google user account and
    /// returns a <see cref="UserCredential"/> that transparently refreshes its access token. This lets the
    /// module authenticate the user directly from PowerShell instead of relying on "gcloud auth login".
    ///
    /// The obtained refresh token is stored under the module's configuration directory using the
    /// Google API client library's <see cref="FileDataStore"/>. A subsequent PowerShell session reuses that
    /// refresh token without prompting the user to log in again.
    /// </summary>
    public static class GoogleCloudCredential
    {
        /// <summary>
        /// The user key under which the credential is stored. The module tracks a single active account,
        /// so a constant key is sufficient.
        /// </summary>
        private const string UserId = "default";

        /// <summary>Sub-folder of the config directory that holds the stored OAuth token.</summary>
        private const string CredentialStoreFolder = "credentials";

        /// <summary>
        /// The public "Cloud SDK" OAuth client. This is the same installed-application client that the gcloud
        /// CLI uses, so the consent screen and granted scopes match "gcloud auth login". A custom client can be
        /// supplied through the GOOGLE_CLOUD_POWERSHELL_CLIENT_ID / GOOGLE_CLOUD_POWERSHELL_CLIENT_SECRET
        /// environment variables.
        /// </summary>
        private const string DefaultClientId = "32555940559.apps.googleusercontent.com";
        private const string DefaultClientSecret = "ZmssLNjJy2998hD4CTg2ejr2";

        /// <summary>
        /// The scopes requested during login. cloud-platform covers the services exposed by this module; the
        /// remaining scopes mirror what gcloud requests so the shared client authorizes them.
        /// </summary>
        public static readonly string[] Scopes =
        {
            "openid",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/cloud-platform",
            "https://www.googleapis.com/auth/appengine.admin",
            "https://www.googleapis.com/auth/compute",
            "https://www.googleapis.com/auth/sqlservice.login",
        };

        private static readonly HttpClient s_httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly SemaphoreSlim s_lock = new SemaphoreSlim(1);
        private static UserCredential s_cachedCredential;
        private static GoogleCredential s_serviceAccountCredential;

        /// <summary>
        /// The service account credential activated through Connect-GcpAccount -ServiceAccountKeyFile, or null
        /// if no service account has been activated. When set, it takes precedence over the interactive login
        /// and Application Default Credentials.
        /// </summary>
        public static GoogleCredential ActiveServiceAccountCredential => s_serviceAccountCredential;

        private static string ClientId =>
            Environment.GetEnvironmentVariable("GOOGLE_CLOUD_POWERSHELL_CLIENT_ID") ?? DefaultClientId;

        private static string ClientSecret =>
            Environment.GetEnvironmentVariable("GOOGLE_CLOUD_POWERSHELL_CLIENT_SECRET") ?? DefaultClientSecret;

        /// <summary>The directory where the OAuth token is persisted.</summary>
        public static string CredentialStorePath =>
            Path.Combine(GCloudPowerShellConfig.ConfigDirectory, CredentialStoreFolder);

        private static GoogleAuthorizationCodeFlow CreateFlow()
        {
            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = ClientId, ClientSecret = ClientSecret },
                Scopes = Scopes,
                DataStore = new FileDataStore(CredentialStorePath, fullPath: true),
            });
        }

        /// <summary>
        /// Runs the interactive browser login flow and persists the resulting refresh token. If a valid
        /// credential is already stored it is reused unless <paramref name="force"/> is true.
        /// </summary>
        public static async Task<UserCredential> LoginAsync(bool force, CancellationToken cancellationToken)
        {
            await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                GoogleAuthorizationCodeFlow flow = CreateFlow();
                if (force)
                {
                    await flow.DeleteTokenAsync(UserId, cancellationToken).ConfigureAwait(false);
                }

                // AuthorizationCodeInstalledApp reuses a stored token when present, and otherwise spins up a
                // localhost listener (LocalServerCodeReceiver), opens the browser to Google's consent page and
                // waits for the redirect that carries the authorization code.
                var installedApp = new AuthorizationCodeInstalledApp(flow, new LocalServerCodeReceiver());
                UserCredential credential = await installedApp.AuthorizeAsync(UserId, cancellationToken)
                    .ConfigureAwait(false);

                s_cachedCredential = credential;
                return credential;
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Activates a service account from a JSON key file and makes it the active credential for the module.
        /// This is the programmatic equivalent of "gcloud auth activate-service-account". Returns the service
        /// account's email address (read from the key file), or null if it could not be determined.
        /// </summary>
        public static string ActivateServiceAccount(string keyFilePath)
        {
            GoogleCredential credential;
            using (Stream stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            if (credential.IsCreateScopedRequired)
            {
                credential = credential.CreateScoped(Scopes);
            }

            s_serviceAccountCredential = credential;
            // Drop any cached interactive login so the service account takes over.
            s_cachedCredential = null;

            try
            {
                JObject keyJson = JObject.Parse(File.ReadAllText(keyFilePath));
                return keyJson["client_email"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns the stored user credential, refreshing its access token as needed. This never launches a
        /// browser: if no credential has been stored it throws an <see cref="InvalidOperationException"/>
        /// directing the user to log in.
        /// </summary>
        public static async Task<UserCredential> GetActiveUserCredentialAsync(CancellationToken cancellationToken)
        {
            if (s_cachedCredential != null)
            {
                return s_cachedCredential;
            }

            await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (s_cachedCredential != null)
                {
                    return s_cachedCredential;
                }

                GoogleAuthorizationCodeFlow flow = CreateFlow();
                OAuthTokenResponse token = await flow.LoadTokenAsync(UserId, cancellationToken).ConfigureAwait(false);
                if (token == null || string.IsNullOrEmpty(token.RefreshToken))
                {
                    throw new InvalidOperationException(
                        "You are not logged in to Google Cloud. Run 'Connect-GcpAccount' to authenticate, " +
                        "or set the GOOGLE_APPLICATION_CREDENTIALS environment variable to a service account key file.");
                }

                s_cachedCredential = new UserCredential(flow, UserId, token);
                return s_cachedCredential;
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Returns true if a user credential with a refresh token is currently stored.
        /// </summary>
        public static bool HasStoredCredential()
        {
            try
            {
                GoogleAuthorizationCodeFlow flow = CreateFlow();
                OAuthTokenResponse token = flow.LoadTokenAsync(UserId, CancellationToken.None).GetAwaiter().GetResult();
                return token != null && !string.IsNullOrEmpty(token.RefreshToken);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Revokes the stored refresh token (best effort) and removes it from the credential store.
        /// </summary>
        public static async Task RevokeAsync(CancellationToken cancellationToken)
        {
            await s_lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                GoogleAuthorizationCodeFlow flow = CreateFlow();
                OAuthTokenResponse token = await flow.LoadTokenAsync(UserId, cancellationToken).ConfigureAwait(false);
                if (token != null)
                {
                    try
                    {
                        var credential = new UserCredential(flow, UserId, token);
                        await credential.RevokeTokenAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        // The token may already be invalid/expired; deleting the local copy is what matters.
                    }
                }

                await flow.DeleteTokenAsync(UserId, cancellationToken).ConfigureAwait(false);
                s_cachedCredential = null;
                s_serviceAccountCredential = null;
            }
            finally
            {
                s_lock.Release();
            }
        }

        /// <summary>
        /// Best-effort lookup of the authenticated account's email address using the userinfo endpoint.
        /// Returns null if it cannot be determined.
        /// </summary>
        public static async Task<string> GetUserEmailAsync(UserCredential credential, CancellationToken cancellationToken)
        {
            try
            {
                string accessToken = await credential
                    .GetAccessTokenForRequestAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                using (var request = new HttpRequestMessage(
                    HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    HttpResponseMessage response = await s_httpClient
                        .SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JObject.Parse(body)["email"]?.Value<string>();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
