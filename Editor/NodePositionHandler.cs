using UnityEngine;
using System.Collections.Generic;
using Nodes;
using Nodes.Decorator;
using Tree;

namespace Editor
{
    public class NodePositionHandler
    {
        /// <summary>
        /// Assigns positions to all nodes in a dialog tree based on hierarchy.
        /// Each child is 30px below its parent, siblings are 100px apart horizontally.
        /// </summary>
        public static void AssignPositions(DialogOptionNode root, float startX = 0f, float startY = 0f)
        {
            AssignPositionsByLevel(root, startX, startY);
        }

        private static void AssignPositionsRecursive(DialogOptionNode node, float x, float y)
        {
            if (node == null) return;
            node.Position = new Vector2(x, y);
            if (node.Children == null || node.Children.Count == 0) return;
            float childY = y - 30f;
            float baseX = x - ((node.Children.Count - 1) * 100f) / 2f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i] as DialogOptionNode;
                if (child != null)
                {
                    float childX = baseX + i * 100f;
                    AssignPositionsRecursive(child, childX, childY);
                }
            }
        }

        /// <summary>
        /// Assigns positions to all nodes in a dialog tree so that nodes with the same number of parents (depth) are at the same y-position.
        /// Siblings are spaced 100px apart horizontally.
        /// </summary>
        public static void AssignPositionsByLevel(DialogOptionNode root, float startX = 0f, float startY = 0f)
        {
            var levelToNodes = new Dictionary<int, List<DialogOptionNode>>();
            var visited = new HashSet<DialogOptionNode>();
            void Traverse(DialogOptionNode node, int depth)
            {
                if (node == null || visited.Contains(node)) return;
                visited.Add(node);
                if (!levelToNodes.ContainsKey(depth))
                    levelToNodes[depth] = new List<DialogOptionNode>();
                levelToNodes[depth].Add(node);
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        if (child is DialogOptionNode childNode)
                            Traverse(childNode, depth + 1);
                    }
                }
            }
            Traverse(root, 0);

            float verticalSpacing = 30f;
            float horizontalSpacing = 100f;
            foreach (var kvp in levelToNodes)
            {
                int depth = kvp.Key;
                var nodesAtLevel = kvp.Value;
                float y = startY - depth * verticalSpacing;
                float baseX = startX - ((nodesAtLevel.Count - 1) * horizontalSpacing) / 2f;
                for (int i = 0; i < nodesAtLevel.Count; i++)
                {
                    nodesAtLevel[i].Position = new Vector2(baseX + i * horizontalSpacing, y);
                }
            }
        }
        
        public static void AssignPositionsByLevel(DialogTree tree, float startX = 0f, float startY = 0f, float verticalSpacing = 120f, float horizontalSpacing = 500f)
        {
            // Dictionary to store: depth -> list of nodes at that depth
            var levelToNodes = new Dictionary<int, List<Node>>();
            // Dictionary to store: node -> depth
            var nodeToDepth = new Dictionary<Node, int>();
            // HashSet to avoid visiting nodes multiple times (in case of cycles)
            var visited = new HashSet<Node>();

            // Find root nodes (nodes with no parents)
            var childSet = new HashSet<Node>();
            foreach (var node in tree.nodes)
            {
                if (node is CompositeNode composite && composite.Children != null)
                {
                    foreach (var child in composite.Children)
                    {
                        if (child != null)
                            childSet.Add(child);
                    }
                }
            }
            var rootNodes = new List<Node>();
            foreach (var node in tree.nodes)
            {
                if (!childSet.Contains(node))
                    rootNodes.Add(node);
            }

            // BFS traversal to assign depth and collect nodes per level
            var queue = new Queue<(Node node, int depth)>();
            foreach (var root in rootNodes)
            {
                queue.Enqueue((root, 0));
            }
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (visited.Contains(node)) continue;
                visited.Add(node);
                nodeToDepth[node] = depth;
                if (!levelToNodes.ContainsKey(depth))
                    levelToNodes[depth] = new List<Node>();
                levelToNodes[depth].Add(node);
                if (node is CompositeNode composite && composite.Children != null)
                {
                    foreach (var child in composite.Children)
                    {
                        if (child != null && !visited.Contains(child))
                            queue.Enqueue((child, depth + 1));
                    }
                }
            }

            // Assign positions: equally distribute nodes at each level on the x-axis, all at the same y
            foreach (var kvp in levelToNodes)
            {
                int depth = kvp.Key;
                var nodesAtLevel = kvp.Value;
                float y = startY + depth * verticalSpacing; // Higher level = higher y
                int count = nodesAtLevel.Count;
                if (count == 1)
                {
                    nodesAtLevel[0].Position = new Vector2(startX, y);
                }
                else
                {
                    float totalWidth = (count - 1) * horizontalSpacing;
                    float leftX = startX - totalWidth / 2f;
                    for (int i = 0; i < count; i++)
                    {
                        float x = leftX + i * horizontalSpacing;
                        nodesAtLevel[i].Position = new Vector2(x, y);
                    }
                }
            }
        }

        /// <summary>
        /// Positions nodes so that each group of children is centered under its parent.
        /// The middle child (for even numbers, the second, fourth, etc.) is exactly centered under the parent.
        /// </summary>
        public static void AssignPositionsTreeLike(DialogTree tree, float startX = 0f, float startY = 0f, float verticalSpacing = 120f, float horizontalSpacing = 250f)
        {
            // Helper: Calculate subtree width for each node
            var subtreeWidths = new Dictionary<Node, int>();
            int CalculateSubtreeWidth(Node node)
            {
                if (!(node is CompositeNode composite) || composite.Children == null || composite.Children.Count == 0)
                {
                    subtreeWidths[node] = 1;
                    return 1;
                }
                int width = 0;
                foreach (var child in composite.Children)
                {
                    width += CalculateSubtreeWidth(child);
                }
                subtreeWidths[node] = width;
                return width;
            }

            // Find root nodes (nodes with no parents)
            var childSet = new HashSet<Node>();
            foreach (var node in tree.nodes)
            {
                if (node is CompositeNode composite && composite.Children != null)
                {
                    foreach (var child in composite.Children)
                    {
                        if (child != null)
                            childSet.Add(child);
                    }
                }
            }
            var rootNodes = new List<Node>();
            foreach (var node in tree.nodes)
            {
                if (!childSet.Contains(node))
                    rootNodes.Add(node);
            }

            foreach (var root in rootNodes)
                CalculateSubtreeWidth(root);

            // Recursive position assignment
            void PositionSubtree(Node node, float x, float y)
            {
                node.Position = new Vector2(x, y);
                if (!(node is CompositeNode composite) || composite.Children == null || composite.Children.Count == 0)
                    return;
                float totalWidth = subtreeWidths[node];
                float childX = x - ((totalWidth - 1) * horizontalSpacing) / 2f;
                foreach (var child in composite.Children)
                {
                    int childWidth = subtreeWidths[child];
                    float childCenterX = childX + ((childWidth - 1) * horizontalSpacing) / 2f;
                    PositionSubtree(child, childCenterX, y + verticalSpacing);
                    childX += childWidth * horizontalSpacing;
                }
            }

            // Distribute root nodes horizontally if there are multiple roots
            float nextRootX = startX;
            foreach (var root in rootNodes)
            {
                int width = subtreeWidths[root];
                float rootCenterX = nextRootX + ((width - 1) * horizontalSpacing) / 2f;
                PositionSubtree(root, rootCenterX, startY);
                nextRootX += width * horizontalSpacing;
            }
        }

    }
}