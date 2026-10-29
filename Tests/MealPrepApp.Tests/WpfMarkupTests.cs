using System.Text.RegularExpressions;
using System.Xml.Linq;
using MealPrepApp.ViewModels;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    [Fact]
    public void WpfSharedMarkupTypesResolveFromTheCoreAssembly()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MealPrepApp.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var assembly = typeof(ViewModelBase).Assembly;
        var files = Directory.GetFiles(Path.Combine(directory.FullName, "App", "MealPrepApp"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));
        var declarations = 0;
        var references = 0;
        foreach (var file in files)
        {
            var document = XDocument.Load(file);
            foreach (var declaration in document.Descendants().Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
            {
                var value = declaration.Value;
                if (!value.StartsWith("clr-namespace:MealPrepApp.ViewModels") && !value.StartsWith("clr-namespace:MealPrepApp.Models"))
                    continue;
                declarations++;
                var parts = value.Split(';');
                Assert.Contains("assembly=" + assembly.GetName().Name, parts);
                var clrNamespace = parts[0]["clr-namespace:".Length..];
                var prefix = declaration.Name.LocalName;
                var scope = declaration.Parent!.DescendantsAndSelf();
                var usedTypes = scope.Attributes().SelectMany(attribute => Regex.Matches(attribute.Value,
                        @"\b" + Regex.Escape(prefix) + @":([A-Za-z_]\w*)").Cast<Match>().Select(match => match.Groups[1].Value))
                    .Concat(scope.Where(element => element.Name.NamespaceName == value).Select(element => element.Name.LocalName.Split('.')[0]))
                    .Distinct();
                foreach (var name in usedTypes)
                {
                    var type = assembly.GetType(clrNamespace + "." + name);
                    Assert.True(type?.IsPublic == true, $"{file}: {prefix}:{name} is not a public type in {assembly.GetName().Name}");
                    references++;
                }
            }
        }
        Assert.True(declarations > 0);
        Assert.True(references > 0);
    }
}
