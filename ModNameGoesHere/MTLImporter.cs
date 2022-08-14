using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MTLImporter
{
    public class MTLImporter : NeosMod
    {
        public override string Name => "MTLImporter";
        public override string Author => "dfgHiatus";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/MTLImporter/";

        private static ModConfiguration _config;
        private static Dictionary<string, StaticTexture2D> assetDict = new Dictionary<string, StaticTexture2D>();

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> enabled = new ModConfigurationKey<bool>("enabled", "Enabled", () => true);

        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.MTLImporter").PatchAll();
            _config = GetConfiguration();
            Engine.Current.RunPostInit(() => AssetPatch());
        }

        public static void AssetPatch()
        {
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Special].Add("mtl");
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
                                    case "map_Ks":
                                        mtlMat.specularMap = lineSplit[1];
                                        break;
                                    case "map_Kd":
                                        mtlMat.diffuseMap = lineSplit[1];
                                        break;
                                }

                                mtlMat.ComputeAlpha();   
                            }
                        }

                        foreach (var material in mtlMaterial)
                        {
                            var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot(material.name);
                            slot.PositionInFrontOfUser();

                            SetupTextures(file, material, slot);
                            
                            if (material.isMetallic)
                            {
                                if (material.specularMap != string.Empty)
                                    SetupSpecular(file, material, slot);
                                else
                                    SetupMetallic(file, material, slot);
                            }
                            else
                            {
                                SetupUnlit(file, material, slot);
                            }
                        }
                    }
                }

                assetDict.Clear();
                return true;
            }
        }

        public static void SetupTextures(string file, MTLMaterial material, Slot slot)
        {
            if (!string.IsNullOrEmpty(material.diffuseMap))
            {
                var diffuseTextureSlot = slot.AddSlot("Diffuse Map");
                var path = Path.Combine(Path.GetDirectoryName(file), material.diffuseMap);
                UniLog.Log(path);
                ImageImporter.ImportImage(path, diffuseTextureSlot);
                assetDict.Add(material.diffuseMap, slot.GetComponent<StaticTexture2D>());
            }

            if (!string.IsNullOrEmpty(material.specularMap))
            {
                var specularTextureSlot = slot.AddSlot("Specular Map");
                var path = Path.Combine(Path.GetDirectoryName(file), material.specularMap);
                UniLog.Log(path);
                ImageImporter.ImportImage(path, specularTextureSlot);
                assetDict.Add(material.specularMap, slot.GetComponent<StaticTexture2D>());
            }
        }

        public static void SetupMetallic(string file, MTLMaterial material, Slot slot)
        {
            UniLog.Log("Starting Metallic");
            slot.CreateMaterialOrb<PBS_Metallic>();
            var neosMat = slot.GetComponent<PBS_Metallic>();

            if (material.alpha == 1.0f)
            {
                neosMat.AlbedoColor.Value = material.diffuseColor;
            }
            else
            {
                neosMat.BlendMode.Value = BlendMode.Alpha;
                neosMat.AlbedoColor.Value = new color(
                    material.diffuseColor.r,
                    material.diffuseColor.g,
                    material.diffuseColor.b,
                    material.alpha
                );
            }

            neosMat.AlbedoTexture.Target = assetDict[material.diffuseMap];
            neosMat.EmissiveColor.Value = material.emissionColor;
            neosMat.Smoothness.Value = (100.0f - material.roughness) / 100.0f;
        }

        public static void SetupSpecular(string file, MTLMaterial material, Slot slot)
        {
            UniLog.Log("Starting Specular");
            slot.CreateMaterialOrb<PBS_Specular>();
            var neosMat = slot.GetComponent<PBS_Specular>();

            if (material.alpha == 1.0f)
            {
                neosMat.AlbedoColor.Value = material.diffuseColor;
            }
            else
            {
                neosMat.BlendMode.Value = BlendMode.Alpha;
                neosMat.AlbedoColor.Value = new color(
                    material.diffuseColor.r,
                    material.diffuseColor.g,
                    material.diffuseColor.b,
                    material.alpha
                );
            }

            neosMat.AlbedoTexture.Target = assetDict[material.diffuseMap];
            neosMat.EmissiveColor.Value = material.emissionColor;
            neosMat.SpecularColor.Value = material.specularColor;
            neosMat.SpecularMap.Target = assetDict[material.specularMap];
        }

        public static void SetupUnlit(string file, MTLMaterial material, Slot slot)
        {
            UniLog.Log("Starting Unlit");
            slot.CreateMaterialOrb<UnlitMaterial>();
            var neosMat = slot.GetComponent<UnlitMaterial>();

            if (material.alpha == 1.0f)
            {
                neosMat.TintColor.Value = material.diffuseColor;
            }
            else
            {
                neosMat.BlendMode.Value = BlendMode.Alpha;
                neosMat.TintColor.Value = new color(
                    material.diffuseColor.r,
                    material.diffuseColor.g,
                    material.diffuseColor.b,
                    material.alpha
                );
            }

            neosMat.Texture.Target = assetDict[material.diffuseMap];
        }
    }
}