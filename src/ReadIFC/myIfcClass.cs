using devDept.Eyeshot.Entities;
using devDept.Geometry;
using GeometryGym.Ifc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        private static Dictionary<string, Color> defaultColor;

        static UtilityIfc()
        {
            defaultColor = new Dictionary<string, Color>();

            defaultColor.Add("IfcBuildingElementProxy", Color.FromArgb(255, 150, 150, 150));
            defaultColor.Add("IfcWall", Color.FromArgb(255, 246, 233, 186));
            defaultColor.Add("IfcWallStandardCase", Color.FromArgb(255, 246, 233, 186));
            defaultColor.Add("IfcSlab", Color.FromArgb(255, 204, 255, 255));
            defaultColor.Add("IfcColumn", Color.FromArgb(255, 80, 80, 100));
            defaultColor.Add("IfcOpening", Color.FromArgb(65, 204, 204, 255));
            defaultColor.Add("IfcBeam", Color.FromArgb(255, 80, 80, 100));
            defaultColor.Add("IfcCurtainWall", Color.FromArgb(65, 204, 204, 255));
            defaultColor.Add("IfcDoor", Color.FromArgb(255, 200, 200, 200));
            defaultColor.Add("IfcObject", Color.FromArgb(255, 150, 150, 150));
            defaultColor.Add("IfcWindow", Color.FromArgb(65, 204,204,255));
            defaultColor.Add("IfcSpace", Color.FromArgb(50, 153, 204, 0));
            defaultColor.Add("IfcStair", Color.FromArgb(255, 86, 170, 198));
            defaultColor.Add("IfcRoof", Color.FromArgb(255, 153, 155, 255));

        }

        public static void loadProperties(IfcEntity ifcEntity, IfcProduct ifcProduct)
        {
            ifcEntity.ColorMethod = colorMethodType.byEntity;
            
            #region Identification
            ifcEntity.Identification.Add("Model", "nome del file");
            ifcEntity.Identification.Add("Name", ifcProduct.Name);
            ifcEntity.Identification.Add("Type", ifcProduct.ObjectType);
            ifcEntity.Identification.Add("GUID", ifcProduct.GlobalId);
            ifcEntity.Identification.Add("KeyWord", ifcProduct.KeyWord);
            #endregion

            if (ifcProduct is IfcElement)
            {
                IfcElement ifcElement = (IfcElement)ifcProduct;

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

                            if (ifcMaterial.HasRepresentation != null)
                            {
                                IfcMaterialDefinitionRepresentation imdr = ifcMaterial.HasRepresentation;
                                foreach (IfcStyledRepresentation isr in imdr.Representations)
                                {
                                    foreach (IfcStyledItem isi in isr.Items)
                                    {
                                        foreach (IfcPresentationStyleAssignment isas in isi.Styles) // se c'e' presentatio style direttamente
                                        {
                                            foreach (IfcPresentationStyle ips in isas.Styles)
                                            {
                                                if (ips is IfcSurfaceStyle)
                                                {
                                                    IfcSurfaceStyle iss = (IfcSurfaceStyle)ips;

                                                    foreach (var item in iss.Styles)
                                                    {
                                                        // vedere IFcSurfaceStyleElementSelect
                                                        if (item is IfcSurfaceStyleRendering)
                                                        {
                                                            IfcSurfaceStyleRendering issr = (IfcSurfaceStyleRendering)item;

                                                            int alpha = Convert.ToInt32((1 - issr.Transparency) * 255);

                                                            ifcEntity.Color = Color.FromArgb(alpha, issr.SurfaceColour.Colour);

                                                            ifcEntity.Identification.Add(i + "C" + mls.MaterialLayers[i].Material.Name, issr.SurfaceColour.Colour.ToString());
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
            }
            #endregion

            if( ifcEntity.Color == Color.Black)
            {
                Color color;
                if (defaultColor.TryGetValue(ifcProduct.KeyWord, out color))
                    ifcEntity.Color = color;
                //else
                    //Debug.Write(ifcElement.KeyWord + " default color not set\n");
            }
        }
    }
}
