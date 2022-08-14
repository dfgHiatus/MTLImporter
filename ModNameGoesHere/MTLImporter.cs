using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System.IO;
using System.Collections.Generic;

namespace MTLImporter
{
    public class MTLImporter : NeosMod
    {
        public override string Name => "MTLImporter";
        public override string Author => "dfgHiatus";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/MTLImporter/";

        private static ModConfiguration _config;

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> enabled = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);

        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.MTLImporter").PatchAll();
            _config = GetConfiguration();
        }

        [HarmonyPatch(typeof(UniversalImporter), "Import", typeof(AssetClass), typeof(IEnumerable<string>),
            typeof(World), typeof(float3), typeof(floatQ), typeof(bool))]
        public class UniversalImporterPatch
        {
            static bool Prefix(ref IEnumerable<string> files)
            {
                foreach (var file in files)
                {
                    if (Path.GetExtension(file).ToLower().Equals(".mtl") && _config.GetValue(enabled))
                    {
                        // https://stackoverflow.com/questions/65201192/read-from-file-split-content-into-group-when-empty-line
                        List<List<string>> materials = new List<List<string>>();
                        List<string> currentMaterial = new List<string>();

                        foreach (string line in File.ReadLines(file))
                        {
                            if (line.Trim().Length == 0)
                            {
                                if (currentMaterial.Count > 0)
                                {
                                    materials.Add(currentMaterial);
                                    currentMaterial = new List<string>();
                                }
                            }
                            else
                            {
                                currentMaterial.Add(line);
                            }
                        }

                        if (currentMaterial.Count > 0)
                        {
                            materials.Add(currentMaterial);
                        }

                        var mtlMaterial = new List<MTLMaterial>();

                        foreach (var material in materials)
                        {
                            var mtlMat = new MTLMaterial();
                            mtlMaterial.Add(mtlMat);
                            foreach (var propertyTuple in material)
                            {
                                var lineSplit = propertyTuple.Split(' ');
                                switch (lineSplit[0])
                                {
                                    case "newmtl":
                                        mtlMat.name = lineSplit[1];
                                        break;
                                    case "Ns":
                                        mtlMat.roughness = MTLUtils.ToFloat(propertyTuple);
                                        break;
                                    case "d":
                                        mtlMat.alpha = MTLUtils.ToFloat(propertyTuple);
                                        break;
                                    case "Tr":
                                        mtlMat.nonAlpha = MTLUtils.ToFloat(propertyTuple);
                                        break;
                                    case "Ka":
                                        mtlMat.ambientColor = MTLUtils.ToColor(propertyTuple);
                                        break;
                                    case "Kd":
                                        mtlMat.diffuseColor = MTLUtils.ToColor(propertyTuple);
                                        break;
                                    case "Ks":
                                        mtlMat.specularColor = MTLUtils.ToColor(propertyTuple);
                                        break;
                                    case "Ke":
                                        mtlMat.emissionColor = MTLUtils.ToColor(propertyTuple);
                                        break;
                                    case "illum":
                                        mtlMat.isMetallic = mtlMat.ConvertFromMTL(MTLUtils.ToInt(propertyTuple));
                                        break; 
                                }

                                mtlMat.ComputeAlpha();

                                var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot(mtlMat.name);
                                slot.PositionInFrontOfUser();

                                // TODO Populate
                                if (mtlMat.isMetallic)
                                {
                                    slot.CreateMaterialOrb<PBS_Metallic>();
                                    var neosMat = slot.GetComponent<PBS_Metallic>();
                                    neosMat.AlbedoColor.Value = mtlMat.diffuseColor;
                                }
                                else
                                {
                                    slot.CreateMaterialOrb<UnlitMaterial>();
                                    var neosMat = slot.AttachComponent<UnlitMaterial>();
                                    neosMat.TintColor.Value = mtlMat.diffuseColor;
                                }     
                            }
                        }
                    }
                }

                return true;
            }
        }
    }
}