. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Get-GceUrlMap" {
    $previousAllCount = (Get-GceUrlMap).Count
    $urlMapName1 = "url-map1-$r"
    $urlMapName2 = "url-map2-$r"

    It "should fail for wrong project" {
        { Get-GceUrlMap -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant proxy" {
        { Get-GceUrlMap $urlMapName1 } | Should Throw 404
    }

    Add-GceHealthCheck "health-check-$r" | Out-Null
    Add-GceBackendService "backend-$r" -HttpHealthCheck "health-check-$r" | Out-Null
    Add-GceUrlMap $urlMapName1 -DefaultService "backend-$r" | Out-Null
    Add-GceUrlMap $urlMapName2 -DefaultService "backend-$r" | Out-Null

    It "should get all maps" {
        $maps = Get-GceUrlMap
        $maps.Count - $previousAllCount | Should Be 2
        ($maps | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.UrlMap }
    }

    It "should get url map by name" {
        $map = Get-GceUrlMap $urlMapName1
        $map.Count | Should Be 1
        ($map | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.UrlMap }
    }
    
    Remove-GceUrlMap $urlMapName1 -ErrorAction SilentlyContinue
    Remove-GceUrlMap $urlMapName2 -ErrorAction SilentlyContinue
    Remove-GceBackendService "backend-$r" -ErrorAction SilentlyContinue
    Remove-GceHealthCheck "health-check-$r" -Http -ErrorAction SilentlyContinue
}

Reset-GCloudConfig $oldActiveConfig $configName
