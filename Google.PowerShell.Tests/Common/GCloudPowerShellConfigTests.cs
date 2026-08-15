// Copyright 2024 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class GCloudPowerShellConfigTests
    {
        [SetUp]
        public void SetUp()
        {
            GCloudPowerShellConfig.InMemoryOverride = new System.Collections.Generic.Dictionary<string, string>();
        }

        [TearDown]
        public void TearDown()
        {
            GCloudPowerShellConfig.InMemoryOverride = null;
        }

        [Test]
        public void TestSetAndGetSetting()
        {
            GCloudPowerShellConfig config = GCloudPowerShellConfig.Default;
            config.SetSetting(GCloudPowerShellConfig.ProjectKey, "my-project");

            Assert.AreEqual("my-project", config.GetSetting(GCloudPowerShellConfig.ProjectKey));
        }

        [Test]
        public void TestGetMissingSettingReturnsNull()
        {
            Assert.IsNull(GCloudPowerShellConfig.Default.GetSetting(GCloudPowerShellConfig.ZoneKey));
        }

        [Test]
        public void TestLookupIsCaseInsensitive()
        {
            GCloudPowerShellConfig config = GCloudPowerShellConfig.Default;
            config.SetSetting("PROJECT", "case-project");

            Assert.AreEqual("case-project", config.GetSetting("project"));
        }

        [Test]
        public void TestSettingEmptyValueRemovesSetting()
        {
            GCloudPowerShellConfig config = GCloudPowerShellConfig.Default;
            config.SetSetting(GCloudPowerShellConfig.RegionKey, "us-central1");
            config.SetSetting(GCloudPowerShellConfig.RegionKey, null);

            Assert.IsNull(config.GetSetting(GCloudPowerShellConfig.RegionKey));
        }
    }
}
