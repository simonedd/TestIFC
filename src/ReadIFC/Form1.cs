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
            
            //DatabaseIfc db = new DatabaseIfc("C:\\devdept\\IFC Model.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\MOD-Padrão\\MOD-Padrão.ifc");
            DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\Blueberry031105_Complete_optimized.ifc");
            //DatabaseIfc db = new DatabaseIfc("C:\\devDept\\IFC\\IFC Data\\c_rvt8_Townhouse.ifc");


            IfcProject project = db.Project;

            List<IfcBuildingElement> elements = project.Extract<IfcBuildingElement>();

            List<IfcSpatialElement> spElements = project.Extract<IfcSpatialElement>();
            
            //ci sono piu elementi uguali ( stesso mark )
            foreach (IfcBuildingElement element in elements)
            {
                if (handleElements.Contains(element.KeyWord) && element.Placement!= null && element.Representation != null)
                {   
                    trs = getPlacementTransformtion((IfcLocalPlacement)element.Placement);

                    ////Color

                    //element.MaterialSelect.PrimaryMaterial.HasRepresentation.Representations[0].Items[0].Styles[0]....

                    //fcPresentationStyleAssignment psa = (IfcPresentationStyleAssignment)extrAreaSolid.StyledByItem.Styles[0];
                    //IfcSurfaceStyle ss = (IfcSurfaceStyle)psa.Styles[0];
                    //IfcSurfaceStyleRendering ssr = (IfcSurfaceStyleRendering)ss.Styles[0];
                    //Color color = ssr.SurfaceColour.Colour;

                    IfcProductRepresentation prodRep = (IfcProductRepresentation)element.Representation;

                    Entity entity = getEntityFromIfcProductRepresentation(prodRep);

                    if(entity != null)
                    {
                        entity.EntityData = element.ObjectType + "|" + element.Name + "|" + element.GlobalId;

                        entity.TransformBy(trs);

                        viewportLayout1.Entities.Add(entity, 0, Color.Gray);
                    }
                }
                else
                {
                    if(!debug.Contains("IfcElement not supported: " + element.KeyWord))
                        debug += "IfcElement not supported: " + element.KeyWord + "\n";
                }
            }

            Debug.WriteLine(debug);

            //viewportLayout1.ActionMode = actionType.SelectVisibleByPick;

            TreeViewManager.PopulateTree(modelTree, viewportLayout1.Entities.ToList(), viewportLayout1.Blocks);

            //viewportLayout1.DisplayMode = displayType.Shaded;

            //viewportLayout1.Layers.TurnOff("testLayer");
            //viewportLayout1.Layers.TurnOff("Default");
            viewportLayout1.ZoomFit();
        }

        private Entity getEntityFromIfcProductRepresentation (IfcProductRepresentation prodRep)
        {
            List<Mesh> mList = new List<Mesh>();

            foreach (IfcRepresentation iRep in prodRep.Representations)
            {
                Mesh m = getMeshFromIfcRepresentationItem(iRep.Items[0]); // correggere se item.Count > 1

                if (m != null)
                    mList.Add(m);
            }

            if (mList.Count > 1)
            {
                Block b = new Block();

                foreach (Mesh m in mList)
                {
                    b.Entities.Add(m);
                }

                viewportLayout1.Blocks.Add(prodRep.Index.ToString(), b);

                return new BlockReference(prodRep.Index.ToString());
            }
            else if (mList.Count == 0)
                return null;

            return mList[0];
        }

        private Mesh getMeshFromIfcRepresentationItem (IfcRepresentationItem repItem)
        {
            Mesh m = null;
            if (repItem is IfcExtrudedAreaSolid)
            {
                IfcExtrudedAreaSolid extrAreaSolid = (IfcExtrudedAreaSolid)repItem;
                // if (!viewportLayout1.Blocks.ContainsKey(extrAreaSolid.Index.ToString()))
                {
                    Plane pln = ConvMet.getPlaneFromPosition(extrAreaSolid.Position);

                    Align3D align = new Align3D(Plane.XY, pln);

                    IfcDirection dir = extrAreaSolid.ExtrudedDirection;

                    Vector3D extDir = new Vector3D(dir.DirectionRatioX, dir.DirectionRatioY, dir.DirectionRatioZ);
                    
                    //extDir.TransformBy(trs * align2);
                    extDir.TransformBy(align);

                    devDept.Eyeshot.Entities.Region region = IfcProfileDefToRegion(extrAreaSolid.SweptArea);

                    if (region != null)
                    {
                        //region.TransformBy(trs * align2);
                        region.TransformBy(align);

                        m = region.ExtrudeAsMesh(extDir * extrAreaSolid.Depth, 0.1, Mesh.natureType.Plain); // 0.1 tolerance must be computed according to object size

                        //Block b = new Block();
                        //b.Entities.Add(m);
                        //viewportLayout1.Blocks.Add(extrAreaSolid.Index.ToString(), b);
                        
                    }
                }
                // BlockReference br = new BlockReference(trs, extrAreaSolid.Index.ToString());
                // viewportLayout1.Entities.Add(br, 0, Color.Gray);
            }
            else if (repItem is IfcFacetedBrep)  //controllare
            {
                IfcFacetedBrep facBrep = (IfcFacetedBrep)repItem;

                IfcClosedShell cs = facBrep.Outer;

                Mesh global = new Mesh(0, 0, Mesh.natureType.Plain);

                foreach (IfcFace face in cs.CfsFaces)
                {
                    foreach (IfcFaceBound fb in face.Bounds)
                    {
                        // if fb is IfcFaceOuterBound ??
                        IfcPolyloop pl = (IfcPolyloop)fb.Bound;

                        Point3D[] points = new Point3D[pl.Polygon.Count + 1];

                        for (int i = 0; i < pl.Polygon.Count; i++)
                        {
                            points[i] = new Point3D(pl.Polygon[i].Coordinates.Item1, pl.Polygon[i].Coordinates.Item2, pl.Polygon[i].Coordinates.Item3);
                        }

                        points[points.Length - 1] = (Point3D)points[0].Clone();

                        Plane fit = Utility.FitPlane(points); // puo' venire in un verso o nell'altro (verificare la soluzione, del piano con 3 punti: primo origine, secondo asse X, penultimo asse Y)

                        Align3D al = new Align3D(fit, Plane.XY);

                        Point3D[] onPlane = new Point3D[points.Length];

                        for (int i = 0; i < points.Length; i++)
                        {
                            onPlane[i] = al * points[i];
                        }

                        // LinearPath lp = new LinearPath(points);

                        Mesh local = UtilityEx.Triangulate(onPlane);
                        if (local != null)
                        {
                            Mesh m3d = new Mesh(points, local.Triangles);

                            global.MergeWith(m3d, true); // fonde i vertici, sarebbe meglio farla una volta sola alla fine
                        }
                        // devDept.Eyeshot.Entities.Region region = new devDept.Eyeshot.Entities.Region(lp);

                        // region.TransformBy(trs);

                        // viewportLayout1.Entities.Add(region, 0, Color.Yellow);
                    }
                }
                m = global;
            }
            else if (repItem is IfcBoundingBox)
            {
                //IfcBoundingBox bBox = (IfcBoundingBox)iRep.Items[0];
                //m = Mesh.CreateBox(bBox.XDim, bBox.YDim, bBox.ZDim);
                //m.Translate(bBox.Corner.Coordinates.Item1, bBox.Corner.Coordinates.Item1, bBox.Corner.Coordinates.Item1);
            }
            else if (repItem is IfcBooleanClippingResult)
            {
                IfcBooleanClippingResult bcr = (IfcBooleanClippingResult)repItem;
                Solid s = IfcBooleanClippingResultToSolid(bcr);
                if (s != null)
                    m = s.ConvertToMesh();
            }
            else
            {
                if (!debug.Contains("IfcRepresentationItem not supported: " + repItem.KeyWord))
                    debug += "IfcRepresentationItem not supported: " + repItem.KeyWord + "\n";
            }
            return m;
        }

        private Solid IfcBooleanClippingResultToSolid(IfcBooleanClippingResult bcr)
        {
            Solid op1 = null, op2 = null;
            Mesh m;

            if (bcr.FirstOperand is IfcBooleanClippingResult)
            {
                op1 = IfcBooleanClippingResultToSolid((IfcBooleanClippingResult)bcr.FirstOperand);
            }
            else
            {
                m = getMeshFromIfcRepresentationItem((IfcRepresentationItem)bcr.FirstOperand);

                if (m != null)
                    op1 = m.ConvertToSolid();
            }

            //m = getMeshFromIfcRepresentationItem((IfcRepresentationItem)bcr.SecondOperand);

            //if (m != null)
            //    op2 = m.ConvertToSolid();

            if (bcr.SecondOperand is IfcPolygonalBoundedHalfSpace)
            {
                IfcPolygonalBoundedHalfSpace polB = (IfcPolygonalBoundedHalfSpace)bcr.SecondOperand;

                Plane pln = ConvMet.getPlaneFromPosition(polB.Position);

                Align3D align = new Align3D(Plane.XY, pln);

                IfcPolyline p = (IfcPolyline)polB.PolygonalBoundary;

                Point3D[] points = new Point3D[p.Points.Count];
                for (int i = 0; i < p.Points.Count; i++)
                {
                    points[i] = new Point3D(p.Points[i].Coordinates.Item1, p.Points[i].Coordinates.Item2, p.Points[i].Coordinates.Item3);
                }
                LinearPath lp = new LinearPath(points);

                devDept.Eyeshot.Entities.Region region = new devDept.Eyeshot.Entities.Region(lp);

                region.TransformBy(align);

                //verificare extrud direction

                op2 = region.ExtrudeAsSolid(Plane.XY.AxisZ * 20, 0.1); // 0.1 tolerance must be computed according to object size
            }
            else if (bcr.SecondOperand is IfcHalfSpaceSolid)
            {
                IfcHalfSpaceSolid hs = (IfcHalfSpaceSolid)bcr.SecondOperand;

                IfcPlane ip = (IfcPlane)hs.BaseSurface;

                Plane pln = ConvMet.getPlaneFromPosition(ip.Position);

                op1.CutBy(pln);

                return op1;
            }

            if (op1 == null || op2 == null)
                return null;

            //op1.TransformBy(trs);
            //op2.TransformBy(trs);

            //viewportLayout1.Entities.Add(op1, testLayer, Color.Red);
            //viewportLayout1.Entities.Add(op2, testLayer, Color.Blue);
            //return null;


            Solid[] result;

            switch (bcr.Operator)
            {
                case IfcBooleanOperator.DIFFERENCE:
                    result = Solid.Difference(op1, op2);
                    break;

                case IfcBooleanOperator.UNION:
                    result = Solid.Union(op1, op2);
                    break;

                case IfcBooleanOperator.INTERSECTION:
                    result = Solid.Intersection(op1, op2);
                    break;

                default:
                    return null;
            }

            if (result != null)
                return result[0];

            return null;
        }

        private devDept.Eyeshot.Entities.Region IfcProfileDefToRegion (IfcProfileDef ipd)
        {
            devDept.Eyeshot.Entities.Region region = null;

            if (ipd is IfcIShapeProfileDef) // IfcIShapeProfileDef and all derived from
            {
                IfcIShapeProfileDef shProfDef = (IfcIShapeProfileDef)ipd;
                double halfWidth = shProfDef.OverallWidth / 2;
                double halfDepth = shProfDef.OverallDepth / 2;
                LinearPath lp = new LinearPath(Plane.XY,
                    new Point2D(-halfWidth, -halfDepth),
                    new Point2D(halfWidth, -halfDepth),
                    new Point2D(halfWidth, -halfDepth + shProfDef.FlangeThickness),
                    new Point2D(shProfDef.WebThickness / 2, -halfDepth + shProfDef.FlangeThickness),
                    new Point2D(shProfDef.WebThickness / 2, +halfDepth - shProfDef.FlangeThickness),
                    new Point2D(halfWidth, +halfDepth - shProfDef.FlangeThickness),
                    new Point2D(halfWidth, halfDepth),
                    new Point2D(-halfWidth, halfDepth),
                    new Point2D(-halfWidth, halfDepth - shProfDef.FlangeThickness),
                    new Point2D(-shProfDef.WebThickness / 2, halfDepth - shProfDef.FlangeThickness),
                    new Point2D(-shProfDef.WebThickness / 2, -halfDepth + shProfDef.FlangeThickness),
                    new Point2D(-halfWidth, -halfDepth + shProfDef.FlangeThickness),
                    new Point2D(-halfWidth, -halfDepth)
                );

                region = new devDept.Eyeshot.Entities.Region(lp);
            }
            else if (ipd is IfcRectangleProfileDef)
            {
                IfcRectangleProfileDef recProfDef = (IfcRectangleProfileDef)ipd;
                region = new RectangularRegion(recProfDef.XDim, recProfDef.YDim, true);
            }
            else if (ipd is IfcArbitraryClosedProfileDef)
            {
                IfcArbitraryClosedProfileDef arProfDef = (IfcArbitraryClosedProfileDef)ipd;

                IfcPolyline p = (IfcPolyline)arProfDef.OuterCurve;

                Point3D[] points = new Point3D[p.Points.Count];
                for (int i = 0; i < p.Points.Count; i++)
                {
                    points[i] = new Point3D(p.Points[i].Coordinates.Item1, p.Points[i].Coordinates.Item2, p.Points[i].Coordinates.Item3);
                }
                LinearPath lp = new LinearPath(points);
                
                region = new devDept.Eyeshot.Entities.Region(lp);
            }
            else
            {
                if (!debug.Contains("IfcProfileDef not supported: " + ipd.KeyWord))
                    debug += "IfcProfileDef not supported: " + ipd.KeyWord + "\n";
            }
            return region;
        }

        private Transformation getPlacementTransformtion(IfcLocalPlacement ilp)
        {
            IfcAxis2Placement3D rp = (IfcAxis2Placement3D)ilp.RelativePlacement;

            Plane pln = ConvMet.getPlaneFromPosition(rp);

            Align3D align = new Align3D(Plane.XY, pln);

            if (ilp.PlacementRelTo == null)
                return align;
            else
                return getPlacementTransformtion( (IfcLocalPlacement)ilp.PlacementRelTo ) * align;
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

