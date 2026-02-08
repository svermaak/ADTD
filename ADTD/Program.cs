using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ADTD
{
    class Program
    {
        static void Main(string[] args)
        {
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + Environment.NewLine + "<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://graphml.graphdrawing.org/xmlns http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd\">" + Environment.NewLine + "{Keys}" + Environment.NewLine + "<graph id=\"G\" edgedefault=\"directed\">" + Environment.NewLine + "{Nodes}" + Environment.NewLine + "{Edges}" + Environment.NewLine + "</graph>" + Environment.NewLine + "</graphml>";
            string nodes = "";
            string edges = "";

            Models.Topology topology = new Models.Topology();

            Forest forest = Forest.GetCurrentForest();
            string domainName = forest.RootDomain.Name;

            DirectoryContext directoryContext =
                new DirectoryContext(DirectoryContextType.Forest, domainName);

            Models.Graph graph = topology.GenerateGraph(Forest.GetForest(directoryContext));

            foreach(Models.Graph.Node node in graph.Nodes)
            {
                nodes = nodes + Environment.NewLine + CreateNode(node.ID.ToString(), node.Label, node.Data);
            }

            foreach (Models.Graph.Edge edge in graph.Edges)
            {
                edges = edges + Environment.NewLine + CreateEdge(edge.ID.ToString(), edge.Source.ID.ToString(), edge.Target.ID.ToString(), edge.Label,edge.Data);
            }

            xml = xml.Replace("{Keys}", "").Replace("{Nodes}", nodes).Replace("{Edges}", edges);

            string fileName = $"ADTD_{DateTime.Now:yyyyMMdd_HHmmss}.xml"; // "nicetimestamp"
            string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

            File.WriteAllText(filePath, xml);

            Console.WriteLine($"Wrote: {filePath}");
        }
        static string CreateNode(string ID, string Labels, List<KeyValuePair<string, object>> DataEntries = null)
        {
            string data = $"<data key=\"labels\">{Labels}</data>";
            if (DataEntries != null)
            {
                foreach (KeyValuePair<string, object> dataEntry in DataEntries)
                {
                    data = data + $"<data key=\"{dataEntry.Key}\">{dataEntry.Value}</data>";
                }
            }
            return $"<node id=\"{ID}\" labels=\"{Labels}\">{data}</node>";
        }
        static string CreateEdge(string ID, string SourceID, string TargetID, string Label, List<KeyValuePair<string, object>> DataEntries = null)
        {
            string data = $"<data key=\"labels\">{Label}</data>";
            if (DataEntries != null)
            {
                foreach (KeyValuePair<string, object> dataEntry in DataEntries)
                {
                    data = data + $"<data key=\"{dataEntry.Key}\">{dataEntry.Value}</data>";
                }
            }
            return $"<edge id=\"{ID}\" source=\"{SourceID}\" target=\"{TargetID}\" label=\"{Label}\">{data}</edge>";
        }
    }
}
