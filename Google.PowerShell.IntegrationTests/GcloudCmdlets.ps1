# TODO(chrsmith): Provide a "initialize unit tests" method, which also sets common properties like $project.

# Install the GCP cmdlets module into the current PowerShell session.
function Install-GcloudCmdlets() {
    # Find the latest Google.PowerShell.dll that shares a folder with GoogleCloud.psd1.
    $dll = Get-ChildItem $PSScriptRoot\.. -Recurse -Include Google.PowerShell.dll |
        where {Test-Path (Join-Path $_.PSParentPath GoogleCloud.psd1)} |
        sort LastWriteTime -Descending |
        select -First 1

    # Set environment variable to disable Google Analytics metric reporting.
    # Shouldn't persist beyond the current PowerShell session.
    # Important: We have to set this first before calling Import-Module
    # as Import-Module will try to initialize the GCS PowerShell Provider,
    # which will actually creates an AnalyticsReport that will report
    # the data to production instead of debugging server.
    $env:DISABLE_POWERSHELL_ANALYTICS = "TRUE"

    # Copy all the assemblies file into fullclr folder since that is what
    # the psd1 file is expecting.
    $fullClrFolder = Join-Path $dll.PSParentPath fullclr
    if (-not (Test-Path $fullClrFolder)) {
        mkdir $fullClrFolder
        Copy-Item "$($dll.PSParentPath)\*" $fullClrFolder -Include *.pdb, *.xml, *.dll
    }

    # Import the GoogleCloud.psd1 in the folder of the latest dll.
    Join-Path $dll.PSParentPath GoogleCloud.psd1 | Import-Module
}

# Creates a GCS bucket owned associated with the project, deleting any existing
# buckets with that name and all of their contents.
function Create-TestBucket($project, $bucket) {
    Remove-GcsBucket -Name $bucket -Force -ErrorAction SilentlyContinue
    New-GcsBucket -Name $bucket -Project $project | Out-Null
}

# Copies a 0-byte file from the local machine to Google Cloud Storage.
function Add-TestFile($bucket, $objName) {
    $filename = [System.IO.Path]::GetTempFileName()
    New-GcsObject -Bucket $bucket -ObjectName $objName -File $filename | Out-Null
    Remove-Item -Force $filename
}

# Points the module at the testing project/zone/region and returns the previous configuration so it can
# be restored afterwards. The return shape (project, zone, oldConfig, reserved) is kept for compatibility
# with the callers; the fourth value is unused and only preserved so existing test scaffolding keeps working.
function Set-GCloudConfig(){
    $project = "gcloud-powershell-testing"
    $zone = "us-central1-f"

    # Capture the current defaults so Reset-GCloudConfig can put them back when the tests finish.
    $oldConfig = Get-GcpConfig

    Set-GcpConfig -Project $project -Zone $zone -Region "us-central1"

    return $project, $zone, $oldConfig, $null
}

# Restores the module configuration captured by Set-GCloudConfig. The second parameter is unused and kept
# only for compatibility with existing callers.
function Reset-GCloudConfig($oldConfig, $configName) {
    if ($null -eq $oldConfig) {
        return
    }

    # Set-GcpConfig rejects null/empty values, so only restore settings that were previously present.
    $params = @{}
    if ($oldConfig.Project) { $params["Project"] = $oldConfig.Project }
    if ($oldConfig.Zone)    { $params["Zone"]    = $oldConfig.Zone }
    if ($oldConfig.Region)  { $params["Region"]  = $oldConfig.Region }
    if ($params.Count -gt 0) {
        Set-GcpConfig @params
    }
}

# Installs Cloud SDK non-interactively.
function Install-CloudSdk() {
    $cloudSdkUri = "https://dl.google.com/dl/cloudsdk/channels/rapid/google-cloud-sdk.zip"
    Invoke-WebRequest -Uri $cloudSdkUri -OutFile "$env:APPDATA\gcloudsdk.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    # This will extract it to a folder $env:APPDATA\google-cloud-sdk.
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$env:APPDATA\gcloudsdk.zip", "$env:APPDATA")
    
    $installationPath = "$env:LOCALAPPDATA\Google\Cloud SDK"

    md $installationPath
    Copy-Item "$env:APPDATA\google-cloud-sdk" $installationPath -Recurse -Force

    # Set this to true to disable prompts.
    $env:CLOUDSDK_CORE_DISABLE_PROMPTS = $true
    & "$installationPath\google-cloud-sdk\install.bat" --quiet 2>$null

    $cloudBinPath = "$installationPath\google-cloud-sdk\bin"
    $envPath = [System.Environment]::GetEnvironmentVariable("Path")
    if (-not $envPath.Contains($cloudBinPath)) {
        [System.Environment]::SetEnvironmentVariable("Path", "$envPath;$cloudBinPath")
    }
}

# Runs pester test in folder $env:test_folder and throws error if any test fails.
function Start-PesterTest() {
    $testResult = Invoke-Pester "$PSScriptRoot\$env:test_folder" -PassThru
    if ($testResult.FailedCount -gt 0) {
        throw "$($testResult.FailedCount) tests failed."
    }
}
