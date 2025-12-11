using LspTypes;
using System.Reflection;

class Program
{
    static void Main()
    {
        // Check SemanticTokensLegend properties
        Console.WriteLine("SemanticTokensLegend properties:");
        foreach (var prop in typeof(SemanticTokensLegend).GetProperties())
        {
            Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }

        // Check SemanticTokensOptions properties
        Console.WriteLine("\nSemanticTokensOptions properties:");
        foreach (var prop in typeof(SemanticTokensOptions).GetProperties())
        {
            Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }

        // Check ServerCapabilities properties for semantic tokens
        Console.WriteLine("\nServerCapabilities semantic token properties:");
        foreach (var prop in typeof(ServerCapabilities).GetProperties())
        {
            if (prop.Name.ToLower().Contains("semantic"))
                Console.WriteLine($"  {prop.Name}: {prop.PropertyType.Name}");
        }
    }
}
