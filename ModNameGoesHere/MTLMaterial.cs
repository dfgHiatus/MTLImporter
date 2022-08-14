using BaseX;
using FrooxEngine;
using System;

namespace MTLImporter
{
    // https://people.sc.fsu.edu/~jburkardt/data/mtl/mtl.html
    // https://steamcommunity.com/sharedfiles/filedetails/?id=2005695630
    public class MTLMaterial
    {
        public string name = "New Material";
        public float roughness = 100f;
        public color ambientColor = new color(0.2f, 0.2f, 0.2f);
        public color diffuseColor = new color(0.8f, 0.8f, 0.8f);
        public color specularColor = new color(1.0f, 1.0f, 1.0f);
        public color emissionColor = new color(0.0f, 0.0f, 0.0f);
        public float alpha = 1.0f;
        public float nonAlpha = 0.0f;
        public bool isMetallic = false;
        public string fileName = string.Empty;

        // Transparent Color (Tf) and Index of Refraction (Ni) are not utilized
        public MTLMaterial()
        {
            
        }

        public MTLMaterial(string newmtl, float Ns, color Ka, color Kd, color Ks, color Ke, float d, float TR, int illum)
        {
            name = newmtl;
            ambientColor = Ka;
            diffuseColor = Kd;
            emissionColor = Ke;
            alpha = TR - d;
            roughness = Ns;
            specularColor = Ks;
            isMetallic = ConvertFromMTL(illum);
        }

        public void ComputeAlpha()
        {
            alpha = MathX.Max(alpha, 1 - nonAlpha);
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
}
