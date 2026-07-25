. $PSScriptRoot\..\GcloudCmdlets.ps1
Install-GcloudCmdlets

$project, $zone, $oldActiveConfig, $configName = Set-GCloudConfig
$r = Get-Random

Describe "Get-GceTargetPool"{
    $previousAllAcount = (Get-GceTargetPool).Count
    $previousRegionAcount = (Get-GceTargetPool -Region asia-east1).Count
    $poolName1 = "pool1-$r"
    $poolName2 = "pool2-$r"
    It "should fail for wrong project" {
        { Get-GceTargetPool -Project "asdf" } | Should Throw 403
    }

    It "should fail to get non-existant instance" {
        { Get-GceTargetPool $poolName1 } | Should Throw 404
    }

    Context "with data" {
        BeforeAll {
            Add-GceTargetPool $poolName1 -Region us-central1 | Out-Null

            Add-GceTargetPool $poolName2 -Region asia-east1 | Out-Null
        }

        It "should get all rules" {
            $rules = Get-GceTargetPool
            $rules.Count -$previousAllAcount | Should Be 2
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
        }
        
        It "should get region rule" {
            $rules = Get-GceTargetPool -Region asia-east1
            $rules.Count - $previousRegionAcount | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
            $rules.Name | Should Be $poolName2
        }

        It "should get region rule by name" {
            $rules = Get-GceTargetPool $poolName1
            $rules.Count | Should Be 1
            ($rules | Get-Member).TypeName | ForEach-Object { $_ | Should Be Google.Apis.Compute.v1.Data.TargetPool }
            $rules.Name | Should Be $poolName1
        }

        AfterAll {
            Remove-GceTargetPool $poolName1 -Region us-central1 -ErrorAction SilentlyContinue
            Remove-GceTargetPool $poolName2 -Region asia-east1 -ErrorAction SilentlyContinue
        }
    }
}

Describe "Set-GceTargetPool" {
    $instance = Add-GceInstance "instance-$r" -BootDiskImage (Get-GceImage -Family "coreos-stable")
    $poolName = "pool-$r"
    Add-GceTargetPool $poolName -Region us-central1 | Out-Null
    $poolObj = Get-GceTargetPool $poolName
    It "should add instance with object" {
        $pool = $poolObj | Set-GceTargetPool -AddInstance $instance
        $pool.Instances.Count | Should Be 1
        $pool.Instances | Should Be $instance.SelfLink
    }

    It "should remove instance with object" {
        $pool = $poolObj | Set-GceTargetPool -RemoveInstance $instance
        $pool.Instances.Count | Should Be 0
    }
    It "should add instance by name" {
        $pool =Set-GceTargetPool $poolName -AddInstance $instance.SelfLink
        $pool.Instances.Count | Should Be 1
        $pool.Instances | Should Be $instance.SelfLink
    }

    It "should remove instance with object" {
        $pool =Set-GceTargetPool $poolName -RemoveInstance $instance.SelfLink
        $pool.Instances.Count | Should Be 0
    }

    Remove-GceTargetPool $poolName -Region us-central1 -ErrorAction SilentlyContinue
    $instance | Remove-GceInstance
}

Reset-GCloudConfig $oldActiveConfig $configName
