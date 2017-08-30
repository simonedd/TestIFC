using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using devDept.Eyeshot.Entities;
using devDept.Eyeshot;
using devDept.Graphics;
using devDept.Geometry;
using System.Collections;
using System.Linq;
using GeometryGym.Ifc;
using System.Diagnostics;

namespace WindowsApplication1
{
    public partial class Form1 : Form
    {
        private int testLayer;

        private string debug = "";

        private Transformation trs;

        private bool _treeModify;

        public Form1()
        {
            InitializeComponent();

            // Listens the events to handle the tree synchronization
            viewportLayout1.MouseDown += ViewportLayout1_MouseDown;
            viewportLayout1.MouseClick += ViewportLayout1_MouseClick;
            viewportLayout1.MouseDoubleClick += ViewportLayout1_MouseDoubleClick;

            // Listens the events to handle the deletion of the selected entity
            viewportLayout1.KeyDown += ViewportLayout1_KeyDown;
            modelTree.KeyDown += TreeView1_KeyDown;

            Unlock();
        }

        protected override void OnLoad(EventArgs e)
        {
            _treeModify = false;

            String[] handleElements = { "IfcBeam", "IfcColumn", "IfcSlab", "IfcStair", "IfcPile", "IfcFooting", "IfcWallStandardCase", "IfcWindow", "IfcDoor", "IfcBuildingElementProxy" };

            testLayer = viewportLayout1.Layers.Add("testLayer", Color.Red);

            viewportLayout1.Layers[0].Name = "Default";
            viewportLayout1.Layers[0].Color = Color.Gray;
            
            //DatabaseIfc db = new DatabaseIfc("C:\\devdept\\IFC Model.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\MOD-Padrão\\MOD-Padrão.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\Blueberry031105_Complete_optimized.ifc");
            DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\c_rvt8_Townhouse.ifc");
            

            IfcProject project = db.Project;

            List<IfcBuildingElement> elements = project.Extract<IfcBuildingElement>();

            List<IfcSpatialElement> spElements = project.Extract<IfcSpatialElement>();
            
            //ci sono piu elementi uguali ( stesso mark )
            foreach (IfcBuildingElement element in elements)
            {
                if (/*handleElements.Contains(element.KeyWord) && */element.Placement!= null && element.Representation != null)
                {   
                    trs = Conversion.getPlacementTransformtion((IfcLocalPlacement)element.Placement);

                    

                    IfcProductRepresentation prodRep = (IfcProductRepresentation)element.Representation;

                    Entity entity = Conversion.getEntityFromIfcProductRepresentation(prodRep, viewportLayout1);

                    #region Color
                    //try
                    //{
                    //    IfcMaterialLayerSetUsage mls = (IfcMaterialLayerSetUsage)element.MaterialSelect;
                    //    if (mls != null)
                    //    {


                    //        IfcPresentationStyleAssignment ps = (IfcPresentationStyleAssignment)mls.ForLayerSet.MaterialLayers[0].PrimaryMaterial.HasRepresentation.Representations[0].Items[0].Styles[0];
                    //        IfcSurfaceStyle ss = (IfcSurfaceStyle)ps.Styles[0];
                    //        IfcSurfaceStyleRendering ssr = (IfcSurfaceStyleRendering)ss.Styles[0];
                    //        Color color = ssr.SurfaceColour.Colour;


                    //        entity.ColorMethod = colorMethodType.byEntity;

                    //        entity.Color = color;
                    //    }
                    //}
                    //catch (Exception exc)
                    //{
                    //    Debug.Write(exc);
                    //}
                    //element.MaterialSelect.PrimaryMaterial.HasRepresentation.Representations[0].Items[0].Styles[0]....

                    //fcPresentationStyleAssignment psa = (IfcPresentationStyleAssignment)extrAreaSolid.StyledByItem.Styles[0];
                    //IfcSurfaceStyle ss = (IfcSurfaceStyle)psa.Styles[0];
                    //IfcSurfaceStyleRendering ssr = (IfcSurfaceStyleRendering)ss.Styles[0];
                    //Color color = ssr.SurfaceColour.Colour;
#endregion

                    if(entity != null)
                    {
                        entity.TransformBy(trs);

                        if (element.HasOpenings.Count > 0 )//&& entity is Mesh)    //gestire opening se non e mesh
                        {
                            Mesh m = (Mesh)entity;

                            Solid entitySolid = m.ConvertToSolid();

                            foreach (IfcRelVoidsElement relVE in element.HasOpenings)
                            {
                                Mesh openingMesh = (Mesh)Conversion.getEntityFromIfcProductRepresentation(relVE.RelatedOpeningElement.Representation, viewportLayout1);

                                Transformation opTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)relVE.RelatedOpeningElement.Placement);

                                openingMesh.TransformBy(opTrs);

                                Solid openingSolid = openingMesh.ConvertToSolid();

                                Solid[] result;

                                result = Solid.Difference(entitySolid, openingSolid, 0.00001);

                                if (result != null)
                                    entitySolid = result[0];
                                else
                                    debug += "Error in boolean operation\n";
                            }
                            entity = entitySolid;
                        }

                        entity.EntityData = element.KeyWord + "|" + element.GlobalId;

                        //entity.TransformBy(trs);

                        viewportLayout1.Entities.Add(entity, 0);
                    }

                }
                else
                {
                    if(!debug.Contains("IfcElement error: " + element.KeyWord))
                        debug += "IfcElement error: " + element.KeyWord + "\n";
                }
            }

            debug += Conversion.Debug;

            Debug.WriteLine(debug);

            viewportLayout1.Entities.AddRange(Conversion.DebugEntity, testLayer);

            TreeViewManager.PopulateTree(modelTree, viewportLayout1.Entities.ToList(), viewportLayout1.Blocks);

            //viewportLayout1.DisplayMode = displayType.Shaded;

            //viewportLayout1.Layers.TurnOff("testLayer");
            //viewportLayout1.Layers.TurnOff("Default");
            viewportLayout1.ZoomFit();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
        }




        #region gestione TreeView
        private enum MouseClickType
        {
            LeftClick,
            LeftDoubleClick,
            RightClick
        }

        private System.Drawing.Point _mousePosition;
        private void ViewportLayout1_MouseDown(object sender, MouseEventArgs e)
        {
            // Gets the mouse location
            _mousePosition = e.Location;
        }

        private void ViewportLayout1_MouseClick(object sender, MouseEventArgs e)
        {
            if (viewportLayout1.ActionMode != actionType.None) return;

            if (e.Button == MouseButtons.Left)
            {
                // Selects the entity under the mouse     
                Selection(MouseClickType.LeftClick);
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Sets the parent's BlockReference as current (so I can select the parent's entities with one click).
                Selection(MouseClickType.RightClick);
            }
        }

        private void ViewportLayout1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (viewportLayout1.ActionMode != actionType.None) return;

            if (e.Button == MouseButtons.Left)
                // Sets the BlockReference as current (so I can select its entities with one click).
                Selection(MouseClickType.LeftDoubleClick);
        }

        private ViewportLayout.SelectedItem lastSelectedItem;

        private void Selection(MouseClickType mouseClickType)
        {
            if (_treeModify)
                return;

            _treeModify = true;

            if (mouseClickType == MouseClickType.RightClick)
            {
                // Sets the parent of the current BlockReference as current.
                viewportLayout1.Entities.SetParentAsCurrent();
            }
            else
            {
                // Deselects the previously selected item
                if (lastSelectedItem != null)
                {
                    lastSelectedItem.Select(viewportLayout1, false);
                    lastSelectedItem = null;
                }

                var item = viewportLayout1.GetItemUnderMouseCursor(_mousePosition);

                if (item != null)
                {
                    lastSelectedItem = item;

                    TreeViewManager.CleanCurrent(viewportLayout1, false);

                    // Marks as selected the entity under the mouse cursor.
                    item.Select(viewportLayout1, true);
                }
                else
                {
                    // Back to the root level                
                    if (mouseClickType == MouseClickType.LeftDoubleClick)
                        TreeViewManager.CleanCurrent(viewportLayout1);
                }
            }

            // An entity in the viewport was selected, so we highlight the corresponding element in the treeview as well                        
            TreeViewManager.SynchScreenSelection(modelTree, new Stack<BlockReference>(viewportLayout1.Entities.CurrentBlockReferences), lastSelectedItem);

            viewportLayout1.Invalidate();

            _treeModify = false;
        }

        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_treeModify)
            {
                return;
            }

            _treeModify = true;

            //An element of the treeview was selected, so we select the corresponding viewport element as well

            if (lastSelectedItem != null)
                lastSelectedItem.Select(viewportLayout1, false);

            TreeViewManager.CleanCurrent(viewportLayout1);
            lastSelectedItem = TreeViewManager.SynchTreeSelection(modelTree, viewportLayout1);

            viewportLayout1.Invalidate();

            _treeModify = false;
        }

        private void TreeView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                viewportLayout1.Entities.DeleteSelected();
                TreeViewManager.DeleteSelectedNode(modelTree, viewportLayout1);

                viewportLayout1.Invalidate();
            }
        }

        private void ViewportLayout1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                TreeNode selectedNode = modelTree.SelectedNode;
                if (selectedNode != null && ((Entity)selectedNode.Tag).Selected)
                {
                    // Removes all the nodes linked to the deleted entity                                        
                    TreeViewManager.DeleteSelectedNode(modelTree, viewportLayout1);
                }
            }
        }

        #endregion
    }
}

