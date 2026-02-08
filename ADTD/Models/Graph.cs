using System;
using System.Collections.Generic;
using System.Linq;

namespace ADTD.Models
{
    public class Graph
    {
        public List<Node> Nodes = new List<Node>();
        public List<Edge> Edges = new List<Edge>();
        public class Node
        {
            public Guid ID;
            public string Label;
            public List<KeyValuePair<string, object>> Data = new List<KeyValuePair<string, object>>();
            public object Object;
            public Node(string NodeLabel, object NodeObject, List<KeyValuePair<string, object>> NodeData)
            {
                ID = Guid.NewGuid();
                Label = NodeLabel;
                Object = NodeObject;
                Data = NodeData;
            }
        }
        public class Edge
        {
            public Guid ID;
            public string Label;
            public Node Source;
            public Node Target;
            public List<KeyValuePair<string, object>> Data = new List<KeyValuePair<string, object>>();
            public Edge(string EdgeLabel, Node EdgeSource, Node EdgeTarget, List<KeyValuePair<string, object>> EdgeData)
            {
                ID = Guid.NewGuid();
                Label = EdgeLabel;
                Source = EdgeSource;
                Target = EdgeTarget;
                Data = EdgeData;
            }
        }
        public Node AddNode(string Label, object Object, List<KeyValuePair<string, object>> NodeData)
        {
            Node node = Nodes.Where(n => ((n.Label == Label) && (n.Data.Contains(NodeData.Where(d => d.Key == "Name").FirstOrDefault())))).FirstOrDefault();
            if (node == null)
            {
                node = new Node(Label, Object, NodeData);
                Nodes.Add(node);
            }
            else
            {
                node.Object = Object;
            }
            return node;
        }
        public Edge AddEdge(string Label, Node Source, Node Target, List<KeyValuePair<string, object>> NodeData)
        {
            Edge edge = Edges.Where(e => ((e.Label == Label) && (e.Source == Source) && (e.Target == Target))).FirstOrDefault();
            if (edge == null)
            {
                edge = new Edge(Label, Source, Target, NodeData);
                Edges.Add(edge);
            }
            return edge;
        }
    }
}
