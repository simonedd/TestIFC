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
    public static class Conversion
    {
        private static int count = 0;

        private static string debug = String.Empty;

        public static Transformation getPlacementTransformtion(IfcLocalPlacement ilp)
        {
            IfcAxis2Placement3D rp = (IfcAxis2Placement3D)ilp.RelativePlacement;

            Plane pln = Conversion.getPlaneFromPosition(rp);

            Align3D align = new Align3D(Plane.XY, pln);

            if (ilp.PlacementRelTo == null)
                return align;
            else
                return  getPlacementTransformtion((IfcLocalPlacement)ilp.PlacementRelTo) * align;
        }

        public static Plane getPlaneFromPosition(IfcAxis2Placement3D pos)
        {
            Point3D org = new Point3D(pos.Location.Coordinates.Item1, pos.Location.Coordinates.Item2, pos.Location.Coordinates.Item3);

            Vector3D xAxis;

            if (pos.RefDirection != null)
                xAxis = new Vector3D(pos.RefDirection.DirectionRatioX, pos.RefDirection.DirectionRatioY, pos.RefDirection.DirectionRatioZ);
            else
                xAxis = new Vector3D(1, 0, 0);

            Vector3D zAxis;

            if (pos.Axis != null)
                zAxis = new Vector3D(pos.Axis.DirectionRatioX, pos.Axis.DirectionRatioY, pos.Axis.DirectionRatioZ);
            else
                zAxis = new Vector3D(0, 0, 1);

            if (xAxis.IsZero)    //rivedere
                xAxis = new Vector3D(1, 0, 0);

            return new Plane(org, xAxis, Vector3D.Cross(zAxis, xAxis));
        }

        public static Entity getEntityFromIfcProductRepresentation(IfcProductRepresentation prodRep, ViewportLayout viewportLayout1)
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

        public static Mesh getMeshFromIfcRepresentationItem(IfcRepresentationItem repItem)
        {
            Mesh m = null;
            if (repItem is IfcExtrudedAreaSolid)
            {
                IfcExtrudedAreaSolid extrAreaSolid = (IfcExtrudedAreaSolid)repItem;
                // if (!viewportLayout1.Blocks.ContainsKey(extrAreaSolid.Index.ToString()))
                {
                    Plane pln = Conversion.getPlaneFromPosition(extrAreaSolid.Position);

                    Align3D align = new Align3D(Plane.XY, pln);

                    IfcDirection dir = extrAreaSolid.ExtrudedDirection;

                    Vector3D extDir = new Vector3D(dir.DirectionRatioX, dir.DirectionRatioY, dir.DirectionRatioZ);

                    //extDir.TransformBy(trs * align2);
                    extDir.TransformBy(align);

                    devDept.Eyeshot.Entities.Region region = getRegionFromIfcProfileDef(extrAreaSolid.SweptArea);

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
                Solid s = getSolidFromIfcBooleanClippingResult(bcr);
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

        private static Solid getSolidFromIfcBooleanClippingResult(IfcBooleanClippingResult bcr)
        {
            Solid op1 = null, op2 = null;
            Mesh m;

            if (bcr.FirstOperand is IfcBooleanClippingResult)
            {
                op1 = getSolidFromIfcBooleanClippingResult((IfcBooleanClippingResult)bcr.FirstOperand);
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
            Triangle t1 = null, t2 =null;
            if (bcr.SecondOperand is IfcPolygonalBoundedHalfSpace)
            {
                IfcPolygonalBoundedHalfSpace polB = (IfcPolygonalBoundedHalfSpace)bcr.SecondOperand;

                Plane pln = Conversion.getPlaneFromPosition(polB.Position);

                PlanarEntity pe = new PlanarEntity(pln, 100);

                pe.Regen(0);

                 t1 = new Triangle(pe.Vertices[0], pe.Vertices[1], pe.Vertices[2]);
                 t2 = new Triangle(pe.Vertices[0], pe.Vertices[2], pe.Vertices[3]);

                //  Align3D align = new Align3D(Plane.XY, pln);

                IfcPolyline p = (IfcPolyline)polB.PolygonalBoundary;

                Point3D[] points = new Point3D[p.Points.Count];
                for (int i = 0; i < p.Points.Count; i++)
                {
                    points[i] = new Point3D(p.Points[i].Coordinates.Item1, p.Points[i].Coordinates.Item2, p.Points[i].Coordinates.Item3);
                }
                LinearPath lp = new LinearPath(points);

                devDept.Eyeshot.Entities.Region region = new devDept.Eyeshot.Entities.Region(lp);

               // region.TransformBy(align);

                Vector3D extDir = Plane.XY.AxisZ;

                if (!polB.AgreementFlag)
                    extDir.Negate();

                op2 = region.ExtrudeAsSolid(extDir * 10, 0.1); // 0.1 tolerance must be computed according to object size

                pln.Flip();

                op2.CutBy(pln);                
            }
            else if (bcr.SecondOperand is IfcHalfSpaceSolid)
            {
                IfcHalfSpaceSolid hs = (IfcHalfSpaceSolid)bcr.SecondOperand;

                IfcPlane ip = (IfcPlane)hs.BaseSurface;

                Plane pln = Conversion.getPlaneFromPosition(ip.Position);
                
                //verificare flag agreement

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

            if (!op1.IsClosed || !op2.IsClosed)
            {
                throw new Exception("brrr");
            }

            Solid[] result;
           // op1 = Solid.CreateCylinder(6, 20, 16);
          //  op2 = Solid.CreateBox(10, 20, 30);

            switch (bcr.Operator)
            {
                case IfcBooleanOperator.DIFFERENCE:
                    result = Solid.Difference(op1, op2);    //su dll nuova e' possibile inserire parametro di tolleranza
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
            {
                return result[0];
            }
            else
            {
                WriteSTL ws = new WriteSTL(new Entity[] { op1, op2 }, new Layer[] { new Layer("Default") }, new Dictionary<string, Block>(), @"c:\devdept\booleanError\gino"+count+".stl", 0.01, true);
                
                count++;
                ws.DoWork();
                debug += "Error in boolean operation\n";
            }

            return null;
        }

        public static devDept.Eyeshot.Entities.Region getRegionFromIfcProfileDef(IfcProfileDef ipd)
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
            if (ipd is IfcParameterizedProfileDef)
            {
                IfcParameterizedProfileDef parProfDef = (IfcParameterizedProfileDef)ipd;
                if (parProfDef.Position != null)
                    //non considere se in position cambiano assi X e Y
                    region.Translate(parProfDef.Position.Location.Coordinates.Item1, parProfDef.Position.Location.Coordinates.Item2, parProfDef.Position.Location.Coordinates.Item3);
            }
            return region;
        }

        public static string Debug { get { return debug; } }

    }
}
