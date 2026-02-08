#!/usr/bin/env python3
"""
Visualize a GraphML with an interactive HTML graph (PyVis/vis-network),
and inject Save PNG + Save SVG buttons into the generated HTML.

What it does
- Parses GraphML (including the default GraphML namespace)
- Uses node "Name" (fallback to id) as the display label
- Colors nodes by the ":Label" type (Forest, Site, Server, etc)
- Labels edges using the edge @label attribute (fallback to data key="labels")
- Writes an interactive HTML file you can open in a browser
- Adds floating buttons in the HTML: Save PNG, Save SVG (current view)

Install:
  pip install networkx pyvis

Run:
  python graphml_viz.py --in input.graphml --out graph.html
"""

from __future__ import annotations

import argparse
import itertools
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

import networkx as nx
from pyvis.network import Network


def _read_graphml_xml(path: Path) -> tuple[list[dict], list[dict]]:
    """
    Minimal GraphML parser that extracts nodes + edges plus their <data key="..."> values.

    Returns:
      nodes: [{"id": "...", "attr": {...}}]
      edges: [{"source": "...", "target": "...", "attr": {...}}]
    """
    tree = ET.parse(path)
    root = tree.getroot()

    # Handle GraphML default namespace, like: xmlns="http://graphml.graphdrawing.org/xmlns"
    ns = {"g": "http://graphml.graphdrawing.org/xmlns"}

    graph = root.find("g:graph", ns)
    if graph is None:
        raise ValueError("Could not find <graph> element in GraphML.")

    nodes: list[dict] = []
    edges: list[dict] = []

    for n in graph.findall("g:node", ns):
        node_id = n.attrib.get("id")
        if not node_id:
            continue
        attr: dict[str, str] = {}

        # Attributes directly on <node ...>
        for k, v in n.attrib.items():
            if k != "id":
                attr[k] = v

        # <data key="...">value</data>
        for d in n.findall("g:data", ns):
            key = d.attrib.get("key")
            if key:
                attr[key] = (d.text or "").strip()

        nodes.append({"id": node_id, "attr": attr})

    for e in graph.findall("g:edge", ns):
        src = e.attrib.get("source")
        tgt = e.attrib.get("target")
        if not src or not tgt:
            continue
        attr: dict[str, str] = {}

        # Attributes directly on <edge ...>
        for k, v in e.attrib.items():
            if k not in ("source", "target"):
                attr[k] = v

        # <data key="...">value</data>
        for d in e.findall("g:data", ns):
            key = d.attrib.get("key")
            if key:
                attr[key] = (d.text or "").strip()

        edges.append({"source": src, "target": tgt, "attr": attr})

    return nodes, edges


def _type_from_labels(labels_value: str) -> str:
    """
    Your nodes have labels like ':Forest'. Normalize to 'Forest'.
    """
    if not labels_value:
        return "Unknown"
    return labels_value.replace(":", "").strip() or "Unknown"


def build_nx_graph(nodes: list[dict], edges: list[dict]) -> nx.DiGraph:
    G = nx.DiGraph()

    for n in nodes:
        node_id = n["id"]
        attr = n["attr"]
        display = attr.get("Name") or node_id

        # Prefer attribute "labels" from node attribs, fallback to <data key="labels">
        raw_labels = attr.get("labels", "")
        node_type = _type_from_labels(raw_labels)

        G.add_node(node_id, display=display, node_type=node_type, **attr)

    for e in edges:
        src = e["source"]
        tgt = e["target"]
        attr = e["attr"]
        edge_label = attr.get("label") or attr.get("labels") or ""
        G.add_edge(src, tgt, edge_label=edge_label, **attr)

    return G


def make_color_map(types: set[str]) -> dict[str, str]:
    palette = [
        "#4C78A8", "#F58518", "#54A24B", "#E45756", "#72B7B2",
        "#EECA3B", "#B279A2", "#FF9DA6", "#9D755D", "#BAB0AC",
    ]
    cmap: dict[str, str] = {}
    for t, c in zip(sorted(types), itertools.cycle(palette)):
        cmap[t] = c
    return cmap


def inject_export_buttons(out_html: Path) -> None:
    """
    Post-process the generated PyVis HTML file and inject floating
    "Save PNG" and "Save SVG" buttons.
    """
    html = out_html.read_text(encoding="utf-8")

    # The injected toolbar and JavaScript.
    # SVG export uses canvas2svg to re-render the current network view.
    toolbar_html = r"""
<!-- Export toolbar (injected) -->
<div id="export-toolbar" style="
  position: fixed;
  top: 12px;
  right: 12px;
  z-index: 9999;
  background: rgba(255,255,255,0.95);
  border: 1px solid #ccc;
  border-radius: 8px;
  padding: 10px;
  font-family: Arial, sans-serif;
  box-shadow: 0 2px 8px rgba(0,0,0,0.15);
">
  <div style="display:flex; gap:8px; align-items:center;">
    <button id="btn-save-png" style="padding:6px 10px; cursor:pointer;">Save PNG</button>
    <button id="btn-save-svg" style="padding:6px 10px; cursor:pointer;">Save SVG</button>
  </div>
  <div style="margin-top:6px; font-size:12px; color:#555;">
    Exports the current view
  </div>
</div>

<!-- canvas2svg for SVG export -->
<script src="https://cdn.jsdelivr.net/npm/canvas2svg@1.0.16/canvas2svg.js"></script>

<script>
(function () {
  function downloadBlob(filename, blob) {
    var url = URL.createObjectURL(blob);
    var a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function(){ URL.revokeObjectURL(url); }, 1000);
  }

  function getNetworkCanvas() {
    // PyVis generated HTML usually contains: var network = new vis.Network(...)
    if (typeof network === "undefined" || !network.canvas || !network.canvas.frame || !network.canvas.frame.canvas) {
      return null;
    }
    return network.canvas.frame.canvas;
  }

  document.getElementById("btn-save-png").addEventListener("click", function () {
    var canvas = getNetworkCanvas();
    if (!canvas) {
      alert("Could not find the vis-network canvas (variable 'network').");
      return;
    }
    try {
      var dataUrl = canvas.toDataURL("image/png");
      var a = document.createElement("a");
      a.href = dataUrl;
      a.download = "graph.png";
      document.body.appendChild(a);
      a.click();
      a.remove();
    } catch (e) {
      alert("PNG export failed: " + e);
    }
  });

  document.getElementById("btn-save-svg").addEventListener("click", function () {
    var canvas = getNetworkCanvas();
    if (!canvas) {
      alert("Could not find the vis-network canvas (variable 'network').");
      return;
    }
    if (typeof C2S === "undefined") {
      alert("SVG export library (canvas2svg) not loaded.");
      return;
    }

    try {
      var width = canvas.width;
      var height = canvas.height;

      // Create SVG context
      var ctx = new C2S(width, height);

      // Monkeypatch getContext so vis-network draws into our SVG context
      var originalGetContext = canvas.getContext.bind(canvas);
      canvas.getContext = function (type) {
        if (type === "2d") return ctx;
        return originalGetContext(type);
      };

      // Redraw into SVG context
      network.redraw();

      // Restore normal drawing
      canvas.getContext = originalGetContext;
      network.redraw();

      // Serialize SVG
      var svgText = ctx.getSerializedSvg(true);
      var blob = new Blob([svgText], { type: "image/svg+xml;charset=utf-8" });
      downloadBlob("graph.svg", blob);
    } catch (e) {
      alert("SVG export failed: " + e);
    }
  });
})();
</script>
<!-- /Export toolbar (injected) -->
"""

    # Inject before </body> (case-sensitive in most generated pages, but we handle the common variant)
    if "</body>" in html:
        html = html.replace("</body>", toolbar_html + "\n</body>", 1)
    elif "</BODY>" in html:
        html = html.replace("</BODY>", toolbar_html + "\n</BODY>", 1)
    else:
        raise ValueError("Could not find </body> tag in generated HTML to inject export buttons.")

    out_html.write_text(html, encoding="utf-8")


def export_pyvis(G: nx.DiGraph, out_html: Path, show_buttons: bool = True, inject_buttons: bool = True) -> None:
    net = Network(height="900px", width="100%", directed=True, notebook=False)

    # Physics gives a good automatic layout for small graphs
    net.barnes_hut(
        gravity=-20000,
        central_gravity=0.2,
        spring_length=180,
        spring_strength=0.02,
        damping=0.09,
    )

    types = {G.nodes[n].get("node_type", "Unknown") for n in G.nodes}
    cmap = make_color_map(types)

    for n, data in G.nodes(data=True):
        label = str(data.get("display", n))
        node_type = data.get("node_type", "Unknown")
        title_lines = [
            f"<b>{label}</b>",
            f"Type: {node_type}",
            f"ID: {n}",
        ]
        for k, v in sorted(data.items()):
            if k in ("display", "node_type"):
                continue
            title_lines.append(f"{k}: {v}")

        net.add_node(
            n,
            label=label,
            title="<br/>".join(title_lines),
            color=cmap.get(node_type, "#999999"),
        )

    for u, v, data in G.edges(data=True):
        elabel = str(data.get("edge_label", "") or "")
        title_lines = [f"<b>{elabel}</b>" if elabel else "<b>edge</b>"]
        for k, val in sorted(data.items()):
            if k == "edge_label":
                continue
            title_lines.append(f"{k}: {val}")

        net.add_edge(
            u,
            v,
            label=elabel,
            title="<br/>".join(title_lines),
            arrows="to",
        )

    if show_buttons:
        # PyVis config panel (physics, layout, interaction)
        net.show_buttons(filter_=["physics", "layout", "interaction"])

    out_html.parent.mkdir(parents=True, exist_ok=True)
    net.write_html(str(out_html))

    if inject_buttons:
        inject_export_buttons(out_html)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True, help="Input GraphML file path")
    ap.add_argument("--out", dest="out", default="graph.html", help="Output HTML file path")
    ap.add_argument("--no-buttons", action="store_true", help="Disable pyvis config buttons")
    ap.add_argument("--no-export", action="store_true", help="Disable injected Save PNG/SVG buttons")
    args = ap.parse_args()

    inp = Path(args.inp)
    out = Path(args.out)

    if not inp.exists():
        print(f"Input file not found: {inp}", file=sys.stderr)
        return 2

    nodes, edges = _read_graphml_xml(inp)
    G = build_nx_graph(nodes, edges)

    export_pyvis(
        G,
        out,
        show_buttons=not args.no_buttons,
        inject_buttons=not args.no_export,
    )

    print(f"Wrote: {out.resolve()}")
    print("Open it in a browser to pan, zoom, and drag nodes around.")
    print("Use the top-right buttons to export PNG/SVG of the current view.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
