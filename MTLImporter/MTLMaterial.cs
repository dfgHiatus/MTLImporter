using Elements.Core;

namespace MTLImporter;

// https://people.sc.fsu.edu/~jburkardt/data/mtl/mtl.html
// https://steamcommunity.com/sharedfiles/filedetails/?id=2005695630
public class MTLMaterial
{
    public MTLMaterial() { }

    public string name = "New Material";

    public colorX diffuseColor = new colorX(0.8f, 0.8f, 0.8f);
    public string diffuseMap = string.Empty;

    public colorX ambientColor = new colorX(0.2f, 0.2f, 0.2f);
    public string ambientMap = string.Empty;

    public colorX emissionColor = new colorX(0.0f, 0.0f, 0.0f);
    public string emissionMap = string.Empty;

    public string normalMap = string.Empty;

    public string heightMap = string.Empty;

    public float alpha = 1.0f;

    public float nonAlpha { set => alpha = MathX.Max(alpha, 1 - value); }

    public bool isMetallic = false;
    public float metallic = 0.0f;
    public string metallicMap = string.Empty;

    public string specularMap = string.Empty;
    public colorX specularColor = new colorX(0.0f, 0.0f, 0.0f, 0.25f);

    private float _roughness = 0.75f;
    public float roughness
    {
        set => _roughness = (100.0f - value) / 100.0f;
        get => _roughness; 
    }
    public string roughnessMap = string.Empty;

    public int illum
    {
        set
        {
            isMetallic = ConvertFromMTL(value);
        }
    }
    
    public bool ConvertFromMTL(int index)
    {
        switch (index)
        {
            case 0:
            case 1:
            case 2:
            case 4:
            case 6:
            case 7:
            case 9:
                return false;
            case 3:
            case 5:
            case 8:
                return true;
            default:
                return false;
        }
    }
}
