namespace MTLImporter.Tests;

public class Program
{
    static void Main()
    {
        string file = "Tests/test.mtl";
        List<List<string>> materials = new();

        // https://stackoverflow.com/questions/65201192/read-from-file-split-content-into-group-when-empty-line
        try
        {
            List<string> currentMaterial = new();

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

        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine(e.Message);
        }
        
        foreach (var material in materials)
        {
            foreach (var property in material)
            {
               Console.WriteLine($"{property}");
            }
            Console.WriteLine($"");
        }
    }
}