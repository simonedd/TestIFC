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
    interface IfcEntity : IEntity
    {
        Dictionary<string, string> Identification { get; }

        Dictionary<string, object> Location { get; }

        Dictionary<string, object> Quantities { get; }

        Dictionary<string, double> Material { get; }
    }



    class IfcMesh : Mesh, IfcEntity
    {
        public IfcSpatialElement Parent { get; internal set; }

        public IfcMesh() : base()
        {
            _identification = new Dictionary<string, string>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _material = new Dictionary<string, double>();
        }

        public IfcMesh(IList<Point3D> vertices, IList<IndexTriangle> triangles) : base(vertices, triangles)
        {
            _identification = new Dictionary<string, string>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _material = new Dictionary<string, double>();
        }

        private Dictionary<string, string> _identification;

        public Dictionary<string, string> Identification { get { return _identification; } }

        private Dictionary<string, object> _location;

        public Dictionary<string, object> Location { get { return _location; } }

        private Dictionary<string, object> _quantities;

        public Dictionary<string, object> Quantities { get { return _quantities; } }

        private Dictionary<string, double> _material;

        public Dictionary<string, double> Material { get { return _material; } }

        public override string Dump()
        {
            String dump = base.Dump();

            dump += "----------------------\r\n";

            dump += "Identification\r\n";

            foreach (string key in Identification.Keys)
            {
                String value;
                Identification.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }
            dump += "Material\r\n";

            foreach (string key in Material.Keys)
            {
                Double value;
                Material.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }
            return dump;
        }
    }

    class IfcBlockReference : BlockReference, IfcEntity
    {
        public IfcSpatialElement Parent { get; internal set; }

        public IfcBlockReference(string blockName) : base(blockName)
        {
            _identification = new Dictionary<string, string>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _material = new Dictionary<string, double>();
        }

        public IfcBlockReference(Transformation t, string blockName) : base(t, blockName)
        {
            _identification = new Dictionary<string, string>();
            _location = new Dictionary<string, object>();
            _quantities = new Dictionary<string, object>();
            _material = new Dictionary<string, double>();
        }

        private Dictionary<string, string> _identification;

        public Dictionary<string, string> Identification { get { return _identification; } }

        private Dictionary<string, object> _location;

        public Dictionary<string, object> Location { get { return _location; } }

        private Dictionary<string, object> _quantities;

        public Dictionary<string, object> Quantities { get { return _quantities; } }

        private Dictionary<string, double> _material;

        public Dictionary<string, double> Material { get { return _material; } }

        public override string Dump()
        {
            String dump = base.Dump();

            dump += "\r\n----------------------\r\n";

            dump += "Identification\r\n";

            foreach (string key in Identification.Keys)
            {
                String value;
                Identification.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }
            dump += "Material\r\n";

            foreach (string key in Material.Keys)
            {
                Double value;
                Material.TryGetValue(key, out value);
                dump += key + ": " + value + "\r\n";
            }
            return dump;
        }
    }

    static class UtilityIfc
    {
        public static void loadProperties(IfcEntity ifcEntity, IfcElement ifcElement)
        {
            ifcEntity.ColorMethod = colorMethodType.byEntity;
            
            
            #region Identification
            ifcEntity.Identification.Add("Model", "nome del file");
            ifcEntity.Identification.Add("Name", ifcElement.Name);
            ifcEntity.Identification.Add("Type", ifcElement.ObjectType);
            ifcEntity.Identification.Add("GUID", ifcElement.GlobalId);
            ifcEntity.Identification.Add("KeyWord", ifcElement.KeyWord);
            #endregion

            #region Material
            if (ifcElement.MaterialSelect != null)
            {
                if (ifcElement.MaterialSelect is IfcMaterialLayerSetUsage)
                {
                    IfcMaterialLayerSetUsage mlsu = (IfcMaterialLayerSetUsage)ifcElement.MaterialSelect;

                    //foreach(IfcMaterialLayer ml in mlsu.ForLayerSet.MaterialLayers)
                    IfcMaterialLayerSet mls = mlsu.ForLayerSet;
                    for (int i = 0; i < mls.MaterialLayers.Count; i++)
                    {
                        ifcEntity.Material.Add(i + " " + mls.MaterialLayers[i].Material.Name, mls.MaterialLayers[i].LayerThickness);

                        IfcMaterial ifcMaterial = mls.MaterialLayers[i].Material;

                        if( ifcMaterial.HasRepresentation != null)
                        {
                            IfcMaterialDefinitionRepresentation imdr = ifcMaterial.HasRepresentation;
                            foreach(IfcStyledRepresentation isr in imdr.Representations)
                            {
                                foreach(IfcStyledItem isi in isr.Items)
                                {
                                    foreach (IfcPresentationStyleAssignment isas in isi.Styles) // se c'e' presentatio style direttamente
                                    {
                                        foreach(IfcPresentationStyle ips in isas.Styles)
                                        {
                                            if(ips is IfcSurfaceStyle)
                                            {
                                                IfcSurfaceStyle iss = (IfcSurfaceStyle)ips;

                                                foreach( var item in iss.Styles)
                                                {
                                                    // vedere IFcSurfaceStyleElementSelect
                                                    if(item is IfcSurfaceStyleRendering)
                                                    {
                                                        IfcSurfaceStyleRendering issr = (IfcSurfaceStyleRendering)item;

                                                        ifcEntity.Identification.Add(i + "C" + mls.MaterialLayers[i].Material.Name, issr.SurfaceColour.Colour.ToString());

                                                        ifcEntity.Color = issr.SurfaceColour.Colour;  
                                                    }
                                                }
                                            }
                                            else if (ips is IfcCurveStyle)
                                            {
                                                //ddgfdg
                                            }
                                        }
                                    }

                                    //IfcStyledItem ifcStyledItem = reprItem.mStyledByItem;

                                    //IfcStyleAssignmentSelect sas = (IfcPresentationStyleAssignment)ifcStyledItem.Styles[0];

                                    //IfcSurfaceStyle ss = (IfcSurfaceStyle)sas.Styles[0];

                                    //IfcSurfaceStyleRendering ssr = (IfcSurfaceStyleRendering)ss.Styles[0];

                                    //color = ssr.SurfaceColour.Colour;
                                }
                            }
                        }
                    }
                }
            }
            #endregion
        }
    }
}
