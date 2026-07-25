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
    /// Gets Google Compute Engine forwarding rules.
    /// </para>
    /// <para type="description">
    /// Lists forwarding rules of a project, or gets a specific one.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule</code>
    ///   <para>This command lists all forwarding rules for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule -Region us-central1</code>
    ///   <para>This command lists all forwarding rules in region "us-central1" for the default project.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule "my-forwarding-rule"</code>
    ///   <para>This command gets the forwarding rule named "my-forwarding-rule" in the default project and region.</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule -Project my-project -Global</code>
    ///   <para>This command lists all global forwarding rules for the project named "my-project".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Get-GceForwardingRule "my-forwarding-rule" -Gobal</code>
    ///   <para>This command gets the global forwarding rule named "my-forwarding-rule" in the default project.</para>
    /// </example>
    /// <para type="link" uri="(https://cloud.google.com/compute/docs/reference/latest/forwardingRules#resource)">
    /// [Forwarding Rule resource definition]
    /// </para>
    [Cmdlet(VerbsCommon.Get, "GceForwardingRule", DefaultParameterSetName = ParameterSetNames.OfProject)]
    [OutputType(typeof(ForwardingRule))]
    public class GetGceForwardingRuleCmdlet : GceCmdlet
    {
        private class ParameterSetNames
        {
            public const string OfProject = "OfProject";
            public const string OfRegion = "OfRegion";
            public const string ByLocalName = "ByLocalName";
            public const string ByGlobalName = "ByGlobalName";
        }

        /// <summary>
        /// <para type="description">
        /// The project the forwarding rules belong to. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">
        /// If set, will retrieve only global forwarding rules.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfProject)]
        [Parameter(ParameterSetName = ParameterSetNames.ByGlobalName, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region of the forwaring rule to get. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.OfRegion, Mandatory = true)]
        [Parameter(ParameterSetName = ParameterSetNames.ByLocalName)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name of the forwarding rule to get.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByLocalName, Mandatory = true, Position = 0)]
        [Parameter(ParameterSetName = ParameterSetNames.ByGlobalName, Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSetNames.OfProject:
                    WriteObject(GetAllProjectForwardingRules(Project), true);
                    break;
                case ParameterSetNames.OfRegion:
                    WriteObject(GetRegionForwardingRules(Project, Region), true);
                    break;
                case ParameterSetNames.ByLocalName:
                    WriteObject(Service.ForwardingRules.Get(Project, Region, Name).Execute());
                    break;
                case ParameterSetNames.ByGlobalName:
                    WriteObject(Service.GlobalForwardingRules.Get(Project, Name).Execute());
                    break;
                default:
                    throw UnknownParameterSetException;
            }
        }

        private IEnumerable<ForwardingRule> GetRegionForwardingRules(string project, string region)
        {
            ForwardingRulesResource.ListRequest request = Service.ForwardingRules.List(project, region);
            do
            {
                ForwardingRuleList response = request.Execute();
                if (response.Items != null)
                {
                    foreach (ForwardingRule forwardingRule in response.Items)
                    {
                        yield return forwardingRule;
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!Stopping && request.PageToken != null);
        }


        private IEnumerable<ForwardingRule> GetAllProjectForwardingRules(string project)
        {
            if (Global)
            {
                GlobalForwardingRulesResource.ListRequest request =
                    Service.GlobalForwardingRules.List(project);
                do
                {
                    ForwardingRuleList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (ForwardingRule forwardingRule in response.Items)
                        {
                            yield return forwardingRule;
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);

            }
            else
            {
                ForwardingRulesResource.AggregatedListRequest request =
                    Service.ForwardingRules.AggregatedList(project);
                do
                {
                    ForwardingRuleAggregatedList response = request.Execute();
                    if (response.Items != null)
                    {
                        foreach (KeyValuePair<string, ForwardingRulesScopedList> kvp in response.Items)
                        {
                            if (kvp.Value?.ForwardingRules != null)
                            {
                                foreach (ForwardingRule forwardingRule in kvp.Value.ForwardingRules)
                                {
                                    yield return forwardingRule;
                                }
                            }
                        }
                    }
                    request.PageToken = response.NextPageToken;
                } while (!Stopping && request.PageToken != null);
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Adds a new Google Compute Engine forwarding rule.
    /// </para>
    /// <para type="description">
    /// Creates a global forwarding rule that points at a target HTTP proxy, or a regional forwarding rule that
    /// points at a target pool.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Add-GceForwardingRule "my-rule" -Global -TargetHttpProxy "my-proxy" -PortRange 8080</code>
    ///   <para>Creates a global forwarding rule sending port 8080 traffic to the target HTTP proxy "my-proxy".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Add-GceForwardingRule "my-rule" -Region us-central1 -TargetPool "my-pool"</code>
    ///   <para>Creates a regional forwarding rule sending traffic to the target pool "my-pool".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Add, "GceForwardingRule", DefaultParameterSetName = ParameterSetNames.Region)]
    [OutputType(typeof(ForwardingRule))]
    public class AddGceForwardingRuleCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string Global = "Global";
            public const string Region = "Region";
        }

        /// <summary>
        /// <para type="description">
        /// The project that will own the forwarding rule. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the forwarding rule to create.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">If set, creates a global forwarding rule.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name (or self-link) of the target HTTP proxy a global forwarding rule sends traffic to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true)]
        public string TargetHttpProxy { get; set; }

        /// <summary>
        /// <para type="description">The port range a global forwarding rule listens on, for example "8080".</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global)]
        public string PortRange { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region for a regional forwarding rule. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Region)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">
        /// The name (or self-link) of the target pool a regional forwarding rule sends traffic to.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Region, Mandatory = true)]
        public string TargetPool { get; set; }

        /// <summary>
        /// <para type="description">Human readable description of the forwarding rule.</para>
        /// </summary>
        [Parameter]
        public string Description { get; set; }

        protected override void ProcessRecord()
        {
            if (ParameterSetName == ParameterSetNames.Global)
            {
                ForwardingRule body = new ForwardingRule
                {
                    Name = Name,
                    Description = Description,
                    IPProtocol = "TCP",
                    PortRange = PortRange,
                    Target = BuildGlobalResourceUri(Project, "targetHttpProxies", TargetHttpProxy)
                };
                Operation operation = Service.GlobalForwardingRules.Insert(body, Project).Execute();
                AddGlobalOperation(Project, operation,
                    () => WriteObject(Service.GlobalForwardingRules.Get(Project, Name).Execute()));
            }
            else
            {
                ForwardingRule body = new ForwardingRule
                {
                    Name = Name,
                    Description = Description,
                    IPProtocol = "TCP",
                    Target = BuildRegionResourceUri(Project, Region, "targetPools", TargetPool)
                };
                Operation operation = Service.ForwardingRules.Insert(body, Project, Region).Execute();
                AddRegionOperation(Project, Region, operation,
                    () => WriteObject(Service.ForwardingRules.Get(Project, Region, Name).Execute()));
            }
        }
    }

    /// <summary>
    /// <para type="synopsis">
    /// Removes a Google Compute Engine forwarding rule.
    /// </para>
    /// <para type="description">
    /// Deletes a global forwarding rule, or a regional forwarding rule from the given region.
    /// </para>
    /// <example>
    ///   <code>PS C:\> Remove-GceForwardingRule "my-rule" -Region us-central1</code>
    ///   <para>Removes the regional forwarding rule "my-rule".</para>
    /// </example>
    /// <example>
    ///   <code>PS C:\> Remove-GceForwardingRule "my-rule" -Global</code>
    ///   <para>Removes the global forwarding rule "my-rule".</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Remove, "GceForwardingRule", SupportsShouldProcess = true,
        DefaultParameterSetName = ParameterSetNames.Region)]
    public class RemoveGceForwardingRuleCmdlet : GceConcurrentCmdlet
    {
        private class ParameterSetNames
        {
            public const string Region = "Region";
            public const string Global = "Global";
            public const string ByObject = "ByObject";
        }

        /// <summary>
        /// <para type="description">
        /// The project that owns the forwarding rule. Defaults to the project in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Region)]
        [Parameter(ParameterSetName = ParameterSetNames.Global)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Project)]
        public override string Project { get; set; }

        /// <summary>
        /// <para type="description">The name of the forwarding rule to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Region, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public string Name { get; set; }

        /// <summary>
        /// <para type="description">If set, removes a global forwarding rule.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Global, Mandatory = true)]
        public SwitchParameter Global { get; set; }

        /// <summary>
        /// <para type="description">
        /// The region of the forwarding rule. Defaults to the region in the Cloud SDK config.
        /// </para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.Region)]
        [ConfigPropertyName(CloudSdkSettings.CommonProperties.Region)]
        public string Region { get; set; }

        /// <summary>
        /// <para type="description">The ForwardingRule object to remove.</para>
        /// </summary>
        [Parameter(ParameterSetName = ParameterSetNames.ByObject, Mandatory = true,
            Position = 0, ValueFromPipeline = true)]
        public ForwardingRule Object { get; set; }

        protected override void ProcessRecord()
        {
            string project;
            string name;
            bool global;
            string region;

            if (ParameterSetName == ParameterSetNames.ByObject)
            {
                project = GetProjectNameFromUri(Object.SelfLink);
                name = Object.Name;
                global = string.IsNullOrEmpty(Object.Region);
                region = global ? null : GetRegionNameFromUri(Object.Region);
            }
            else
            {
                project = Project;
                name = Name;
                global = Global;
                region = Region;
            }

            if (global)
            {
                if (ShouldProcess($"{project}/{name}", "Remove-GceForwardingRule -Global"))
                {
                    Operation operation = Service.GlobalForwardingRules.Delete(project, name).Execute();
                    AddGlobalOperation(project, operation);
                }
            }
            else
            {
                if (ShouldProcess($"{project}/{region}/{name}", "Remove-GceForwardingRule"))
                {
                    Operation operation = Service.ForwardingRules.Delete(project, region, name).Execute();
                    AddRegionOperation(project, region, operation);
                }
            }
        }
    }
}
