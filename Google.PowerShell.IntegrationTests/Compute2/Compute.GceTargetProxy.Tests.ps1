. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random
Describe "Get-GceTargetPool"{
    $previousAllCount = (Get-GceTargetProxy).Count
    $previousHttpCount = (Get-GceTargetProxy -Http).Count
    $previousHttpsCount = (Get-GceTargetProxy -Https).Count
    $previousHttpAndHttpsCount = (Get-GceTargetProxy -Http -Https).Count
    $httpProxyName = "http-proxy-$r"
    $httpsProxyName= "https-proxy-$r"

    It "should fail for wrong project" {
        { Get-GceTargetProxy -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant proxy" {
        { Get-GceTargetProxy $httpProxyName -Http } | Should Throw 404
        { Get-GceTargetProxy $httpsProxyName -Https } | Should Throw 404
        { Get-GceTargetProxy $httpProxyName } | Should Throw "Can not find target proxy"
    }

    Context "with data" {
        BeforeAll {
            Add-GceHealthCheck "health-check-$r" | Out-Null
            Add-GceBackendService "backend-$r" -HttpHealthCheck "health-check-$r" | Out-Null
            Add-GceUrlMap "url-map-$r" -DefaultService "backend-$r" | Out-Null
            Add-GceTargetProxy $httpProxyName -UrlMap "url-map-$r" | Out-Null
            # TODO(jimwp): Make this a target-https-proxy by creating a self signed certificate.
            Add-GceTargetProxy $httpsProxyName -UrlMap "url-map-$r" | Out-Null
        }

        It "should get all Proxies" {
            $proxies = Get-GceTargetProxy
            $proxies.Count - $previousAllCount | Should Be 2
        }
        
        It "should get proxy by protocol" {
            $proxy = Get-GceTargetProxy -Http
            $proxy.Count - $previousHttpCount | Should Be 2
            ($proxy | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy }
            $proxy = Get-GceTargetProxy -Https
            $proxy.Count - $previousHttpsCount | Should Be 0
            $proxies = Get-GceTargetProxy -Http -Https
            $proxies.Count - $previousHttpAndHttpsCount | Should Be 2
        }

        It "should get proxies by name" {
            $proxy = Get-GceTargetProxy $httpProxyName
            $proxy.Count | Should Be 1
            ($proxy | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy }
            $proxy.Name | Should Be $httpProxyName
            $proxy = Get-GceTargetProxy $httpsProxyName
            $proxy.Count | Should Be 1
            ($proxy | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetHttpProxy }
            $proxy.Name | Should Be $httpsProxyName
        }

        AfterAll {
            Remove-GceTargetProxy $httpsProxyName -ErrorAction SilentlyContinue
            Remove-GceTargetProxy $httpProxyName -ErrorAction SilentlyContinue
            Remove-GceUrlMap "url-map-$r" -ErrorAction SilentlyContinue
            Remove-GceBackendService "backend-$r" -ErrorAction SilentlyContinue
            Remove-GceHealthCheck "health-check-$r" -Http -ErrorAction SilentlyContinue
        }
    }
}


Reset-GCloudConfig $oldActiveConfig $configName
