// Copyright 2024 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Auth.OAuth2;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading;

namespace Google.PowerShell.Common
{
    /// <summary>
    /// <para type="synopsis">
    /// Logs in to Google Cloud by running an interactive OAuth 2.0 browser flow.
    /// </para>
    /// <para type="description">
    /// Opens the default web browser so you can sign in and consent, then stores the resulting refresh token
    /// under the module's configuration directory. Once you have connected, all cmdlets in the module use the
    /// stored credential and automatically refresh access tokens as needed. This does not require the Google
    /// Cloud SDK (gcloud) to be installed.
    /// </para>
    /// <para type="description">
    /// If the GOOGLE_APPLICATION_CREDENTIALS environment variable points to a service account key, or the
    /// module is running on a Google Cloud VM, those Application Default Credentials take precedence over the
    /// account connected with this cmdlet.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Connect-GcpAccount</code>
    ///   <para>Opens the browser to sign in and stores the credential for later use.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Connect-GcpAccount -Project "my-project" -Force</code>
    ///   <para>Forces a fresh sign-in and sets the default project to "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Connect-GcpAccount -ServiceAccountKeyFile "C:\keys\service-account.json"</code>
    ///   <para>Authenticates as the service account described by the given JSON key file, instead of using
    ///   the interactive browser flow.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Connect, "GcpAccount", DefaultParameterSetName = ParameterSetNames.Interactive)]
    public class ConnectGcpAccountCmdlet : PSCmdlet
    {
        private class ParameterSetNames
        {
            public const string Interactive = "Interactive";
            public const string ServiceAccount = "ServiceAccount";
        }

        /// <summary>
        /// <para type="description">
        /// Forces a new sign-in even if a stored credential already exists.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = ParameterSetNames.Interactive)]
        public SwitchParameter Force { get; set; }

        /// <summary>
        /// <para type="description">
        /// The path to a service account JSON key file to authenticate with, instead of the interactive
        /// browser flow. This is the programmatic equivalent of "gcloud auth activate-service-account".
        /// </para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = ParameterSetNames.ServiceAccount)]
        [ValidateNotNullOrEmpty]
        public string ServiceAccountKeyFile { get; set; }

        /// <summary>
        /// <para type="description">
        /// Sets the default project to use for cmdlets that do not explicitly specify one.
        /// </para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Project { get; set; }

        protected override void ProcessRecord()
        {
            string account;
            if (ParameterSetName == ParameterSetNames.ServiceAccount)
            {
                string keyFilePath = GetUnresolvedProviderPathFromPSPath(ServiceAccountKeyFile);
                if (!File.Exists(keyFilePath))
                {
                    ThrowTerminatingError(new ErrorRecord(
                        new FileNotFoundException($"Service account key file '{keyFilePath}' was not found.", keyFilePath),
                        "ServiceAccountKeyNotFound", ErrorCategory.ObjectNotFound, ServiceAccountKeyFile));
                    return;
                }
                try
                {
                    account = GoogleCloudCredential.ActivateServiceAccount(keyFilePath);
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        ex, "GcpServiceAccountActivationFailed", ErrorCategory.AuthenticationError, ServiceAccountKeyFile));
                    return;
                }
            }
            else
            {
                Host?.UI?.WriteLine("Opening your browser to sign in to Google Cloud. Complete the sign-in there...");

                UserCredential credential;
                try
                {
                    credential = GoogleCloudCredential.LoginAsync(Force.IsPresent, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ThrowTerminatingError(new ErrorRecord(
                        ex, "GcpLoginFailed", ErrorCategory.AuthenticationError, null));
                    return;
                }

                account = GoogleCloudCredential.GetUserEmailAsync(credential, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            if (!string.IsNullOrEmpty(account))
            {
                GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.AccountKey, account);
            }
            if (!string.IsNullOrEmpty(Project))
            {
                GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.ProjectKey, Project);
            }

            var result = new PSObject();
            result.Properties.Add(new PSNoteProperty("Account", account ?? "(unknown)"));
            result.Properties.Add(new PSNoteProperty(
                "Project", GCloudPowerShellConfig.Default.GetSetting(GCloudPowerShellConfig.ProjectKey)));
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Logs out of Google Cloud by revoking and deleting the stored credential.
    /// </para>
    /// <para type="description">
    /// Revokes the stored refresh token with Google (best effort) and removes it from the module's
    /// configuration directory. After running this cmdlet you must run Connect-GcpAccount again before using
    /// cmdlets that call Google Cloud.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Disconnect-GcpAccount</code>
    ///   <para>Revokes and removes the stored credential.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommunications.Disconnect, "GcpAccount", SupportsShouldProcess = true)]
    public class DisconnectGcpAccountCmdlet : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            string account = GCloudPowerShellConfig.Default.GetSetting(GCloudPowerShellConfig.AccountKey)
                ?? "the active account";
            if (!ShouldProcess(account, "Revoke and remove the stored Google Cloud credential"))
            {
                return;
            }

            GoogleCloudCredential.RevokeAsync(CancellationToken.None).GetAwaiter().GetResult();
            GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.AccountKey, null);
            WriteVerbose("Removed the stored Google Cloud credential.");
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets the Google Cloud account the module is currently logged in as.
    /// </para>
    /// <para type="description">
    /// Returns the active account email, the default project, and whether a stored credential is present.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcpAccount</code>
    ///   <para>Shows the current account and default project.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpAccount")]
    public class GetGcpAccountCmdlet : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            var result = new PSObject();
            result.Properties.Add(new PSNoteProperty(
                "Account", GCloudPowerShellConfig.Default.GetSetting(GCloudPowerShellConfig.AccountKey)));
            result.Properties.Add(new PSNoteProperty(
                "Project", GCloudPowerShellConfig.Default.GetSetting(GCloudPowerShellConfig.ProjectKey)));
            result.Properties.Add(new PSNoteProperty("LoggedIn", GoogleCloudCredential.HasStoredCredential()));
            WriteObject(result);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Sets default configuration values (project, zone, region) for the module.
    /// </para>
    /// <para type="description">
    /// Stores default values used by cmdlets when the corresponding parameter is not supplied. Values are
    /// persisted under the module's configuration directory.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Set-GcpConfig -Project "my-project" -Zone "us-central1-f" -Region "us-central1"</code>
    ///   <para>Sets the default project, zone and region.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Set, "GcpConfig")]
    public class SetGcpConfigCmdlet : PSCmdlet
    {
        /// <summary>
        /// <para type="description">The default project.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Project { get; set; }

        /// <summary>
        /// <para type="description">The default compute zone.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Zone { get; set; }

        /// <summary>
        /// <para type="description">The default compute region.</para>
        /// </summary>
        [Parameter(Mandatory = false)]
        [ValidateNotNullOrEmpty]
        public string Region { get; set; }

        protected override void ProcessRecord()
        {
            if (Project != null)
            {
                GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.ProjectKey, Project);
            }
            if (Zone != null)
            {
                GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.ZoneKey, Zone);
            }
            if (Region != null)
            {
                GCloudPowerShellConfig.Default.SetSetting(GCloudPowerShellConfig.RegionKey, Region);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Gets the module's default configuration values (project, zone, region, account).
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GcpConfig</code>
    ///   <para>Shows the current default project, zone, region and account.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GcpConfig")]
    public class GetGcpConfigCmdlet : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            GCloudPowerShellConfig config = GCloudPowerShellConfig.Default;
            var result = new PSObject();
            result.Properties.Add(new PSNoteProperty("Project", config.GetSetting(GCloudPowerShellConfig.ProjectKey)));
            result.Properties.Add(new PSNoteProperty("Zone", config.GetSetting(GCloudPowerShellConfig.ZoneKey)));
            result.Properties.Add(new PSNoteProperty("Region", config.GetSetting(GCloudPowerShellConfig.RegionKey)));
            result.Properties.Add(new PSNoteProperty("Account", config.GetSetting(GCloudPowerShellConfig.AccountKey)));
            WriteObject(result);
        }
    }
}
