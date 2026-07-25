// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using NUnit.Framework;
using Google.PowerShell.Common;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class CloudSdkSettingsTests
    {
        // Seed a fake active config so settings resolve without invoking gcloud.
        [SetUp]
        public void SetUp()
        {
            TestSupport.SeedFakeActiveConfig();
        }

        [TearDown]
        public void TearDown()
        {
            TestSupport.ClearActiveConfig();
        }

        [Test]
        public void TestGetDefaultProject()
        {
            Assert.AreEqual(TestSupport.FakeProject, CloudSdkSettings.GetDefaultProject());
        }

        [Test]
        public void TestGetOptInSetting()
        {
            // Just assert these don't throw. GetAnonymousClientID may return a new UUID each time, so we only
            // check that a value is produced.
            CloudSdkSettings.GetOptIntoUsageReporting();
            CloudSdkSettings.GetAnonymousClientID();
        }
    }
}
