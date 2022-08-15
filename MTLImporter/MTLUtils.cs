using System;
using System.IO;
using System.Threading.Tasks;
using BaseX;
using FrooxEngine;

namespace MTLImporter
{
	public static class MTLUtils
	{
		public static int ToInt(string line)
		{
			int canidate = 0;
			int.TryParse(line, out canidate);
			return canidate;
		}

		public static float ToFloat(string line)
		{
			float canidate = 0;
			float.TryParse(line, out canidate);
			return canidate;
		}

		public static color ToColor(string[] line)
		{
			float r = 0f;
			float g = 0f;
			float b = 0f;
			float.TryParse(line[0], out r);
			float.TryParse(line[1], out g);
			float.TryParse(line[2], out b);
			return new color(r, g, b);
		}

		public static async Task<StaticTexture2D> ImportImage(string path, World world)
		{
			Uri uri = new Uri(path);
			if (!uri.IsWellFormedOriginalString())
			{
				await default(ToBackground);
				LocalDB localDB = world.Engine.LocalDB;
				uri = await localDB.ImportLocalAssetAsync(path, LocalDB.ImportLocation.Copy).ConfigureAwait(continueOnCapturedContext: false);
			}

			await default(ToWorld);
			StaticTexture2D comp = null;
			foreach (var c in world.AssetsSlot.GetComponentsInChildren<StaticTexture2D>())
			{
				if (c is StaticTexture2D staticTexture)
                {
					if (staticTexture.URL.Value == uri)
						return staticTexture;
                }
			}
				
			var textureSlot = world.AssetsSlot.FindChild((s) => s.Name == "mtlImporter");
			if (comp == null)
			{
				if (textureSlot == null)
				{
					textureSlot = world.AssetsSlot.AddSlot("mtlImporter");
				}
			}

			textureSlot.Name = Path.GetFileNameWithoutExtension(path);
			StaticTexture2D tex = textureSlot.AttachComponent<StaticTexture2D>();
			tex.URL.Value = uri;
			return tex;
		}
	}
}
