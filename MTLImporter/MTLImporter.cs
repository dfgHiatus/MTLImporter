using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Reflection;
using System;

namespace MTLImporter
{
	/*
	* [____CURSOR PARKING LOT_______]
	* [                             ]
	* [_____________________________]
	*  EDIT: this was important when we were in live share
	* 	Users present at one point: dfgHiatus, eia485
	*/

	public class MTLImporter : NeosMod
	{
		public override string Name => "MTLImporter";
		public override string Author => "dfgHiatus";
		public override string Version => "1.0.0";
		public override string Link => "https://github.com/dfgHiatus/MTLImporter/";

		private static ModConfiguration _config;
		private static Dictionary<string, StaticTexture2D> assetDict = new Dictionary<string, StaticTexture2D>();

		[AutoRegisterConfigKey]
		public static ModConfigurationKey<bool> forceBright = new ModConfigurationKey<bool>("forceBright", "Tint imported abledbo color as white. Fixes entirely dark materials", () => true);
		[AutoRegisterConfigKey]
		public static ModConfigurationKey<Material> forceMaterial = new ModConfigurationKey<Material>("forceMaterial", "Force material type on import", () => Material.None);

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

		[HarmonyPatch(typeof(UniversalImporter), "ImportTask", typeof(AssetClass), typeof(IEnumerable<string>), typeof(World), typeof(float3), typeof(floatQ), typeof(float3), typeof(bool))]
		public class UniversalImporterPatch
		{
			static bool Prefix(IEnumerable<string> files, ref Task __result)
			{
				var query = files.Where(x => x.ToLower().EndsWith("mtl"));
				if (query.Count() > 0)
                {
					__result = ProcessMTLImport(query);
				}
				return true;
			}
		}

		private static async Task ProcessMTLImport(IEnumerable<string> files)
		{
			await default(ToBackground);
			var filesArr = files.ToArray();
			for (int i = 0; i < filesArr.Count(); i++)
			{
				var file = filesArr[i];
				if (Path.GetExtension(file).ToLower().Equals(".mtl"))
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

								case "illum":
									mtlMat.illum = MTLUtils.ToInt(lineSplit[1]);
									break;

								case "d":
									mtlMat.alpha = MTLUtils.ToFloat(lineSplit[1]);
									break;
								case "Tr":
									mtlMat.nonAlpha = MTLUtils.ToFloat(lineSplit[1]);
									break;

								case "Ka":
									mtlMat.ambientColor = MTLUtils.ToColor(lineSplit.Skip(1).ToArray());
									break;
								case "map_Ka":
									mtlMat.ambientMap = lineSplit[1];
									break;

								case "Kd":
									mtlMat.diffuseColor = MTLUtils.ToColor(lineSplit.Skip(1).ToArray());
									break;
								case "map_Kd":
									mtlMat.diffuseMap = lineSplit[1];
									break;

								case "norm":
								case "bump":
								case "map_bump":
									mtlMat.normalMap = lineSplit[1];
									break;

								case "disp":
								// case "height":
								// case "map_disp":
								// case "map_height":
									mtlMat.heightMap = lineSplit[1];
									break;

								case "Ke":
									mtlMat.emissionColor = MTLUtils.ToColor(lineSplit.Skip(1).ToArray());
									break;
								case "map_Ke":
									mtlMat.emissionMap = lineSplit[1];
									break;

								case "Ns":
								case "Pr":
									mtlMat.roughness = MTLUtils.ToFloat(lineSplit[1]);
									break;
								case "map_Pr":
									mtlMat.roughnessMap = lineSplit[1];
									break;

								case "Pm":
									mtlMat.metallic = MTLUtils.ToFloat(lineSplit[1]);
									break;
								case "map_Pm":
									mtlMat.metallicMap = lineSplit[1];
									break;

								case "Ks":
									mtlMat.specularColor = MTLUtils.ToColor(lineSplit.Skip(1).ToArray());
									break;
								case "map_Ks":
									mtlMat.specularMap = lineSplit[1];
									break;

								case "Tf":
									Warn($"Transparent Color is redundant. MTL importer will not import this.");
									break;
								case "Ni":
									Warn($"Index of Refraction imports are not supported. MTL importer will not import this.");
									break;

								default:
									Warn($"MTL definition was not found: {propertyTuple}");
									break;
							}
						}
					}

					foreach (var material in mtlMaterial)
					{
						await default(ToWorld);
						var slot = Engine.Current.WorldManager.FocusedWorld.AddSlot(material.name);
						slot.PositionInFrontOfUser();
						slot.GlobalPosition = slot.GlobalPosition + new float3(i * .2f, 0f, 0f);
						await default(ToBackground);

						await SetupTextures(file, material, slot).ConfigureAwait(false);

						await default(ToWorld);
						var forceMat = _config.GetValue(forceMaterial);
						if (forceMat != Material.None)
                        {
							switch (forceMat)
							{
								case Material.PBS_Metallic:
									SetupMetallic(material, slot);
									break;
								case Material.PBS_Specular:
									SetupSpecular(material, slot);
									break;
								case Material.Unlit:
									SetupUnlit(material, slot);
									break;
							}
						}
						else
                        {
							if (material.specularMap != string.Empty)
								if (material.isMetallic)
									SetupMetallic(material, slot);
								else
									SetupSpecular(material, slot);
							else
								SetupUnlit(material, slot);
						}
						await default(ToBackground);
					}
				}
				assetDict.Clear();
			} 
		}

		private static async Task SetupTextures(string file, MTLMaterial material, Slot slot)
		{
			var textureFiles = new string[] {
				material.ambientMap,
				material.diffuseMap,
				material.emissionMap,
				material.normalMap,
				material.heightMap,
				material.metallicMap,
				material.specularMap,
				material.roughnessMap,
			};

			foreach (var texture in textureFiles)
			{
				if (!string.IsNullOrEmpty(texture))
				{

					var path = Path.Combine(Path.GetDirectoryName(file), texture);
					var img = await MTLUtils.ImportImage(path, slot.World);

					bool hasAlpha = false;
					try
					{
						hasAlpha = img.RawAsset.HasAlpha;
					}
					catch (Exception) { } // So we don't crash

					// Alpha from intensity
					if (!hasAlpha && (texture == material.metallicMap || texture == material.specularMap))
					{
						img.ProcessPixels(delegate (color c)
						{
							float3 rgb = c.rgb;
							float3 v = c.rgb;
							return new color(in rgb, MathX.MaxComponent(in v));
						});
					}

					// Invert RGB, then Alpha from intensity
					if (!hasAlpha && (texture == material.roughnessMap))
					{
						img.ProcessPixels(delegate (color c)
						{
							float3 v = c.rgb;
							float3 rgb = 1 - v;
							return new color(in rgb, c.a);
						});

						img.ProcessPixels(delegate (color c)
						{
							float3 rgb = c.rgb;
							float3 v = c.rgb;
							return new color(in rgb, MathX.MaxComponent(in v));
						});
					}

					assetDict.Add(texture, img);
				}
			}
		}
		
		private static void SetupMetallic(MTLMaterial material, Slot slot)
		{
			slot.CreateMaterialOrb<PBS_Metallic>();
			var neosMat = slot.GetComponent<PBS_Metallic>();
			StaticTexture2D tex = null;

			if (material.alpha == 1.0f)
				neosMat.AlbedoColor.Value = material.diffuseColor;
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

			if (_config.GetValue(forceBright))
				neosMat.AlbedoColor.Value = color.White;

			if (assetDict.TryGetValue(material.diffuseMap, out tex))
				neosMat.AlbedoTexture.Target = tex;
			if (assetDict.TryGetValue(material.ambientMap, out tex))
				neosMat.AlbedoTexture.Target = tex;

			neosMat.EmissiveColor.Value = material.emissionColor;
			if (assetDict.TryGetValue(material.emissionMap, out tex))
				neosMat.EmissiveMap.Target = tex;

			if (assetDict.TryGetValue(material.normalMap, out tex))
				neosMat.NormalMap.Target = tex;

			if (assetDict.TryGetValue(material.heightMap, out tex))
				neosMat.HeightMap.Target = tex;
			
			neosMat.Metallic.Value = material.metallic;
			if (assetDict.TryGetValue(material.metallicMap, out tex))
				neosMat.MetallicMap.Target = tex;
			neosMat.Smoothness.Value = 1 - material.roughness;
			if (assetDict.TryGetValue(material.roughnessMap, out tex))
				neosMat.MetallicMap.Target = tex;
		}

		private static void SetupSpecular(MTLMaterial material, Slot slot)
		{
			slot.CreateMaterialOrb<PBS_Specular>();
			var neosMat = slot.GetComponent<PBS_Specular>();
			StaticTexture2D tex = null;

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

			if (_config.GetValue(forceBright))
				neosMat.AlbedoColor.Value = color.White;

			if (assetDict.TryGetValue(material.diffuseMap, out tex))
				neosMat.AlbedoTexture.Target = tex;
			if (assetDict.TryGetValue(material.ambientMap, out tex))
				neosMat.AlbedoTexture.Target = tex;

			neosMat.EmissiveColor.Value = material.emissionColor;
			if (assetDict.TryGetValue(material.emissionMap, out tex))
				neosMat.EmissiveMap.Target = tex;

			if (assetDict.TryGetValue(material.normalMap, out tex))
				neosMat.NormalMap.Target = tex;

			if (assetDict.TryGetValue(material.heightMap, out tex))
				neosMat.HeightMap.Target = tex;

			neosMat.SpecularColor.Value = material.specularColor;
			if (assetDict.TryGetValue(material.specularMap, out tex))
				neosMat.SpecularMap.Target = tex;
		}

		private static void SetupUnlit(MTLMaterial material, Slot slot)
		{
			slot.CreateMaterialOrb<UnlitMaterial>();
			var neosMat = slot.GetComponent<UnlitMaterial>();
			StaticTexture2D tex = null;

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

			if (_config.GetValue(forceBright))
				neosMat.TintColor.Value = color.White;

			if (assetDict.TryGetValue(material.diffuseMap, out tex))
				neosMat.Texture.Target = tex;
			if (assetDict.TryGetValue(material.ambientMap, out tex))
				neosMat.Texture.Target = tex;
		}
	}
}