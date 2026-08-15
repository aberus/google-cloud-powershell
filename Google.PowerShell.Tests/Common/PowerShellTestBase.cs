// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.PowerShell.Common;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Google.PowerShell.Tests.Common
{
    /// <summary>
    /// Abstract base class for running unit tests on PSCmdlets.
    /// </summary>
    [TestFixture]
    public abstract class PowerShellTestBase
    {
        protected const string FakeRegionName = "fake-region";
        protected const string FakeZoneName = "fake-zone";
        protected const string FakeProjectId = "fake-project";

        //protected readonly RunspaceConfiguration Config = RunspaceConfiguration.Create();
        protected readonly InitialSessionState Config = InitialSessionState.Create();
        protected Pipeline Pipeline;
        protected System.Management.Automation.PowerShell PowerShellInstance;

        [SetUp]
        public void BeforeEach()
        {
            // Seed the module's settings with fake defaults so cmdlets resolve a project/zone/region without
            // touching disk or the network.
            GCloudPowerShellConfig.InMemoryOverride = new Dictionary<string, string>
            {
                { GCloudPowerShellConfig.ProjectKey, FakeProjectId },
                { GCloudPowerShellConfig.ZoneKey, FakeZoneName },
                { GCloudPowerShellConfig.RegionKey, FakeRegionName },
                { GCloudPowerShellConfig.AccountKey, "testing@google.com" },
                { CloudSdkSettings.DisableUsageReportingSetting, "False" },
            };
            //Runspace rs = RunspaceFactory.CreateRunspace(Config);
            //rs.Open();
            //Pipeline = rs.CreatePipeline();
            InitialSessionState state = InitialSessionState.CreateDefault();
            if (Platform.IsWindows)
            {
                state.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Unrestricted;
            }
            
            PowerShellInstance = System.Management.Automation.PowerShell.Create(state/*RunspaceMode.NewRunspace*//*rs*/);
            //PowerShellInstance.AddScript($"Write-Debug \"current directory: {AppDomain.CurrentDomain.BaseDirectory}\"");
            //string rootPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            //string repoToolsPath = Path.Combine(rootPath, "tools");
            //PowerShellInstance.AddScript($"cd {repoToolsPath}\\ModuleMetadata");
            //PowerShellInstance.AddScript($"Import-Module {repoToolsPath}\\ModuleMetadata\\GetModuleMetadata.psm1");
            PowerShellInstance.AddScript($"Import-Module \"{AppDomain.CurrentDomain.BaseDirectory}\\Google.PowerShell.dll\"");
            PowerShellInstance.AddScript("$ErrorActionPreference='Stop'");
            PowerShellInstance.Invoke();
        }

        [TearDown]
        public void AfterEach()
        {
            GCloudPowerShellConfig.InMemoryOverride = null;
            PowerShellInstance.Dispose();
            PowerShellInstance.Runspace.Dispose();
        }

        /// <summary>
        /// Helper function to test that there is an error
        /// in the pipeline with record category recordCategory.
        /// </summary>
        /// <param name="recordCategory">
        /// The expected category of the errorRecord in the pipeline.
        /// </param>
        protected void TestErrorRecord(ErrorCategory recordCategory)
        {
            // Non-terminating errors written by a cmdlet run through PowerShell.Invoke() are collected on the
            // PowerShell instance's Error stream (not on a freshly created pipeline).
            Assert.AreEqual(1, PowerShellInstance.Streams.Error.Count);
            ErrorRecord errorRecord = PowerShellInstance.Streams.Error[0];
            Assert.IsNotNull(errorRecord);
            Assert.AreEqual(recordCategory, errorRecord.CategoryInfo.Category);
        }
    }
}