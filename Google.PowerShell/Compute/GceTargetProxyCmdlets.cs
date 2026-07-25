// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <para type="synopsis">
    /// Gets Google Compute Engine target proxies.
    /// </para>
    /// <para type="description">
    /// Lists target proxies of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetProxy</code>
    ///   <para>This command lists all target proxies for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetProxy -Region us-central1</code>
    ///   <para>This command lists all target proxies in region "us-central1" for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceTargetProxy "my-target-proxy"</code>
    ///   <para>This command gets the target proxy named "my-target-proxy" in the default project and zone</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/targetHttpProxies#resource)">
    /// [Target Proxy resource definition]
    /// </para>
    [Cmdlet(VerbsCommon.Get, "GceTargetProxy", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(TargetHttpProxy), typeof(TargetHttpsProxy))]
    public class GetGceTargetProxyCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the target proxies belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the target proxy to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get target http proxies. If neither this nor Https is set, will get both http and
        /// https proxies.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Http { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will get target https proxies. If neither this nor Https is set, will get both http and
        /// https proxies.
        /// </para>
        /// </summary>
        [Parameter]
        public SwitchParameter Https { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectTargetProxies(Project), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(GetTargetProxyByName(Project, Name), true);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<object> GetTargetProxyByName(string project, string name)
        {
            var exceptions = new List<Exception>();
            var proxies = new List<object>();
            if (Http || !Https)
            {
                try
                {
                    proxies.Add(Service.TargetHttpProxies.Get(project, name).Execute());
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            if (Https || !Http)
            {
                try
                {
                    proxies.Add(Service.TargetHttpsProxies.Get(project, name).Execute());
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            if (proxies.Count > 0)
            {
                return proxies;
            }
            else
            {
                if (exceptions.Count == 1)
                {
                    throw exceptions[0];
                }
                else
                {
                    throw new AggregateException($"Can not find target proxy {project}/{name}", exceptions);
                }
            }
        }

        private IEnumerable<object> GetAllProjectTargetProxies(string project)
        {
            if (Http || !Https)
            {
                TargetHttpProxiesResource.ListRequest request = Service.TargetHttpProxies.List(project);
                do
                {
                    TargetHttpProxyList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (TargetHttpProxy targetProxy in response.Items)
                        {
                            yield return targetProxy;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }
            if (Https || !Http)
            {
                TargetHttpsProxiesResource.ListRequest request = Service.TargetHttpsProxies.List(project);
                do
                {
                    TargetHttpsProxyList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (TargetHttpsProxy targetProxy in response.Items)
                        {
                            yield return targetProxy;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new Google Compute Engine target HTTP proxy.
    /// </para>
    /// <para type="description">
    /// Creates a new target HTTP proxy that references a URL map. Target proxies are used by global forwarding
    /// rules to route requests through a URL map.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceTargetProxy "my-proxy" -UrlMap "my-url-map"</code>
    ///   <para>Creates a target HTTP proxy that uses the URL map "my-url-map".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceTargetProxy", DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(TargetHttpProxy))]
    public class AddGceTargetProxyCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the target proxy. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the target proxy to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">The name (or self-link) of the URL map the proxy routes requests through.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true)]
        public string UrlMap { get; set; }

        /// <summary>
        /// <para type="description">Human readable description of the target proxy.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">A TargetHttpProxy object describing the target proxy to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public TargetHttpProxy Object { get; set; }

        protected override void ProcessRecord()
        {
            TargetHttpProxy body;
            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                body = Object;
            }
            else
            {
                body = new TargetHttpProxy
                {
                    Name = Name,
                    Description = Description,
                    UrlMap = BuildGlobalResourceUri(Project, "urlMaps", UrlMap)
                };
            }

            string name = body.Name;
            Operation operation = Service.TargetHttpProxies.Insert(body, Project).Execute();
            AddGlobalOperation(Project, operation,
                () => WriteObject(Service.TargetHttpProxies.Get(Project, name).Execute()));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine target HTTP proxy.
    /// </para>
    /// <para type="description">
    /// Deletes the named target HTTP proxy from the given project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceTargetProxy "my-proxy"</code>
    ///   <para>Removes the target HTTP proxy named "my-proxy".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceTargetProxy", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceTargetProxyCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the target proxy. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the target proxy to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">The TargetHttpProxy object to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public TargetHttpProxy Object { get; set; }

        protected override void ProcessRecord()
        {
            string project = ParameterSetName == ParameterSetNames.ByObject
                ? GetProjectNameFromUri(Object.SelfLink) : Project;
            string name = ParameterSetName == ParameterSetNames.ByObject ? Object.Name : Name;

            if (ShouldProcess($"{project}/{name}", "Remove-GceTargetProxy"))
            {
                Operation operation = Service.TargetHttpProxies.Delete(project, name).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }
}
