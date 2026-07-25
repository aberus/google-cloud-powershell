. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random
Describe "Get-GceForwardingRule"{
    $regionRuleName1 = "region-rule1-$r"
    $regionRuleName2 = "region-rule2-$r"
    $globalRuleName = "global-rule-$r"
    $previousAllCount = (Get-GceForwardingRule).Count
    $previousGlobalCount = (Get-GceForwardingRule).Count
    $previousAsiaEastCount = (Get-GceForwardingRule).Count

    It "should fail for wrong project" {
        { Get-GceForwardingRule -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant instance" {
        { Get-GceForwardingRule $regionRuleName1 } | Should Throw 404
        { Get-GceForwardingRule $globalRuleName -Global } | Should Throw 404
    }

    Context "with data" {
        BeforeAll {
            Add-GceHealthCheck "health-check-$r" | Out-Null
            Add-GceBackendService "backend-$r" -HttpHealthCheck "health-check-$r" | Out-Null
            Add-GceUrlMap "url-map-$r" -DefaultService "backend-$r" | Out-Null
            Add-GceTargetProxy "proxy-$r" -UrlMap "url-map-$r" | Out-Null
            Add-GceForwardingRule $globalRuleName -Global -TargetHttpProxy "proxy-$r" -PortRange 8080 | Out-Null

            Add-GceTargetPool "pool-$r" -Region us-central1 | Out-Null
            Add-GceForwardingRule $regionRuleName1 -Region us-central1 -TargetPool "pool-$r" | Out-Null

            Add-GceTargetPool "pool-$r" -Region asia-east1 | Out-Null
            Add-GceForwardingRule $regionRuleName2 -Region asia-east1 -TargetPool "pool-$r" | Out-Null
        }

        It "should get all rules" {
            $rules = Get-GceForwardingRule
            $rules.Count - $previousAllCount | Should Be 3
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
        }

        It "should get global rule" {
            $rules = Get-GceForwardingRule -Global
            $rules.Count - $previousGlobalCount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $globalRuleName
        }

        It "should get region rule" {
            $rules = Get-GceForwardingRule -Region asia-east1
            $rules.Count -$previousAsiaEastCount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $regionRuleName2
        }

        It "should get region rule by name" {
            $rules = Get-GceForwardingRule $regionRuleName1
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $regionRuleName1
        }

        It "should get global rule by name" {
            $rules = Get-GceForwardingRule $globalRuleName -Global
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.ForwardingRule }
            $rules.Name | Should Be $globalRuleName
        }

        AfterAll {
            Remove-GceForwardingRule $regionRuleName1 -Region us-central1 -ErrorAction SilentlyContinue
            Remove-GceTargetPool "pool-$r" -ErrorAction SilentlyContinue

            Remove-GceForwardingRule $regionRuleName2 -Region asia-east1 -ErrorAction SilentlyContinue
            Remove-GceTargetPool "pool-$r" -Region asia-east1 -ErrorAction SilentlyContinue

            Remove-GceForwardingRule $globalRuleName -Global -ErrorAction SilentlyContinue
            Remove-GceTargetProxy "proxy-$r" -ErrorAction SilentlyContinue
            Remove-GceUrlMap "url-map-$r" -ErrorAction SilentlyContinue
            Remove-GceBackendService "backend-$r" -ErrorAction SilentlyContinue
            Remove-GceHealthCheck "health-check-$r" -Http -ErrorAction SilentlyContinue
        }
    }
}

Reset-GCloudConfig $oldActiveConfig $configName