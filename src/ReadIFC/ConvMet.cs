using devDept.Geometry;
using GeometryGym.Ifc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsApplication1
{
    public static class ConvMet
    {
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
    }
}
