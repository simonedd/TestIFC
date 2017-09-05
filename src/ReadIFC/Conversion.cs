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
using devDept.Eyeshot.Triangulation;

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
            Point3D org = getPoint3DFromIfcCartesianPoint(pos.Location);

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

        public static Entity getEntityFromIfcProductRepresentation(IfcProductRepresentation prodRep, ViewportLayout viewportLayout1, Transformation entityTrs = null)
        {
            List<Entity> entityList = new List<Entity>();

            foreach (IfcRepresentation iRep in prodRep.Representations)
            {
                if (iRep.RepresentationIdentifier.Equals("Body"))
                {
                    Entity entity = getEntityFromIfcRepresentationItem(iRep.Items[0], viewportLayout1, entityTrs);      // correggere se item.Count > 1

                    if (entity != null)
                        entityList.Add(entity);
                }
                else
                {
                    if (!debug.Contains("IfcRepresentation.RepresentationIdentifier not supported: " + iRep.RepresentationIdentifier))
                        debug += "IfcRepresentation.RepresentationIdentifier not supported: " + iRep.RepresentationIdentifier + "\n";
                }

            }

            if (entityList.Count > 1)
            {
                Block b = new Block();

                foreach (Entity e in entityList)
                {
                    b.Entities.Add(e);
                }

                viewportLayout1.Blocks.Add(prodRep.Index.ToString(), b);

                return new BlockReference(prodRep.Index.ToString());
            }
            else if (entityList.Count == 0)
                return null;

            return entityList[0];
        }

        public static Entity getEntityFromIfcRepresentationItem(IfcRepresentationItem reprItem, ViewportLayout viewportLayout1 = null, Transformation entityTrs = null)
        {
            Entity result = null;

            if (reprItem is IfcBooleanClippingResult)
            {
                IfcBooleanClippingResult bcr = (IfcBooleanClippingResult)reprItem;

                result = getSolidFromIfcBooleanClippingResult(bcr);
            }
            else if(reprItem is IfcCurve)
            {
                result = getCompositeCurveFromIfcCurve((IfcCurve)reprItem);
            }
            else if (reprItem is IfcExtrudedAreaSolid)
            {
                IfcExtrudedAreaSolid extrAreaSolid = (IfcExtrudedAreaSolid)reprItem;

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
                        
                        result = region.ExtrudeAsMesh(extDir * extrAreaSolid.Depth, 0.1, Mesh.natureType.Plain); // 0.1 tolerance must be computed according to object size

                        //viewportLayout1.Entities.Add(result, 1);
                        //Block b = new Block();
                        //b.Entities.Add(m);
                        //viewportLayout1.Blocks.Add(extrAreaSolid.Index.ToString(), b);

                    }
                }
                // BlockReference br = new BlockReference(trs, extrAreaSolid.Index.ToString());
                // viewportLayout1.Entities.Add(br, 0, Color.Gray);
            }
            else if (reprItem is IfcFaceBasedSurfaceModel)
            {
                IfcFaceBasedSurfaceModel fbs = (IfcFaceBasedSurfaceModel)reprItem;

                result = new Mesh(0, 0, Mesh.natureType.Plain);

                foreach (IfcConnectedFaceSet cfs in fbs.FbsmFaces)
                {
                    Mesh global = new Mesh(0, 0, Mesh.natureType.Plain);

                    foreach (IfcFace face in cfs.CfsFaces)           
                    {
                        Point3D[] outerPoints = null;

                        List<Point3D[]> innerPointsList = new List<Point3D[]>();

                        foreach (IfcFaceBound fb in face.Bounds)        // al massimo 2 ? profilo esterno e interno
                        {
                            // bool sense = ifb.mOrientation;

                            if (fb is IfcFaceOuterBound)
                            {
                                IfcFaceOuterBound ifob = (IfcFaceOuterBound)fb;

                                IfcPolyloop pl = (IfcPolyloop)fb.Bound;

                                List<Point3D> pLIst = new List<Point3D>();

                                for (int i = 0; i < pl.Polygon.Count; i++)
                                {
                                    Point3D p = getPoint3DFromIfcCartesianPoint(pl.Polygon[i]);

                                    if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                        pLIst.Add(p);
                                }

                                outerPoints = pLIst.ToArray();

                                if (!outerPoints[0].Equals(outerPoints[outerPoints.Length - 1]))
                                {
                                    Array.Resize(ref outerPoints, outerPoints.Length + 1);

                                    outerPoints[outerPoints.Length - 1] = (Point3D)outerPoints[0].Clone();
                                }
                                Array.Reverse(outerPoints);
                            }
                            else
                            {
                                IfcFaceBound ifb = (IfcFaceBound)fb;

                                IfcPolyloop inPl = (IfcPolyloop)ifb.Bound;

                                List<Point3D> pLIst = new List<Point3D>();

                                for (int i = 0; i < inPl.Polygon.Count; i++)
                                {
                                    Point3D p = getPoint3DFromIfcCartesianPoint(inPl.Polygon[i]);

                                    if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                        pLIst.Add(p);
                                }

                                Point3D[] innerPoints = pLIst.ToArray();

                                if (!innerPoints[0].Equals(innerPoints[innerPoints.Length - 1]))
                                {
                                    Array.Resize(ref innerPoints, innerPoints.Length + 1);

                                    innerPoints[innerPoints.Length - 1] = (Point3D)innerPoints[0].Clone();
                                }
                                Array.Reverse(innerPoints);

                                innerPointsList.Add(innerPoints);
                            }
                        }
                        if (outerPoints.Length > 3)
                        {
                            Mesh local;

                            List<LinearPath> boundLp = new List<LinearPath>();

                            boundLp.Add(new LinearPath(outerPoints));

                            foreach (Point3D[] innerPoints in innerPointsList)
                            {
                                boundLp.Add(new LinearPath(innerPoints));
                            }

                            local = new devDept.Eyeshot.Entities.Region(boundLp.ToArray()).ConvertToMesh(0, Mesh.natureType.Plain);

                            global.MergeWith(local, true); // fonde i vertici, sarebbe meglio farla una volta sola alla fine
                        }
                    }
                    ((Mesh)result).MergeWith(global, true);
                }
            }
            else if (reprItem is IfcFacetedBrep)  //controllare
            {
                IfcFacetedBrep facBrep = (IfcFacetedBrep)reprItem;

                IfcClosedShell cs = facBrep.Outer;

                Mesh global = new Mesh(0, 0, Mesh.natureType.Plain);

                foreach (IfcFace face in cs.CfsFaces)
                {
                    Point3D[] outerPoints = null;

                    List<Point3D[]> innerPointsList = new List<Point3D[]>();

                    foreach (IfcFaceBound fb in face.Bounds)
                    {
                        // bool sense = ifb.mOrientation;

                        if (fb is IfcFaceOuterBound)
                        {
                            IfcFaceOuterBound ifob = (IfcFaceOuterBound)fb;

                            IfcPolyloop pl = (IfcPolyloop)fb.Bound;

                            List<Point3D> pLIst = new List<Point3D>();

                            for (int i = 0; i < pl.Polygon.Count; i++)
                            {
                                Point3D p = getPoint3DFromIfcCartesianPoint(pl.Polygon[i]);

                                if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                    pLIst.Add(p);
                            }

                            outerPoints = pLIst.ToArray();

                            if (!outerPoints[0].Equals(outerPoints[outerPoints.Length - 1]))
                            {
                                Array.Resize(ref outerPoints, outerPoints.Length + 1);

                                outerPoints[outerPoints.Length - 1] = (Point3D)outerPoints[0].Clone();
                            }
                            Array.Reverse(outerPoints);
                        }
                        else
                        {
                            IfcFaceBound ifb = (IfcFaceBound)fb;

                            IfcPolyloop inPl = (IfcPolyloop)ifb.Bound;

                            List<Point3D> pLIst = new List<Point3D>();

                            for (int i = 0; i < inPl.Polygon.Count; i++)
                            {
                                Point3D p = getPoint3DFromIfcCartesianPoint(inPl.Polygon[i]);

                                if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                    pLIst.Add(p);
                            }

                            Point3D[] innerPoints = pLIst.ToArray();

                            if (!innerPoints[0].Equals(innerPoints[innerPoints.Length - 1]))
                            {
                                Array.Resize(ref innerPoints, innerPoints.Length + 1);

                                innerPoints[innerPoints.Length - 1] = (Point3D)innerPoints[0].Clone();
                            }
                            Array.Reverse(innerPoints);

                            innerPointsList.Add(innerPoints);
                        }
                    }
                    if (outerPoints.Length > 3)
                    {
                        Mesh local;

                        List<LinearPath> boundLp = new List<LinearPath>();

                        boundLp.Add(new LinearPath(outerPoints));

                        foreach (Point3D[] innerPoints in innerPointsList)
                        {
                            boundLp.Add(new LinearPath(innerPoints));
                        }
                        local = new devDept.Eyeshot.Entities.Region(boundLp.ToArray()).ConvertToMesh(0, Mesh.natureType.Plain);

                        global.MergeWith(local, true); // fonde i vertici, sarebbe meglio farla una volta sola alla fine
                    }
                }

                result = global;
            }
            //else if (repItem is IfcBoundingBox)
            //{
            //    IfcBoundingBox bBox = (IfcBoundingBox)iRep.Items[0];
            //    m = Mesh.CreateBox(bBox.XDim, bBox.YDim, bBox.ZDim);
            //    m.Translate(bBox.Corner.Coordinates.Item1, bBox.Corner.Coordinates.Item1, bBox.Corner.Coordinates.Item1);
            //}
            else if (reprItem is IfcMappedItem)
            {
                IfcMappedItem mapItem = (IfcMappedItem)reprItem;

                if (!viewportLayout1.Blocks.ContainsKey(mapItem.MappingSource.Index.ToString()))
                {
                    IfcRepresentationMap reprMapSource = mapItem.MappingSource;

                    Entity mapSource = getEntityFromIfcRepresentationItem(reprMapSource.MappedRepresentation.Items[0], viewportLayout1, entityTrs);

                    Block b = new Block();

                    if (mapSource != null)
                    {
                        Plane pln = getPlaneFromPosition((IfcAxis2Placement3D)reprMapSource.MappingOrigin);

                        Align3D algn = new Align3D(Plane.XY, pln);

                        mapSource.TransformBy(algn);

                        b.Entities.Add(mapSource);
                    }

                    viewportLayout1.Blocks.Add(mapItem.MappingSource.Index.ToString(), b);
                }

                IfcCartesianTransformationOperator3D iTrs = (IfcCartesianTransformationOperator3D)mapItem.MappingTarget;

                Point3D org = new Point3D(iTrs.LocalOrigin.Coordinates.Item1, iTrs.LocalOrigin.Coordinates.Item2, iTrs.LocalOrigin.Coordinates.Item3);

                Vector3D vectorX;

                if (iTrs.Axis1 != null)
                    vectorX = new Vector3D(iTrs.Axis1.DirectionRatioX, iTrs.Axis1.DirectionRatioY, iTrs.Axis1.DirectionRatioZ);
                else
                    vectorX = new Vector3D(1, 0, 0);
                vectorX = vectorX * iTrs.Scale;

                Vector3D vectorY;

                if (iTrs.Axis2 != null)
                    vectorY = new Vector3D(iTrs.Axis2.DirectionRatioX, iTrs.Axis2.DirectionRatioY, iTrs.Axis2.DirectionRatioZ);
                else
                    vectorY = new Vector3D(0, 1, 0);

                Vector3D vectorZ;

                if (iTrs.Axis1 != null)
                    vectorZ = new Vector3D(iTrs.Axis3.DirectionRatioX, iTrs.Axis3.DirectionRatioY, iTrs.Axis3.DirectionRatioZ);
                else
                    vectorZ = new Vector3D(0, 0, 1);

                if (iTrs is IfcCartesianTransformationOperator3DnonUniform)
                {
                    IfcCartesianTransformationOperator3DnonUniform nut = (IfcCartesianTransformationOperator3DnonUniform)iTrs;

                    vectorY = vectorY * nut.Scale2;

                    vectorZ = vectorZ * nut.Scale3;
                }


                Transformation targetTrs = new Transformation(org, vectorX, vectorY, vectorZ);

                result = new BlockReference(targetTrs, mapItem.MappingSource.Index.ToString());

            }
            else if (reprItem is IfcShellBasedSurfaceModel)
            {
                IfcShellBasedSurfaceModel sbs = (IfcShellBasedSurfaceModel)reprItem;

                result = new Mesh(0, 0, Mesh.natureType.Plain);

                foreach (IfcShell cfs in sbs.SbsmBoundary)
                {
                    Mesh global = new Mesh(0, 0, Mesh.natureType.Plain);

                    foreach (IfcFace face in cfs.CfsFaces)
                    {
                        Point3D[] outerPoints = null;

                        List<Point3D[]> innerPointsList = new List<Point3D[]>();

                        foreach (IfcFaceBound fb in face.Bounds)        // al massimo 2 ? profilo esterno e interno
                        {
                            // bool sense = ifb.mOrientation;

                            if (fb is IfcFaceOuterBound)
                            {
                                IfcFaceOuterBound ifob = (IfcFaceOuterBound)fb;

                                IfcPolyloop pl = (IfcPolyloop)fb.Bound;

                                List<Point3D> pLIst = new List<Point3D>();

                                for (int i = 0; i < pl.Polygon.Count; i++)
                                {
                                    Point3D p = getPoint3DFromIfcCartesianPoint(pl.Polygon[i]);

                                    if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                        pLIst.Add(p);
                                }

                                outerPoints = pLIst.ToArray();

                                if (!outerPoints[0].Equals(outerPoints[outerPoints.Length - 1]))
                                {
                                    Array.Resize(ref outerPoints, outerPoints.Length + 1);

                                    outerPoints[outerPoints.Length - 1] = (Point3D)outerPoints[0].Clone();
                                }
                                Array.Reverse(outerPoints);
                            }
                            else
                            {
                                IfcFaceBound ifb = (IfcFaceBound)fb;

                                IfcPolyloop inPl = (IfcPolyloop)ifb.Bound;

                                List<Point3D> pLIst = new List<Point3D>();

                                for (int i = 0; i < inPl.Polygon.Count; i++)
                                {
                                    Point3D p = getPoint3DFromIfcCartesianPoint(inPl.Polygon[i]);

                                    if (!pLIst.Contains(p))                     // non copio punti uguali !!
                                        pLIst.Add(p);
                                }

                                Point3D[] innerPoints = pLIst.ToArray();

                                if (!innerPoints[0].Equals(innerPoints[innerPoints.Length - 1]))
                                {
                                    Array.Resize(ref innerPoints, innerPoints.Length + 1);

                                    innerPoints[innerPoints.Length - 1] = (Point3D)innerPoints[0].Clone();
                                }
                                Array.Reverse(innerPoints);

                                innerPointsList.Add(innerPoints);
                            }
                        }
                        if (outerPoints.Length > 3)
                        {
                            Mesh local;

                            List<LinearPath> boundLp = new List<LinearPath>();

                            boundLp.Add(new LinearPath(outerPoints));

                            foreach (Point3D[] innerPoints in innerPointsList)
                            {
                                boundLp.Add(new LinearPath(innerPoints));
                            }

                            //devDept.Eyeshot.Entities.Region localRegion = new devDept.Eyeshot.Entities.Region(boundLp.ToArray());

                            //localRegion.TransformBy(entityTrs);

                            //viewportLayout1.Entities.Add(localRegion, 1);



                            local = new devDept.Eyeshot.Entities.Region(boundLp.ToArray()).ConvertToMesh(0, Mesh.natureType.Plain);



                            global.MergeWith(local, true); // fonde i vertici, sarebbe meglio farla una volta sola alla fine
                        }
                    }
                    ((Mesh)result).MergeWith(global, true);
                }

            } else
            {
                if (!debug.Contains("IfcRepresentationItem not supported: " + reprItem.KeyWord))
                    debug += "IfcRepresentationItem not supported: " + reprItem.KeyWord + "\n";
            }
            //if (result != null)
            //{
            //    result.Color = getColorFromIfcRepresentationItem(reprItem);
                
            //    result.ColorMethod = colorMethodType.byEntity;
            //}

            return result;
        }

        private static CompositeCurve getCompositeCurveFromIfcCurve( IfcCurve ifcCurve)
        {
            CompositeCurve result = null;

            if (ifcCurve is IfcCircle)
            {
                IfcCircle c = (IfcCircle)ifcCurve;

                IfcAxis2Placement2D center = (IfcAxis2Placement2D)c.Position;                

                Circle circle = new Circle(getPoint3DFromIfcCartesianPoint(center.Location), c.Radius);

                result = new CompositeCurve(circle);
            }
            else if (ifcCurve is IfcPolyline)
            {
                IfcPolyline p = (IfcPolyline)ifcCurve;

                Point3D[] points = new Point3D[p.Points.Count];

                for (int i = 0; i < p.Points.Count; i++)
                {
                    points[i] = getPoint3DFromIfcCartesianPoint(p.Points[i]);
                }
                LinearPath lp = new LinearPath(points);

                result = new CompositeCurve(lp);
            }
            else if( ifcCurve is IfcCompositeCurve)             // verificare sense e transition
            {
                IfcCompositeCurve cc = (IfcCompositeCurve)ifcCurve;

                result = new CompositeCurve();

                foreach( IfcCompositeCurveSegment ccSegment in cc.Segments)
                {
                    CompositeCurve segment = getCompositeCurveFromIfcCurve(ccSegment.ParentCurve);

                    if (segment != null)
                    {
                        result.CurveList.Add(segment);
                    }
                    else
                    {
                        result = null;
                        break;
                    }
                }
            }
            else if (ifcCurve is IfcTrimmedCurve)
            {
                IfcTrimmedCurve tc = (IfcTrimmedCurve)ifcCurve;

                CompositeCurve cc = getCompositeCurveFromIfcCurve(tc.BasisCurve);

                if (cc != null)
                {
                    //if (cc.CurveList[0] is Circle)
                    //{
                    //    Circle circle = (Circle)cc.CurveList[0];
                    //    if (tc.MasterRepresentation == IfcTrimmingPreference.CARTESIAN)
                    //    {
                    //        ICurve curve;

                    //        IfcTrimmingSelect ip1 = tc.Trim1;



                    //        Point3D[] p1 = getPoint3DFromIfcCartesianPoint(tc.Trim1.IfcCartesianPoint);

                    //        //circle.SubCurve(tc.Trim1.IfcCartesianPoint, tc.Trim2.IfcCartesianPoint, curve);
                    //    }

                    //    result = cc;
                    //}
                    //else
                    {
                        if (!debug.Contains("IfcTrimmedCurve.BasisCurve not supported: " + tc.BasisCurve.KeyWord))
                            debug += "IfcTrimmedCurve.BasisCurve not supported: " + tc.BasisCurve.KeyWord + "\n";
                    }
                }
            }
            else
            {
                if (!debug.Contains("IfcCurve not supported: " + ifcCurve.KeyWord))
                    debug += "IfcCurve not supported: " + ifcCurve.KeyWord + "\n";
            }
            return result;
        }

        private static Solid getSolidFromIfcBooleanClippingResult(IfcBooleanClippingResult bcr)
        {
            Solid op1 = null, op2 = null;

            Entity e;

            if (bcr.FirstOperand is IfcBooleanClippingResult)
            {
                op1 = getSolidFromIfcBooleanClippingResult((IfcBooleanClippingResult)bcr.FirstOperand);
            }
            else
            {
                e = getEntityFromIfcRepresentationItem((IfcRepresentationItem)bcr.FirstOperand);

                if (e != null)// && (e is Solid || e is Mesh))
                {
                    if (e is Mesh)
                        op1 = ((Mesh)e).ConvertToSolid();
                    else
                        op1 = (Solid)e;
                }
            }

            //m = getMeshFromIfcRepresentationItem((IfcRepresentationItem)bcr.SecondOperand);

            //if (m != null)
            //    op2 = m.ConvertToSolid();
            
            if (bcr.SecondOperand is IfcPolygonalBoundedHalfSpace)
            {
                IfcPolygonalBoundedHalfSpace polB = (IfcPolygonalBoundedHalfSpace)bcr.SecondOperand;

                IfcPlane baseSurface = (IfcPlane)polB.BaseSurface;

                Plane cutPln = Conversion.getPlaneFromPosition(baseSurface.Position);

                Plane boundaryPlane = Conversion.getPlaneFromPosition(polB.Position);
                
                Align3D align = new Align3D(Plane.XY, boundaryPlane);

                IfcPolyline p = (IfcPolyline)polB.PolygonalBoundary;

                Point3D[] points = new Point3D[p.Points.Count];

                for (int i = 0; i < p.Points.Count; i++)
                {
                    points[i] = new Point3D(p.Points[i].Coordinates.Item1, p.Points[i].Coordinates.Item2, p.Points[i].Coordinates.Item3);
                }
                LinearPath lp = new LinearPath(points);

                devDept.Eyeshot.Entities.Region region = new devDept.Eyeshot.Entities.Region(lp);

                region.TransformBy(align);

                Vector3D extDir = boundaryPlane.AxisZ;

                op2 = region.ExtrudeAsSolid(extDir * 20, 0.1); // 0.1 tolerance must be computed according to object size

                if (!polB.AgreementFlag)
                    cutPln.Flip();

                op2.CutBy(cutPln);                
            }
            else if (bcr.SecondOperand is IfcHalfSpaceSolid)
            {
                IfcHalfSpaceSolid hs = (IfcHalfSpaceSolid)bcr.SecondOperand;

                IfcPlane ip = (IfcPlane)hs.BaseSurface;

                Plane pln = Conversion.getPlaneFromPosition(ip.Position);

                if (!hs.AgreementFlag)
                    pln.Flip();

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

            double tolerance = 0.01;

            switch (bcr.Operator)
            {
                case IfcBooleanOperator.DIFFERENCE:
                    result = Solid.Difference(op1, op2, tolerance);    //su dll nuova e' possibile inserire parametro di tolleranza
                    break;

                case IfcBooleanOperator.UNION:
                    result = Solid.Union(op1, op2, tolerance);
                    break;

                case IfcBooleanOperator.INTERSECTION:
                    result = Solid.Intersection(op1, op2, tolerance);
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
                WriteSTL ws = new WriteSTL(new Entity[] { op1, op2 }, new Layer[] { new Layer("Default") }, new Dictionary<string, Block>(), @"c:\devdept\booleanError\gino"+ bcr.Index +".stl", 0.01, true);   
                count++;
                ws.DoWork();
                debug += "Error in boolean operation\n";
                return op1;
            }
        }

        public static devDept.Eyeshot.Entities.Region getRegionFromIfcProfileDef(IfcProfileDef ipd)
        {
            devDept.Eyeshot.Entities.Region region = null;

            if (ipd is IfcCircleProfileDef)
            {
                IfcCircleProfileDef crProfDef = (IfcCircleProfileDef)ipd;

                region = new CircularRegion(crProfDef.Radius);
            }
            else if (ipd is IfcIShapeProfileDef) // IfcIShapeProfileDef and all derived from
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

                CompositeCurve cc = getCompositeCurveFromIfcCurve(arProfDef.OuterCurve);

                if (cc != null)

                    region = new devDept.Eyeshot.Entities.Region(cc);
            }
            else
            {
                if (!debug.Contains("IfcProfileDef not supported: " + ipd.KeyWord))
                    debug += "IfcProfileDef not supported: " + ipd.KeyWord + "\n";
            }
            if (ipd is IfcParameterizedProfileDef)
            {
                IfcParameterizedProfileDef parProfDef = (IfcParameterizedProfileDef)ipd;
                if (parProfDef.Position != null && region != null)
                {
                    region.Translate(parProfDef.Position.Location.Coordinates.Item1, parProfDef.Position.Location.Coordinates.Item2, parProfDef.Position.Location.Coordinates.Item3);
                }
            }
            return region;
        }

        public static string Debug { get { return debug; } }

        private static Point3D getPoint3DFromIfcCartesianPoint (IfcCartesianPoint icp)
        {
            return new Point3D( icp.Coordinates.Item1, icp.Coordinates.Item2, icp.Coordinates.Item3);
        }

        private static Color getColorFromIfcRepresentationItem(IfcRepresentationItem reprItem)
        {
            Color color = Color.Green;
            try
            {
                IfcStyledItem ifcStyledItem = reprItem.mStyledByItem;

                IfcPresentationStyleAssignment sas = (IfcPresentationStyleAssignment) ifcStyledItem.Styles[0];

                IfcSurfaceStyle ss = (IfcSurfaceStyle)sas.Styles[0];

                IfcSurfaceStyleRendering ssr = (IfcSurfaceStyleRendering)ss.Styles[0];

                color = ssr.SurfaceColour.Colour;
            }
            catch(Exception e)
            {
                //debug += e.Message + "\n";
            }
            return color;
        }
    }
}
