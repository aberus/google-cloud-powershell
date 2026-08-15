// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using System.Collections.Generic;

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class CloudSdkSettingsTests
    {
        [SetUp]
        public void SetUp()
        {
            GCloudPowerShellConfig.InMemoryOverride = new Dictionary<string, string>
            {
                { GCloudPowerShellConfig.ProjectKey, "test-project" },
                { CloudSdkSettings.DisableUsageReportingSetting, "False" },
            };
        }

        [TearDown]
        public void TearDown()
        {
            GCloudPowerShellConfig.InMemoryOverride = null;
        }

        [Test]
        public void TestGetDefaultProject()
        {
            Assert.AreEqual("test-project", CloudSdkSettings.GetDefaultProject());
        }

        [Test]
        public void TestGetOptInSetting()
        {
            // disable_usage_reporting is "False", so reporting is opted in.
            Assert.IsTrue(CloudSdkSettings.GetOptIntoUsageReporting());

            // Just assert this doesn't throw and returns a stable, non-empty client ID.
            string clientId = CloudSdkSettings.GetAnonymousClientID();
            Assert.That(clientId, Is.Not.Null.And.Not.Empty);
        }
    }
}
