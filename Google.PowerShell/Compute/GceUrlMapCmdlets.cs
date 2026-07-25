// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <para type="synopsis">
    /// Gets Google Compute Engine url maps.
    /// </para>
    /// <para type="description">
    /// Lists url maps of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceUrlMap</code>
    ///   <para>This command lists all url maps for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceUrlMap "my-url-map"</code>
    ///   <para>This command gets the url map named "my-url-map"</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/urlMaps#resource)">
    /// [Url Map resource definition]
    /// </para>
    [Cmdlet(VerbsCommon.Get, "GceUrlMap", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(UrlMap))]
    public class GceGceUrlMapCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the url maps belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the url map to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectUrlMaps(Project), true);
                    break;
                case ParameterSetNames.ByName:
                    WriteObject(Service.UrlMaps.Get(Project, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<UrlMap> GetAllProjectUrlMaps(string project)
        {
            UrlMapsResource.ListRequest request = Service.UrlMaps.List(project);
            do
            {
                UrlMapList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (UrlMap urlMap in response.Items)
                    {
                        yield return urlMap;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new Google Compute Engine URL map.
    /// </para>
    /// <para type="description">
    /// Creates a new URL map that routes requests to a default backend service.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceUrlMap "my-url-map" -DefaultService "my-backend"</code>
    ///   <para>Creates a URL map whose default service is the backend service "my-backend".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceUrlMap", DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(UrlMap))]
    public class AddGceUrlMapCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the URL map. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the URL map to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name (or self-link) of the backend service requests are routed to by default.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true)]
        public string DefaultService { get; set; }

        /// <summary>
        /// <para type="description">Human readable description of the URL map.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">A UrlMap object describing the URL map to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public UrlMap Object { get; set; }

        protected override void ProcessRecord()
        {
            UrlMap body;
            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                body = Object;
            }
            else
            {
                body = new UrlMap
                {
                    Name = Name,
                    Description = Description,
                    DefaultService = BuildGlobalResourceUri(Project, "backendServices", DefaultService)
                };
            }

            string name = body.Name;
            Operation operation = Service.UrlMaps.Insert(body, Project).Execute();
            AddGlobalOperation(Project, operation,
                () => WriteObject(Service.UrlMaps.Get(Project, name).Execute()));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine URL map.
    /// </para>
    /// <para type="description">
    /// Deletes the named URL map from the given project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceUrlMap "my-url-map"</code>
    ///   <para>Removes the URL map named "my-url-map".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceUrlMap", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceUrlMapCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the URL map. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the URL map to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">The UrlMap object to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public UrlMap Object { get; set; }

        protected override void ProcessRecord()
        {
            string project = ParameterSetName == ParameterSetNames.ByObject
                ? GetProjectNameFromUri(Object.SelfLink) : Project;
            string name = ParameterSetName == ParameterSetNames.ByObject ? Object.Name : Name;

            if (ShouldProcess($"{project}/{name}", "Remove-GceUrlMap"))
            {
                Operation operation = Service.UrlMaps.Delete(project, name).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }
}
