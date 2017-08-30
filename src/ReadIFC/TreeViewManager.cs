using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using devDept.Eyeshot;
using devDept.Graphics;
using devDept.Eyeshot.Entities;

namespace WindowsApplication1
{
    public static class TreeViewManager
    {
        /// <summary>
        /// Recursive function to populate the tree. 
        /// </summary>
        /// <param name="tv">The treeView control</param>
        /// <param name="entList">The entity list</param>
        /// <param name="blocks">The block collection</param>
        /// <param name="parentNode">The parent node. Can be null for root level nodes.</param>
        public static void PopulateTree(TreeView tv, List<Entity> entList, BlockDictionary blocks, TreeNode parentNode = null)
        {
            TreeNodeCollection nodes;
            if (parentNode == null)
            {
                tv.Nodes.Clear();
                nodes = tv.Nodes;
            }
            else
            {
                nodes = parentNode.Nodes;
            }

            for (int i = 0; i < entList.Count; i++)
            {
                Entity ent = entList[i];
                if (ent.EntityData != null)
                {
                    string[] data = ((string)ent.EntityData).Split(new char[] { '|' }, 2);

                    TreeNode keyWordNode = null;
                    foreach (TreeNode node in nodes)
                    {
                        if (node.Text.Equals(data[0]))
                        {
                            keyWordNode = node;
                            break;
                        }
                    }
                    if (keyWordNode == null)
                    {
                        keyWordNode = new TreeNode(data[0]);
                        keyWordNode.ImageIndex = 1;
                        keyWordNode.SelectedImageIndex = 1;
                        nodes.Add(keyWordNode);
                    }

                    TreeNode glIdNode = new TreeNode(data[1]);
                    glIdNode.Tag = ent;
                    glIdNode.ImageIndex = 1;
                    glIdNode.SelectedImageIndex = 1;
                    keyWordNode.Nodes.Add(glIdNode);
                }
                //if (ent is BlockReference)
                //{
                //    Block child;
                //    string blockName = ((BlockReference)ent).BlockName;

                //    if (blocks.TryGetValue(blockName, out child))
                //    {
                //        TreeNode parentTn = new TreeNode(GetNodeName(blockName, i));
                //        parentTn.Tag = ent;
                //        parentTn.ImageIndex = 0;
                //        parentTn.SelectedImageIndex = 0;

                //        nodes.Add(parentTn);
                //        PopulateTree(tv, child.Entities, blocks, parentTn);
                //    }
                //}
                //else
            }
        }

        /// <summary>
        /// Clear selection for entities to avoid problems with the Tree->Screen selection
        /// </summary>
        /// <param name="vl">The ViewportLayout control</param>
        /// <param name="rootLevel">When true the CurrentBlockReference is set to null (Go back to the root level of the assembly)</param>
        public static void CleanCurrent(ViewportLayout vl, bool rootLevel = true)
        {
            vl.Entities.ClearSelection();

            if (rootLevel && vl.Entities.CurrentBlockReference != null)
                vl.Entities.SetCurrent(null);
        }

        /// <summary>
        /// Deletes the selected tree node and all the others nodes that are linked to the same entity instance.
        /// </summary>
        /// <param name="tv">The TreeView control</param>
        /// <param name="vl">The ViewportLayout control</param>
        public static void DeleteSelectedNode(TreeView tv, ViewportLayout vl)
        {
            Entity deletedEntity = tv.SelectedNode.Tag as Entity;
            DeleteNodes(deletedEntity, tv.Nodes);
            CleanCurrent(vl);
            tv.SelectedNode = null;
        }

        /// <summary>
        /// Deletes all the tree nodes that are referring to the same entity instance
        /// </summary>
        /// <param name="entity">The entity instance.</param>
        /// <param name="nodes">The TreeNode collection</param>
        private static void DeleteNodes(Entity entity, TreeNodeCollection nodes)
        {
            int count = nodes.Count;
            while (count > 0)
            {
                count--;
                TreeNode node = nodes[count];
                if (ReferenceEquals(entity, node.Tag))
                {
                    node.Remove();
                    count = -1;
                }
                else
                {
                    DeleteNodes(entity, node.Nodes);
                }
            }
        }

        /// <summary>
        /// Tree->Screen Selection. If the viewport entities are selected, they get marked as selected straight away.
        /// To check we are considering the correct entities we use the Entity stored in the Tag property of the TreeView
        /// </summary>
        /// <returns>The selected item.</returns>
        /// <param name="tv">The TreeView control</param>
        /// <param name="vl">The ViewportLayout control</param>
        public static ViewportLayout.SelectedItem SynchTreeSelection(TreeView tv, ViewportLayout vl)
        {
            // Fill a stack of entities and blockreferences starting from the node tags.
            Stack<BlockReference> parents = new Stack<BlockReference>();

            TreeNode node = tv.SelectedNode;
            Entity entity = node.Tag as Entity;

            node = node.Parent;

            while (node != null)
            {
                var ent = node.Tag as Entity;
                if (ent != null)

                    parents.Push((BlockReference)ent);

                node = node.Parent;
            }

            tv.HideSelection = false;

            // The top most parent is the root Blockreference: must reverse the order, creating a new Stack
            var selItem = new ViewportLayout.SelectedItem(new Stack<BlockReference>(parents), entity);

            // Selects the item
            selItem.Select(vl, true);

            return selItem;

        }

        /// <summary>
        /// Screen->Tree Selection.
        /// </summary>
        /// <param name="tv">The TreeView control</param>
        /// <param name="blockReferences">The BlockReference stack</param>
        /// <param name="selectedEntity">The selected entity inside a block reference. Can be null when we click on a BlockReference.</param>
        public static void SynchScreenSelection(TreeView tv, Stack<BlockReference> blockReferences, ViewportLayout.SelectedItem selectedEntity)
        {
            tv.SelectedNode = null;
            tv.HideSelection = false;

            tv.CollapseAll();

            if (selectedEntity != null && selectedEntity.Parents.Count > 0)
            {
                //// Add the parents of the selectedEntity to the BlockReferences stack

                // Reverse the stack so the one on top is the one at the root of the hierarchy
                var parentsReversed = selectedEntity.Parents.Reverse();

                var cumulativeStack = new Stack<BlockReference>(blockReferences);

                foreach (var br in parentsReversed)
                {
                    cumulativeStack.Push(br);
                }

                // Create a new stack with the reversed order so the one on top is the root.
                blockReferences = new Stack<BlockReference>(cumulativeStack);
            }

            SearchNodeInTree(tv, blockReferences, selectedEntity);
        }

        /// <summary>
        /// Screen->Tree Selection. To check we are considering the correct entities, we use the Entity stored in the Tag property of the TreeView.
        /// </summary>
        /// <param name="tv">The TreeView control</param>
        /// <param name="blockReferences">The block reference stack</param>
        /// <param name="selectedEntity">The selected entity inside a block reference. Can be null when we click on a BlockReference.</param>
        /// <param name="parentTn">The parent TreeNode for searching inside its nodes. Can be null.</param>
        public static void SearchNodeInTree(TreeView tv, Stack<BlockReference> blockReferences, ViewportLayout.SelectedItem selectedEntity, TreeNode parentTn = null)
        {
            if (blockReferences.Count == 0 && selectedEntity == null)
                return;

            TreeNodeCollection tnc = tv.Nodes;
            if (parentTn != null)
                tnc = parentTn.Nodes;

            if (blockReferences.Count > 0)
            {
                // Nested BlockReferences

                BlockReference br = blockReferences.Pop();

                foreach (TreeNode tn in tnc)
                {
                    if (ReferenceEquals(br, tn.Tag))
                    {
                        if (blockReferences.Count > 0)
                        {
                            SearchNodeInTree(tv, blockReferences, selectedEntity, tn);
                        }
                        else
                        {
                            if (selectedEntity != null)
                            {
                                foreach (TreeNode childNode in tn.Nodes)
                                {
                                    if (ReferenceEquals(selectedEntity.Item, childNode.Tag))
                                    {
                                        tv.SelectedNode = childNode;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                tv.SelectedNode = tn;
                            }
                        }

                        return;
                    }
                }
            }
            else
            {
                // Root level

                if (selectedEntity != null)
                {
                    foreach (TreeNode childNode in tnc)
                    {
                        if (ReferenceEquals(selectedEntity.Item, childNode.Tag))
                        {
                            tv.SelectedNode = childNode;
                            break;
                        }
                    }
                }

            }
        }
    }
}
