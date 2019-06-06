using System;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Services;
using Google.Apis.Compute.v1;
using Google.Apis.Compute.v1.Data;
using Google.Apis.CloudResourceManager.v1;
using Google.Apis.CloudResourceManager.v1.Data;
using Google.Apis.Auth.OAuth2;
using System.IO;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace gnet_dump
{
    class Program
    {
        /// <summary>
        /// GCP Network Toplogy Dump - 
        ///
        /// This application will output the network topology project(s) to which the user identified by the accessToken has access.
        /// </summary>
        /// <param name="accessToken">(Required) Access token of the user with access to the projects 
        /// - run "gcloud auth print-access-token" at the console to generate</param>
        /// <param name="projectId">(Optional) The project for which the network topology should be dumped
        /// - if not provided, all projects will be selected</param>
        /// <param name="outputFile">(Optional) The name of the output file
        ///  - will default to network_topology.json in the current working directory</param>
        /// <param name="skipDefault">(Optional) Skip the gcp default networks - false by default</param>
        static void Main(string accessToken = null, string projectId = null, string outputFile = "./network_topology.json",
            bool skipDefault = false)
        {
            if (accessToken == null)
            {
                Console.WriteLine("accessToken is a required parameter!");
                Environment.Exit(-1);
            }

            try
            {
                var outputStream = File.CreateText(outputFile);
                outputStream.Close();
                File.Delete(outputFile);
                var task = new Program().RetrieveProjectNetworkTopologies(accessToken, projectId, outputFile, skipDefault);
                task.Wait();
                var (results, errors) = task.Result;
                Console.WriteLine("Writing network topology to file: {0}", outputFile);
                using (var file = File.CreateText(outputFile))
                {
                    var serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(file, results);
                }

                Console.WriteLine("Network topology dump complete.");

                if (errors != null && errors.Count > 0)
                {
                    Console.Error.WriteLine("Errors occurred during processing: {0}",
                        JsonConvert.SerializeObject(errors, Formatting.Indented));
                };
            }
            catch (IOException cause)
            {
                Console.WriteLine("Failed to write to file {0}. Error: {1}", outputFile, cause.Message);
                Environment.Exit(-1);
            }
            catch (Exception cause)
            {
                if (cause.Message.Contains("Invalid Credentials"))
                {
                    Console.WriteLine("Please check your access token - credentials are invalid. Keep in mind that access tokens will expire.");
                }
                else
                {
                    Console.WriteLine(cause.Message);
                }
                Environment.Exit(-99);
            }

        }

        private BaseClientService.Initializer createInitializer(string accessToken)
        {
            return new BaseClientService.Initializer
            {
                ApplicationName = "Project Network Topology Dump",
                HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken)
            };
        }

        private async Task<(IList<ProjectNetworkModel>, IList<string[]>)> RetrieveProjectNetworkTopologies(
            string accessToken = null, string projectId = null, string outputFile = "network_topology.json", bool skipDefault = false)
        {

            var result = new List<ProjectNetworkModel>();
            var errors = new List<string[]>();
            var service = new CloudResourceManagerService(createInitializer(accessToken));

            IList<Google.Apis.CloudResourceManager.v1.Data.Project> projects =
                new List<Google.Apis.CloudResourceManager.v1.Data.Project>();

            if (projectId != null)
            {
                Console.WriteLine("Generating network topology for Project: {0}", projectId);
                var retrievedProject = await service.Projects.Get(projectId).ExecuteAsync();
                if (retrievedProject == null)
                {
                    Console.WriteLine("Could not retrieve Project: {0}", projectId);
                    return (result, errors);
                }
                projects.Add(retrievedProject);
            }
            else
            {
                Console.WriteLine("Generating network topology for all Projects");
                var retrievedProjects = await service.Projects.List().ExecuteAsync();
                projects = retrievedProjects.Projects;
            }


            foreach (var project in projects)
            {
                try
                {
                    Console.WriteLine("Retrieving network topology for Project: {0}", project.ProjectId);
                    var networksForProject = await RetrieveNetworkTopology(accessToken, project.ProjectId, skipDefault);
                    var model = asModel(project, networksForProject);
                    result.Add(model);
                }
                catch (Exception cause)
                {
                    errors.Add(new string[] { project.ProjectId, cause.Message });
                }
            }

            return (result, errors);
        }

        private ProjectNetworkModel asModel(Google.Apis.CloudResourceManager.v1.Data.Project project,
            IDictionary<Network, IList<Subnetwork>> networks)
        {
            var result = new ProjectNetworkModel()
            {
                Parent = project.Parent,
                ProjectId = project.ProjectId,
                ProjectNumber = project.ProjectNumber,
                Name = project.Name,
                Networks = new List<NetworkModel>()
            };

            foreach (var network in networks)
            {
                var networkModel = new NetworkModel()
                {
                    Name = network.Key.Name,
                    Peerings = network.Key.Peerings,
                    Subnets = new List<SubnetworkModel>()
                };

                foreach (var subnet in network.Value)
                {
                    var subnetModel = new SubnetworkModel()
                    {
                        GatewayAddress = subnet.GatewayAddress,
                        IpCidrRange = subnet.IpCidrRange,
                        Name = subnet.Name,
                        Region = subnet.Region,
                        SecondaryIpRanges = subnet.SecondaryIpRanges
                    };
                    networkModel.Subnets.Add(subnetModel);
                }

                result.Networks.Add(networkModel);

            }

            return result;
        }

        class SubnetworkModel
        {
            [JsonProperty("gatewayAddress")]
            public virtual string GatewayAddress { get; set; }
            [JsonProperty("ipCidrRange")]
            public virtual string IpCidrRange { get; set; }
            [JsonProperty("name")]
            public virtual string Name { get; set; }
            [JsonProperty("region")]
            public virtual string Region { get; set; }
            [JsonProperty("secondaryIpRanges")]
            public virtual IList<SubnetworkSecondaryRange> SecondaryIpRanges { get; set; }
        }

        class NetworkModel
        {

            [JsonProperty("name")]
            public virtual string Name { get; set; }
            [JsonProperty("peerings")]
            public virtual IList<NetworkPeering> Peerings { get; set; }
            [JsonProperty("subnets")]
            public virtual IList<SubnetworkModel> Subnets { get; set; }
        }

        class ProjectNetworkModel
        {
            [JsonProperty("name")]
            public virtual string Name { get; set; }
            [JsonProperty("parent")]
            public virtual ResourceId Parent { get; set; }
            [JsonProperty("projectId")]
            public virtual string ProjectId { get; set; }
            [JsonProperty("projectNumber")]
            public virtual long? ProjectNumber { get; set; }
            [JsonProperty("networks")]
            public virtual IList<NetworkModel> Networks { get; set; }
        }

        private async Task<IDictionary<Network, IList<Subnetwork>>> RetrieveNetworkTopology(
            string accessToken, string projectId, Boolean skipDefault)
        {

            var service = new ComputeService(createInitializer(accessToken));

            var retrievedNetworks = await service.Networks.List(projectId).ExecuteAsync();

            if (retrievedNetworks.Items == null) return null;

            var result = new Dictionary<Network, IList<Subnetwork>>();

            foreach (Network network in retrievedNetworks.Items)
            {
                if (skipDefault && network.Name == "default") continue;
                var subnets = new List<Subnetwork>();
                result.Add(network, subnets);
                foreach (var subnetUri in network.Subnetworks)
                {
                    var (_projectId, region, networkId) = parseSubnetUri(subnetUri);
                    var subnetResult = await service.Subnetworks.Get(projectId, region, networkId).ExecuteAsync();
                    subnets.Add(subnetResult);
                }
            }

            return result;

        }

        private (string projectId, string region, string network) parseSubnetUri(string uriString)
        {
            var uri = new Uri(uriString);
            var parts = uri.PathAndQuery.Split("/");
            return (parts[4], parts[6], parts[8]);
        }

    }

}