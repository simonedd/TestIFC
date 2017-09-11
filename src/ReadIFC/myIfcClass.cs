using devDept.Eyeshot.Entities;
using devDept.Geometry;
using GeometryGym.Ifc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsApplication1
{
    class IfcMesh : Mesh
    {
        public IfcSpatialElement Parent { get; internal set; }

        public IfcMesh() : base()
        {
            _identification = new Dictionary<string, object>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _Material = new Dictionary<string, object>();
        }

        public IfcMesh( IList<Point3D> vertices, IList<IndexTriangle> triangles) : base(vertices, triangles)
        {
            _identification = new Dictionary<string, object>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _Material = new Dictionary<string, object>();
        }


        private Dictionary<string, object> _identification;

        public Dictionary<string, object> Identification { get { return _identification; } }

        private Dictionary<string, object> _location;

        public Dictionary<string, object> Location { get { return _location; } }

        private Dictionary<string, object> _quantities;

        public Dictionary<string, object> Quantities { get { return _quantities; } }

        private Dictionary<string, object> _Material;

        public Dictionary<string, object> Material { get { return _quantities; } }

        public void loadProperty(IfcElement ifcElement)
        {
            #region Identification
            Identification.Add("Model", "nome del file");
            Identification.Add("Name", ifcElement.Name);
            Identification.Add("Type", ifcElement.ObjectType);
            Identification.Add("GUID", ifcElement.Guid);
            #endregion

            #region Material
            if(ifcElement.MaterialSelect != null)
            {
                if(ifcElement.MaterialSelect is IfcMaterialLayerSetUsage)
                {
                    IfcMaterialLayerSetUsage mlsu = (IfcMaterialLayerSetUsage)ifcElement.MaterialSelect;

                    //foreach(IfcMaterialLayer ml in mlsu.ForLayerSet.MaterialLayers)
                    IfcMaterialLayerSet mls = mlsu.ForLayerSet;
                    for (int i = 0; i < mls.MaterialLayers.Count; i++)
                    {
                        Material.Add(i + " " + mls.MaterialLayers[i].Material.Name, mls.MaterialLayers[i].LayerThickness);
                    }
                }
            }
            #endregion
        }

        public override string Dump()
        {
            String dump = base.Dump();

            dump += "----------------------\r\n";

            dump += "Identification";
            foreach (string key in Identification.Keys)
            {
                Object value;
                Identification.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }


            dump += "Material\r\n";
            foreach (string key in Material.Keys)
            {
                Object value;
                Material.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }

            return dump;
        }


    }
   
}
