/*
Copyright 2015-2016 Google Inc. All Rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/
using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// OAuth 2.0 credential for accessing protected resources using an access token.
    /// This delegates to the user credential obtained through <see cref="GoogleCloudCredential"/> (an
    /// interactive browser login persisted on disk), so no dependency on the gcloud CLI is required. The
    /// underlying <see cref="UserCredential"/> transparently refreshes the access token from the stored
    /// refresh token.
    /// </summary>
    public class AuthenticateWithSdkCredentialsExecutor : ICredential, IHttpExecuteInterceptor, IHttpUnsuccessfulResponseHandler
    {
        #region IHttpExecuteInterceptor

        /// <summary>
        /// Adds the access token to the outgoing request, obtaining (and refreshing) it from the stored user
        /// credential as needed.
        /// </summary>
        public async Task InterceptAsync(HttpRequestMessage request, CancellationToken taskCancellationToken)
        {
            UserCredential credential = await GoogleCloudCredential.GetActiveUserCredentialAsync(taskCancellationToken).ConfigureAwait(false);
            await credential.InterceptAsync(request, taskCancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region IHttpUnsuccessfulResponseHandler

        /// <summary>
        /// Handles an abnormal response when sending a HTTP request. On a 401 it refreshes the token via the
        /// underlying user credential and signals the request should be retried.
        /// </summary>
        public async Task<bool> HandleResponseAsync(HandleUnsuccessfulResponseArgs args)
        {
            if (args.Response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return false;
            }

            UserCredential credential = await GoogleCloudCredential.GetActiveUserCredentialAsync(args.CancellationToken).ConfigureAwait(false);
            return await credential.HandleResponseAsync(args).ConfigureAwait(false);
        }

        #endregion

        #region IConfigurableHttpClientInitializer

        public void Initialize(ConfigurableHttpClient httpClient)
        {
            httpClient.MessageHandler.AddExecuteInterceptor(this);
            httpClient.MessageHandler.AddUnsuccessfulResponseHandler(this);
        }

        #endregion

        #region ITokenAccess implementation

        public virtual async Task<string> GetAccessTokenForRequestAsync(string authUri = null, CancellationToken cancellationToken = default)
        {
            UserCredential credential =
                await GoogleCloudCredential.GetActiveUserCredentialAsync(cancellationToken).ConfigureAwait(false);
            return await credential.GetAccessTokenForRequestAsync(authUri, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        /// <summary>
        /// Refreshes the access token using the stored user credential.
        /// </summary>
        public async Task<bool> RefreshTokenAsync(CancellationToken taskCancellationToken)
        {
            UserCredential credential = await GoogleCloudCredential.GetActiveUserCredentialAsync(taskCancellationToken).ConfigureAwait(false);
            return await credential.RefreshTokenAsync(taskCancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Revokes the stored user credential.
        /// </summary>
        /// <param name="taskCancellationToken">Cancellation token to cancel an operation.</param>
        /// <returns><c>true</c> if the token was revoked successfully.</returns>
        public async Task<bool> RevokeTokenAsync(CancellationToken taskCancellationToken)
        {
            await GoogleCloudCredential.RevokeAsync(taskCancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
