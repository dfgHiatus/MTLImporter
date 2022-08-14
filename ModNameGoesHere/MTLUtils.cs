using BaseX;

namespace MTLImporter
{
    public static class MTLUtils
    {
        public static int ToInt(string line)
        {
            return int.Parse(line.Split(' ')[1]);
        }

        public static float ToFloat(string line)
        {
            return float.Parse(line.Split(' ')[1]);
        }

        public static color ToColor(string line)
        {
            var split = line.Split(' ');
            return new color(float.Parse(split[1]), float.Parse(split[2]), float.Parse(split[3]));
        }
    }
}
