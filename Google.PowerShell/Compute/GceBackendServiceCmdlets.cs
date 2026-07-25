// Copyright 2015-2016 Google Inc. All Rights Reserved.
// Licensed under the Apache License Version 2.0.

using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.PowerShell.Common;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace Google.PowerShell.Compute
{
    /// <summary>
    /// <para type="synopsis">
    /// Gets Google Compute Engine backend services.
    /// </para>
    /// <para type="description">
    /// Lists backend services of a project, or gets a specific one.
    /// </para>
    /// <example>
    /// <code>PS C:\> Get-GceBackendService</code>
    /// <para>This command lists all backend services for the default project.</para>
    /// </example>
    /// <example>
    /// <code>PS C:\> Get-GceBackendService "my-backendservice"</code>
    /// <para>This command gets the backend service named "my-backendservice".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/backendServices#resource-representations)">
    /// [Backend resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "GceBackendService", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(BackendService))]
    public class GetGceBackendServiceCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string ByName = "ByName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the backend services belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the backend service to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.ByName:
                    WriteObject(Service.BackendServices.Get(Project, Name).Execute());
                    break;
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectBackendServices(Project), true);
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<BackendService> GetAllProjectBackendServices(string project)
        {
            BackendServicesResource.ListRequest request = Service.BackendServices.List(project);
            do
            {
                BackendServiceList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (BackendService backendService in response.Items)
                    {
                        yield return backendService;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new Google Compute Engine backend service.
    /// </para>
    /// <para type="description">
    /// Creates a new global backend service in the given project. Backend services define how Cloud Load
    /// Balancing distributes traffic and reference one or more health checks.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceBackendService "my-backend" -HttpHealthCheck "my-health-check"</code>
    ///   <para>Creates a backend service that uses the legacy HTTP health check "my-health-check".</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/backendServices#resource)">
    /// [Backend Service resource definition]
    /// </para>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceBackendService", DefaultParameterSetName = ParameterSetNames.ByValues)]
    [OutputType(typeof(BackendService))]
    public class AddGceBackendServiceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByValues = "ByValues";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the backend service. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the backend service to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">
        /// The names (or self-links) of legacy HTTP health checks the backend service should use.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string[] HttpHealthCheck { get; set; }

        /// <summary>
        /// <para type="description">The protocol the backend service uses. Defaults to HTTP.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        [ValidateSet("HTTP", "HTTPS", "HTTP2", "SSL", "TCP", "UDP")]
        public string Protocol { get; set; } = "HTTP";

        /// <summary>
        /// <para type="description">Human readable description of the backend service.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByValues)]
        public string Description { get; set; }

        /// <summary>
        /// <para type="description">A BackendService object describing the backend service to create.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public BackendService Object { get; set; }

        protected override void ProcessRecord()
        {
            BackendService body;
            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                body = Object;
            }
            else
            {
                body = new BackendService
                {
                    Name = Name,
                    Description = Description,
                    Protocol = Protocol,
                    HealthChecks = HttpHealthCheck?
                        .Select(hc => BuildGlobalResourceUri(Project, "httpHealthChecks", hc)).ToList()
                };
            }

            string name = body.Name;
            Operation operation = Service.BackendServices.Insert(body, Project).Execute();
            AddGlobalOperation(Project, operation,
                () => WriteObject(Service.BackendServices.Get(Project, name).Execute()));
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine backend service.
    /// </para>
    /// <para type="description">
    /// Deletes the named global backend service from the given project.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceBackendService "my-backend"</code>
    ///   <para>Removes the backend service named "my-backend".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceBackendService", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.ByName)]
    public class RemoveGceBackendServiceCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string ByName = "ByName";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the backend service. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the backend service to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByName, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">The BackendService object to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public BackendService Object { get; set; }

        protected override void ProcessRecord()
        {
            string project = ParameterSetName == ParameterSetNames.ByObject
                ? GetProjectNameFromUri(Object.SelfLink) : Project;
            string name = ParameterSetName == ParameterSetNames.ByObject ? Object.Name : Name;

            if (ShouldProcess($"{project}/{name}", "Remove-GceBackendService"))
            {
                Operation operation = Service.BackendServices.Delete(project, name).Execute();
                AddGlobalOperation(project, operation);
            }
        }
    }
}
