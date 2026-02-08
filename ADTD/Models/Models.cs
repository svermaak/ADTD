using System;
using System.Collections.Generic;
using System.Linq;
using AD = System.DirectoryServices.ActiveDirectory;

namespace ADTD.Models
{
    public class Topology
    {
        public Graph GenerateGraph(AD.Forest ActiveDirectoryForest)
        {
            Graph graph = new Graph();
            List<Server> servers = new List<Server>();

            // Adding Forest
            Graph.Node forestNode = graph.AddNode(":Forest", ActiveDirectoryForest, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", ActiveDirectoryForest.Name) });

            // Adding Sites
            foreach (AD.ActiveDirectorySite activeDirectorySite in ActiveDirectoryForest.Sites)
            {
                // Adding Site
                Graph.Node siteNode = graph.AddNode(":Site", activeDirectorySite, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectorySite.Name) });
                graph.AddEdge("Linked To", siteNode, forestNode, null);

                //// Adding Intra Site Replication Schedule
                //Graph.Node intraSiteReplicationScheduleNode = graph.AddNode(":Intra Site Replication Schedule", activeDirectorySite.IntraSiteReplicationSchedule, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectorySite.Name) });
                //graph.AddEdge("Intra Site Replication Schedule", siteNode, intraSiteReplicationScheduleNode, null);

                // Adding Site Links
                foreach (AD.ActiveDirectorySiteLink activeDirectorySiteLink in activeDirectorySite.SiteLinks)
                {
                    Graph.Node siteLinkNode = graph.AddNode(":SiteLink", activeDirectorySiteLink, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectorySiteLink.Name) });
                    graph.AddEdge("Site Link", siteNode, siteLinkNode, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Cost", activeDirectorySiteLink.Cost) });
                }

                // Adding Subnets
                foreach (AD.ActiveDirectorySubnet activeDirectorySubnet in activeDirectorySite.Subnets)
                {
                    Graph.Node subnetNode = graph.AddNode(":Subnet", activeDirectorySubnet, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectorySubnet.Name) });
                    graph.AddEdge("Linked To", subnetNode, siteNode, null);
                }

                //Adding Bridgehead Servers
                foreach (AD.DirectoryServer activeDirectoryServer in activeDirectorySite.BridgeheadServers)
                {
                    dynamic serverObject = activeDirectoryServer;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Bridgehead Server", serverNode, siteNode, null);
                }

                //Adding the Inter Site Topology Generator Server
                {
                    if (activeDirectorySite.InterSiteTopologyGenerator != null)
                    {
                        dynamic serverObject = activeDirectorySite.InterSiteTopologyGenerator;
                        Server server = AddServer(servers, serverObject);
                        Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectorySite.InterSiteTopologyGenerator.Name) });

                        graph.AddEdge("Inter Site Topology Generator", siteNode, serverNode, null);
                    }
                }

                //Adding Directory Servers
                foreach (AD.DirectoryServer activeDirectoryServer in activeDirectorySite.Servers)
                {
                    dynamic serverObject = activeDirectoryServer;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Directory Server", serverNode, siteNode, null);


                    // Adding Inbound Connections
                    foreach (AD.ReplicationConnection replicationConnection in activeDirectoryServer.InboundConnections)
                    {
                        Graph.Node replicationConnectionNode = graph.AddNode(":Replication Connection", replicationConnection, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", replicationConnection.Name) });
                        graph.AddEdge("Inbound Connection", replicationConnectionNode, serverNode, null);
                    }

                    // Adding Outbound Connections
                    foreach (AD.ReplicationConnection replicationConnection in activeDirectoryServer.OutboundConnections)
                    {
                        Graph.Node replicationConnectionNode = graph.AddNode(":Replication Connection", replicationConnection, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", replicationConnection.Name) });
                        graph.AddEdge("Outbound Connection", serverNode, replicationConnectionNode, null);
                    }

                    //Adding Partitions
                    foreach (string activeDirectoryPartition in activeDirectoryServer.Partitions)
                    {
                        Graph.Node partitionNode = graph.AddNode(":Partition", activeDirectoryPartition, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectoryPartition) });
                        graph.AddEdge("Hosts", serverNode, partitionNode, null);
                    }
                }
            }

            // Adding Domains
            {
                Graph.Node domainNode = graph.AddNode(":Domain", ActiveDirectoryForest.RootDomain, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", ActiveDirectoryForest.RootDomain.Name) });
                graph.AddEdge("Root Domain", forestNode, domainNode, null);
            }
            foreach (AD.Domain activeDirectoryDomain in ActiveDirectoryForest.Domains)
            {
                Graph.Node domainNode = graph.AddNode(":Domain", activeDirectoryDomain, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", activeDirectoryDomain.Name) });
                graph.AddEdge("Part of Forest", domainNode, forestNode, null);

                // Adding Domain Controller
                foreach (AD.DomainController domainController in activeDirectoryDomain.DomainControllers)
                {
                    dynamic serverObject = domainController;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Domain Controller", domainNode, serverNode, null);
                }

                // Adding Infrastructure Role Owner
                {
                    dynamic serverObject = activeDirectoryDomain.InfrastructureRoleOwner;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Infrastructure Role Owner", domainNode, serverNode, null);
                }

                // Adding Pdc Role Owner
                {
                    dynamic serverObject = activeDirectoryDomain.PdcRoleOwner;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Pdc Role Owner", domainNode, serverNode, null);
                }

                // Adding Rid Role Owner
                {
                    dynamic serverObject = activeDirectoryDomain.RidRoleOwner;
                    Server server = AddServer(servers, serverObject);
                    Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                    graph.AddEdge("Rid Role Owner", domainNode, serverNode, null);
                }
            }

            // Adding Naming Role Owner
            {
                dynamic serverObject = ActiveDirectoryForest.NamingRoleOwner;
                Server server = AddServer(servers, serverObject);
                Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                graph.AddEdge("Naming Role Owner", forestNode, serverNode, null);
            }

            // Adding Schema Role Owner
            {
                dynamic serverObject = ActiveDirectoryForest.SchemaRoleOwner;
                Server server = AddServer(servers, serverObject);
                Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                graph.AddEdge("Schema Role Owner", forestNode, serverNode, null);
            }

            // Adding Global Catalogs
            foreach (AD.GlobalCatalog activeDirectoryGlobalCatalog in ActiveDirectoryForest.GlobalCatalogs)
            {
                dynamic serverObject = activeDirectoryGlobalCatalog;
                Server server = AddServer(servers, serverObject);
                Graph.Node serverNode = graph.AddNode(":Server", server, new List<KeyValuePair<string, object>>() { new KeyValuePair<string, object>("Name", server.Name) });

                graph.AddEdge("Global Catalog", forestNode, serverNode, null);
            }

            return graph;
        }

        private static Server AddServer(List<Server> servers, dynamic serverObject)
        {
            Server server = servers.Where(s => s.Name == serverObject.Name).FirstOrDefault();
            if (server == null)
            {
                server = new Server(serverObject);
                servers.Add(server);
            }
            else
            {
                server.ServerObjects.Add(serverObject);
            }
            return server;
        }

        private class Server
        {
            public List<dynamic> ServerObjects = new List<dynamic>();
            public Server(dynamic serverObject)
            {
                ID = Guid.NewGuid();
                ServerObjects.Add(serverObject);
            }
            public Guid ID;
            public string Name
            {
                get
                {
                    foreach (dynamic serverObject in ServerObjects)
                    {
                        if (serverObject.Name != "")
                        {
                            return serverObject.Name;
                        }
                    }
                    return "UNKNOWN";
                }
            }
            public string OSVersion
            {
                get
                {
                    foreach (dynamic serverObject in ServerObjects)
                    {
                        if (serverObject.OSVersion != "")
                        {
                            return serverObject.OSVersion;
                        }
                    }
                    return "UNKNOWN";
                }
            }
            public string IPAddress
            {
                get
                {
                    foreach (dynamic serverObject in ServerObjects)
                    {
                        if (serverObject.IPAddress != "")
                        {
                            return serverObject.IPAddress;
                        }
                    }
                    return "UNKNOWN";
                }
            }
        }
    }
}
