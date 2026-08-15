// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.PowerShell.Common;
using NUnit.Framework;
using System;

namespace Google.PowerShell.Tests.Common
{
    [TestFixture]
    public class TestAuthenticateWithSdkCredentialsExecutor
    {
        /// <summary>
        /// When there is no stored user credential, requesting an access token should fail with an
        /// actionable error rather than shelling out to the gcloud CLI.
        /// </summary>
        [Test]
        public void TestGetAccessTokenWhenNotLoggedInThrows()
        {
            if (GoogleCloudCredential.HasStoredCredential())
            {
                Assert.Ignore("A stored user credential is present; skipping the not-logged-in test.");
            }

            var executor = new AuthenticateWithSdkCredentialsExecutor();
            AggregateException ex = Assert.Throws<AggregateException>(
                () => { string _ = executor.GetAccessTokenForRequestAsync().Result; });
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException,
                "Expected an InvalidOperationException instructing the user to log in.");
        }
    }
}
