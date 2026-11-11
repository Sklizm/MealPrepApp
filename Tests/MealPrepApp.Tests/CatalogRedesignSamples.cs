using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Views.Ingrediente;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Ingrediente;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Tests;

/// <summary>Synthetic illustrated catalog matrix, independent of database or native display access.</summary>
public static class CatalogRedesignSamples
{
    public static void Render(string output)
    {
        Application.Current!.Resources["MotionDuration"] = TimeSpan.Zero;
        Application.Current.Resources["MotionEnabled"] = false;
        using var services = RedesignSamples.Services(configure: s =>
        {
            s.AddSingleton<IngredientRepository>(new CatalogFixtureRepository { Rows = Rows() });
            s.AddSingleton<LookupRepository>(new IllustratedLookups());
        });
        foreach (var language in new[] { "en", "ro", "ru" })
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        foreach (var size in new[] { new Size(860, 600), new Size(1280, 800), new Size(1440, 900) })
        {
            L.Current.Language = language;
            Application.Current.RequestedThemeVariant = theme;
            using var scope = services.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>();
            vm.LoadAsync().GetAwaiter().GetResult();
            var window = new Window { Width = size.Width, Height = size.Height, Content = new IngredienteListView { DataContext = vm } };
            var directory = Path.Combine(output, language); Directory.CreateDirectory(directory);
            try
            {
                window.Show();
                foreach (var state in new[] { "all", "selected", "custom", "empty", "error" })
                {
                    vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == (state switch { "selected" => 1, "custom" => 9, "empty" => 10, _ => 0 }));
                    vm.ErrorContent = state == "error" ? L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog") : null;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    using var bitmap = new RenderTargetBitmap(new PixelSize((int)size.Width, (int)size.Height), new Vector(96, 96));
                    bitmap.Render(window);
                    bitmap.Save(Path.Combine(directory, $"ingredients-{state}-{theme}-{size.Width}.png"));
                }
            }
            finally { window.Close(); }
            Console.WriteLine($"Rendered catalog: {language}/{theme}/{size.Width}×{size.Height}, five states");
        }
        var manifest = Directory.GetFiles(output, "*.png", SearchOption.AllDirectories).Order().Select(path => new
        {
            File = Path.GetRelativePath(output, path), Bytes = new FileInfo(path).Length,
            SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
        });
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Ingredient[] Rows()
    {
        var groups = new (int Id, string Category, string Unit, string[] Names)[]
        {
            (1, "Produse", "g", ["Apples", "Avocado", "Bell peppers", "Carrots", "Tomatoes"]),
            (2, "Lactate si oua", "ml", ["Milk", "Yogurt"]),
            (3, "Carne si peste", "g", ["Chicken breast", "Salmon"]),
            (4, "Conserve", "g", ["Chickpeas", "Canned tomatoes"]),
            (5, "Condimente si ierburi", "g", ["Basil", "Paprika"]),
            (6, "Cereale si paste", "g", ["Brown rice", "Pasta"]),
            (7, "Bauturi", "ml", ["Apple juice"]),
            (8, "Altele", "g", ["Baking powder"]),
            (9, "Weekend baking and homemade favorites", "g", ["Wholegrain sourdough starter"]),
            (-1, "", "g", ["Salt"])
        };
        return groups.SelectMany(g => g.Names.Select(name => new Ingredient
        {
            Name = name, IngredientCategoryID = g.Id > 0 ? g.Id : null,
            IngredientCategoryName = g.Category, DefaultUnitAbbreviation = g.Unit
        })).Select((row, index) => { row.IngredientID = index + 1; row.HasNutrition = index % 3 != 1; return row; }).ToArray();
    }

    private sealed class IllustratedLookups() : LookupRepository(new RedesignSamples.NoDatabase())
    {
        public override Task<IReadOnlyList<IngredientCategory>> GetIngredientCategoriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IngredientCategory>>(FridgeCategory.DefaultNames.Select((name, index) => new IngredientCategory { IngredientCategoryID = index + 1, Name = name })
                .Concat([new() { IngredientCategoryID = 9, Name = "Weekend baking and homemade favorites" }, new() { IngredientCategoryID = 10, Name = "Seasonal collection" }]).ToArray());
    }
}
