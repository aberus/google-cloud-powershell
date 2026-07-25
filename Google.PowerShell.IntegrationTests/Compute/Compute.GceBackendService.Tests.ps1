. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig

Describe "Get-GceBackendService" {
    $r = Get-Random
    $serviceName1 = "backend-service1-$r"
    $serviceName2 = "backend-service2-$r"

    It "should fail for wrong project" {
        { Get-GceBackendService -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existent proxy" {
        { Get-GceBackendService $serviceName1 } | Should Throw 404
    }

    Add-GceHealthCheck "health-check-$r" | Out-Null
    Add-GceBackendService $serviceName1 -HttpHealthCheck "health-check-$r" | Out-Null
    Add-GceBackendService $serviceName2 -HttpHealthCheck "health-check-$r" | Out-Null

    It "should get all maps" {
        $maps = Get-GceBackendService
        $maps.Count -ge 2 | Should Be $true
        ($maps | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.BackendService }
    }

    It "should get url map by name" {
        $map = Get-GceBackendService $serviceName1
        $map.Count | Should Be 1
        ($map | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.BackendService }
        $map.Name | Should Be $serviceName1
    }
    
    Remove-GceBackendService $serviceName1 -ErrorAction SilentlyContinue
    Remove-GceBackendService $serviceName2 -ErrorAction SilentlyContinue
    Remove-GceHealthCheck "health-check-$r" -Http -ErrorAction SilentlyContinue
}

Reset-GCloudConfig $oldActiveConfig $configName
