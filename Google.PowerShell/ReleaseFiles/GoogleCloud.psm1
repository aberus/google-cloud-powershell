$script:GCloudModule = $ExecutionContext.SessionState.Module
$script:GCloudModulePath = $script:GCloudModule.ModuleBase








Import-Module "$script:GCloudModulePath\Google.PowerShell.dll"

# The module authenticates natively and no longer requires the Google Cloud SDK (gcloud).
# Run 'Connect-GcpAccount' to sign in, or set GOOGLE_APPLICATION_CREDENTIALS to a service account key.
Write-Verbose "Run 'Connect-GcpAccount' to sign in to Google Cloud."

function gs:() {
    <#
    .SYNOPSIS
    Changes the directory to the Google Cloud Storage drive.
    .DESCRIPTION
    This function changes the directory to the Google Cloud Storage drive.
    It can be called before the Google Cloud PowerShell module is imported.
    #>
    cd gs:
}
