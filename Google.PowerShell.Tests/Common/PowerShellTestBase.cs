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

using Google.Apis.Compute.v1;
using Google.PowerShell.Common;
using NUnit.Framework;
using System;
using System.Collections.ObjectModel;
using System.IO;
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
        private static readonly string s_fakeConfigJson = $@"{{
            'configuration': {{
                'active_configuration': 'testing',
                'properties': {{
                    'compute': {{
                        'region': '{FakeRegionName}',
                        'zone': '{FakeZoneName}'
                    }},
                    'core': {{
                        'account': 'testing@google.com',
                        'disable_usage_reporting': 'False',
                        'project': '{FakeProjectId}'
                    }}
                }}
            }},
            'credential': {{
                'access_token': 'fake-token',
                'token_expiry': '2012-12-12T12:12:12Z'
            }},
            'sentinels': {{
                'config_sentinel': 'sentinel.sentinel'
            }}
        }}";

        //protected readonly RunspaceConfiguration Config = RunspaceConfiguration.Create();
        protected readonly InitialSessionState Config = InitialSessionState.Create();
        protected Pipeline Pipeline;
        protected System.Management.Automation.PowerShell PowerShellInstance;

        [SetUp]
        public void BeforeEach()
        {
            ActiveUserConfig.ActiveConfig = new ActiveUserConfig(s_fakeConfigJson);
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
            Collection<object> errorRecords = PowerShellInstance.Runspace.CreatePipeline().Error.ReadToEnd();
            Assert.AreEqual(errorRecords.Count, 1);
            var errorRecord = (errorRecords[0] as PSObject)?.BaseObject as ErrorRecord;
            Assert.IsNotNull(errorRecord);
            Assert.AreEqual(errorRecord.CategoryInfo.Category, recordCategory);
        }
    }
}