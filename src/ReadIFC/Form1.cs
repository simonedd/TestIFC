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
using devDept.Eyeshot.Translators;

namespace WindowsApplication1
{
    public partial class Form1 : Form
    {
        private int testLayer;

        private int count = 0;

        private string debug = "";

        private Transformation elementTrs = new Transformation(1);

        private bool _treeModify;

        public Form1()
        {
            InitializeComponent();

            //viewportLayout1.Backface.ColorMethod = backfaceColorMethodType.SingleColor;
            //viewportLayout1.ShowCurveDirection = true;
            viewportLayout1.DisplayMode = displayType.Shaded;

            //viewportLayout1.Layers.TurnOff("testLayer");
            //viewportLayout1.Layers.TurnOff("Default");


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

            testLayer = viewportLayout1.Layers.Add("testLayer", Color.Red);
            int testLayer1 = viewportLayout1.Layers.Add("onPlane", Color.Blue);

            viewportLayout1.Layers[0].Name = "Default";
            viewportLayout1.Layers[0].Color = Color.Gray;

            //DatabaseIfc db = new DatabaseIfc("C:\\devdept\\IFC Model.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devdept\\IFC\\Martti_Ahtisaaren_RAK.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\MOD-Padrão\\MOD-Padrão.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\Blueberry031105_Complete_optimized.ifc");
            DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\c_rvt8_Townhouse.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Samples\\01 Fire Protection.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Samples\\ArchiCAD IFC Buildsoft.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Samples\\Clinic_S_20110715_optimized.ifc");

            IfcProject project = db.Project;

            List<IfcBuildingElement> elements = project.Extract<IfcBuildingElement>();

            List<IfcSpatialElement> spElements = project.Extract<IfcSpatialElement>();

            List<IfcDistributionElement> distEle = project.Extract<IfcDistributionElement>();
            //ci sono piu elementi uguali ( stesso mark )
            foreach (IfcBuildingElement ifcElement in elements)
            {    
                if (ifcElement.GlobalId.StartsWith("0tHB6cJjH63wzHjR6XgeCI") &&/**/ ifcElement.Decomposes == null)
                {
                    Entity eyeElement = null;

                    if (ifcElement.Placement != null)
                    {
                        elementTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)ifcElement.Placement);
                    }
                    IfcProductRepresentation prodRep = (IfcProductRepresentation)ifcElement.Representation;

                    if(prodRep != null)
                        eyeElement = Conversion.getEntityFromIfcProductRepresentation(prodRep, viewportLayout1, elementTrs);

                    if (eyeElement != null)
                        eyeElement.TransformBy(elementTrs);

                    if (ifcElement.IsDecomposedBy.Count > 0)
                    {
                        Block b = new Block();

                        foreach (IfcRelAggregates relAg in ifcElement.IsDecomposedBy)        //gestire se count > 1
                        {                            
                            foreach (IfcElement el in relAg.RelatedObjects)
                            {
                                Entity entity = Conversion.getEntityFromIfcProductRepresentation(el.Representation, viewportLayout1, elementTrs);  //gestire se entity is decomposet ( funz ricorsiva)

                                Transformation trs = Conversion.getPlacementTransformtion((IfcLocalPlacement)el.Placement);

                                if (entity != null)
                                {
                                    entity.TransformBy(trs);

                                    b.Entities.Add(entity);
                                }
                            }
                        }
                        if (eyeElement != null)
                            b.Entities.Add(eyeElement);

                        viewportLayout1.Blocks.Add(ifcElement.GlobalId, b);

                        eyeElement = new BlockReference(ifcElement.GlobalId);
                    }

                    if(eyeElement != null)
                    {
                        if (ifcElement.HasOpenings.Count > 0 )
                        {
                            eyeElement = this.createOpenings(eyeElement, ifcElement);
                        }

                        eyeElement.EntityData = ifcElement.KeyWord + "|" + ifcElement.GlobalId;
    
                        viewportLayout1.Entities.Add(eyeElement, 0);
                    }

                }
                else
                {
                    if(!debug.Contains("IfcElement error: " + ifcElement.KeyWord))
                        debug += "IfcElement error: " + ifcElement.KeyWord + "\n";
                }
            }


            //da correggere come buildingElement
            foreach (IfcDistributionElement element in distEle)
            {

                if (/*element.GlobalId.StartsWith("ciao") &&/*handleElements.Contains(element.KeyWord) && */element.Placement != null && element.Representation != null)
                {
                    elementTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)element.Placement);

                    IfcProductRepresentation prodRep = (IfcProductRepresentation)element.Representation;

                    Entity entity = Conversion.getEntityFromIfcProductRepresentation(prodRep, viewportLayout1, elementTrs);

                    if (entity != null)
                    {
                        entity.TransformBy(elementTrs);

                        if (element.HasOpenings.Count > 0 && (entity is Solid || entity is Mesh))    //possibile opening su entita che non e' solid o mesh ??
                        {
                            Solid entitySolid;

                            if (entity is Mesh)
                                entitySolid = ((Mesh)entity).ConvertToSolid();
                            else
                                entitySolid = (Solid)entity;

                            foreach (IfcRelVoidsElement relVE in element.HasOpenings)
                            {
                                Entity openingEntity = Conversion.getEntityFromIfcProductRepresentation(relVE.RelatedOpeningElement.Representation, viewportLayout1);

                                Transformation opTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)relVE.RelatedOpeningElement.Placement);

                                openingEntity.TransformBy(opTrs);

                                Solid openingSolid;

                                if (openingEntity is Mesh)
                                    openingSolid = ((Mesh)openingEntity).ConvertToSolid();
                                else
                                    openingSolid = (Solid)openingEntity;

                                Solid[] result;

                                result = Solid.Difference(entitySolid, openingSolid, 0.00001);

                                if (result != null)
                                {
                                    entitySolid = result[0];
                                }
                                else
                                {
                                    WriteSTL ws = new WriteSTL(new Entity[] { entitySolid, openingSolid }, new Layer[] { new Layer("Default") }, new Dictionary<string, Block>(), @"c:\devdept\booleanError\" + count + " " + element.GlobalId + ".stl", 0.01, true);
                                    count++;
                                    ws.DoWork();
                                    debug += "Error in opening boolean operation\n";
                                }
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
                    if (!debug.Contains("IfcElement error: " + element.KeyWord))
                        debug += "IfcElement error: " + element.KeyWord + "\n";
                }
            }

            debug += Conversion.Debug;

            Debug.WriteLine(debug);

            TreeViewManager.PopulateTree(modelTree, viewportLayout1.Entities.ToList(), viewportLayout1.Blocks);

            viewportLayout1.ZoomFit();
        }

        private Entity createOpenings (Entity eyeElement, IfcElement ifcElement)
        {
            //viewportLayout1.Entities.Add((Entity)eyeElement.Clone(), 1);

            if(eyeElement is BlockReference)
            {
                BlockReference brElement = (BlockReference)eyeElement;

                Block blockElement;

                viewportLayout1.Blocks.TryGetValue(brElement.BlockName, out blockElement);

                for(int i=0; i<blockElement.Entities.Count; i++)
                {
                    blockElement.Entities[i] = createOpenings(blockElement.Entities[i], ifcElement);
                }
                return eyeElement;
            }
            else if (eyeElement is Mesh)
            {
                Mesh m = (Mesh)eyeElement;

                Mesh[] splittedMesh;

                splittedMesh = m.SplitDisjoint();

                if (splittedMesh.Length > 1)
                    debug += "splittedMesh.length > 1\n";

                foreach (IfcRelVoidsElement relVE in ifcElement.HasOpenings)
                {
                    Entity openingEntity = Conversion.getEntityFromIfcProductRepresentation(relVE.RelatedOpeningElement.Representation, viewportLayout1);

                    viewportLayout1.Entities.Add((Entity)openingEntity.Clone(), 2);

                    if (openingEntity != null)
                    {
                        Transformation opTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)relVE.RelatedOpeningElement.Placement);

                        openingEntity.TransformBy(opTrs);

                        Solid openingSolid;

                        if (openingEntity is Mesh)
                            openingSolid = ((Mesh)openingEntity).ConvertToSolid();
                        else
                            openingSolid = (Solid)openingEntity;

                        for (int i = 0; i < splittedMesh.Length; i++)
                        {
                            //verificare collision? bound box?
                            Solid[] result;

                            Solid entitySolid = splittedMesh[i].ConvertToSolid();

                            //viewportLayout1.Entities.Add((Entity)openingSolid.Clone(), 1, Color.Green);

                            if (Utility.DoOverlap(entitySolid.BoxMin, entitySolid.BoxMax, openingSolid.BoxMin, openingSolid.BoxMax))
                            {
                                result = Solid.Difference(entitySolid, openingSolid, 0.001);
                                
                                if (result != null)
                                {
                                    splittedMesh[i] = result[0].ConvertToMesh();

                                    break;
                                }
                                else
                                {
                                    WriteSTL ws = new WriteSTL(new Entity[] { entitySolid, openingSolid }, new Layer[] { new Layer("Default") }, new Dictionary<string, Block>(), @"c:\devdept\booleanError\" + count + " " + ifcElement.GlobalId + ".stl", 0.01, true);
                                    count++;
                                    ws.DoWork();
                                    debug += "Error in opening boolean operation\n";
                                }
                            }
                        }
                    }
                }
                if (splittedMesh.Length > 1)
                {
                    Block b = new Block();

                    foreach (Mesh mesh in splittedMesh)
                    {
                        b.Entities.Add(mesh);
                    }
                    viewportLayout1.Blocks.Add(ifcElement.GlobalId, b);

                    eyeElement = new BlockReference(ifcElement.GlobalId);
                }
                else
                {
                    eyeElement = splittedMesh[0];
                }
            }
            else
            {
                Solid entitySolid = (Solid)eyeElement;

                foreach (IfcRelVoidsElement relVE in ifcElement.HasOpenings)
                {
                    Entity openingEntity = Conversion.getEntityFromIfcProductRepresentation(relVE.RelatedOpeningElement.Representation, viewportLayout1);

                    if (openingEntity != null)
                    {
                        Transformation opTrs = Conversion.getPlacementTransformtion((IfcLocalPlacement)relVE.RelatedOpeningElement.Placement);

                        openingEntity.TransformBy(opTrs);

                        Solid openingSolid;

                        if (openingEntity is Mesh)
                            openingSolid = ((Mesh)openingEntity).ConvertToSolid();
                        else
                            openingSolid = (Solid)openingEntity;

                        Solid[] result;

                        //viewportLayout1.Entities.Add((Entity)openingSolid.Clone(), 1, Color.Green);

                        result = Solid.Difference(entitySolid, openingSolid, 0.001);

                        if (result != null)
                        {
                            entitySolid = result[0];
                        }
                        else
                        {
                            WriteSTL ws = new WriteSTL(new Entity[] { entitySolid, openingSolid }, new Layer[] { new Layer("Default") }, new Dictionary<string, Block>(), @"c:\devdept\booleanError\" + count + " " + ifcElement.GlobalId + ".stl", 0.01, true);
                            count++;
                            ws.DoWork();
                            debug += "Error in opening boolean operation\n";
                        }
                    }
                }
                eyeElement = entitySolid;
            }
            return eyeElement;
        }

        private Color getColor()
        {
            Color color = Color.AliceBlue;
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

            return color;
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

