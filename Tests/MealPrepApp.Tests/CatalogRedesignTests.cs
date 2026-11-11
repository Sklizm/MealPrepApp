using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Controls;
using MealPrepApp.Desktop.Views.Ingrediente;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Ingrediente;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    [Fact]
    public async Task CatalogSummariesFollowFiltersRefreshAndLanguageWithoutChangingIdentities()
    {
        var repository = new CatalogFixtureRepository();
        using var services = RedesignSamples.Services(configure: s =>
        {
            s.AddSingleton<IngredientRepository>(repository);
            s.AddSingleton<LookupRepository>(new CatalogFixtureLookups());
        });
        using var scope = services.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>();
        await vm.LoadAsync();
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        var language = L.Current.Language;
        try
        {
            L.Current.Language = "en";
            Assert.Equal(5, vm.TotalIngredientCount);
            Assert.Equal(2, vm.NutritionIngredientCount);
            Assert.Equal("5 ingredients · 2 with nutrition", vm.CatalogSummary);
            Assert.Equal("5 results", vm.ResultSummary);
            Assert.Equal("All categories", vm.SelectedCategoryTitle);
            vm.CatalogSelection = vm.CategoryIndex.Single(c => c.Illustration == "Produce");
            vm.SearchTerm = "ap";
            Assert.Equal("1 result", vm.ResultSummary);
            Assert.Equal("Produce", vm.SelectedCategoryTitle);
            Assert.Contains(nameof(vm.ResultSummary), notifications);
            Assert.Contains(nameof(vm.SelectedCategoryTitle), notifications);
            Assert.Equal(5, vm.CategoryIndex[0].Count);
            var selection = vm.CatalogSelection;
            foreach (var next in new[] { "ro", "ru" })
            {
                var oldSummary = vm.CatalogSummary; var oldTitle = vm.SelectedCategoryTitle;
                notifications.Clear(); L.Current.Language = next;
                Assert.NotEqual(oldSummary, vm.CatalogSummary);
                Assert.NotEqual(oldTitle, vm.SelectedCategoryTitle);
                Assert.Contains(nameof(vm.CatalogSummary), notifications);
                Assert.Contains(nameof(vm.ResultSummary), notifications);
                Assert.Same(selection, vm.CatalogSelection);
                Assert.Equal("ap", vm.SearchTerm);
            }
            vm.CatalogSelection = vm.CategoryIndex.Single(c => c.Category.IngredientCategoryID == 9);
            Assert.Equal("Custom collection", vm.SelectedCategoryTitle);
            Assert.Equal("Other", vm.CatalogSelection!.Illustration);
            vm.SearchTerm = "";
            repository.Rows = [new() { IngredientID = 6, Name = "Custom", IngredientCategoryID = 9, HasNutrition = true }];
            await vm.RetryCommand.ExecuteAsync(null);
            Assert.Equal(1, vm.TotalIngredientCount); Assert.Equal(1, vm.NutritionIngredientCount);
            Assert.Contains(nameof(vm.CatalogSummary), notifications);
            Assert.Equal(9, vm.CatalogSelection!.Category.IngredientCategoryID);
            Assert.Equal(1, vm.CategoryIndex[0].Count);
        }
        finally { L.Current.Language = language; }
    }

    [AvaloniaFact]
    public void CatalogVirtualizesTwoThousandRowsAndSupportsCategoryAndResultKeyboardNavigation()
    {
        var repository = new CatalogFixtureRepository
        {
            Rows = Enumerable.Range(1, 2000).Select(i => new Ingredient
            {
                IngredientID = i, Name = $"Ingredient {i:0000}", IngredientCategoryID = 1,
                IngredientCategoryName = "Produse", DefaultUnitAbbreviation = "g", HasNutrition = i % 2 == 0
            }).ToArray()
        };
        using var services = RedesignSamples.Services(configure: s =>
        {
            s.AddSingleton<IngredientRepository>(repository);
            s.AddSingleton<LookupRepository>(new CatalogFixtureLookups());
        });
        using var scope = services.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>();
        vm.LoadAsync().GetAwaiter().GetResult();
        var view = new IngredienteListView { DataContext = vm };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        try
        {
            window.Show(); PumpCatalog(window);
            var rail = view.FindControl<ListBox>("CategorySidebar")!;
            var list = view.FindControl<ListBox>("IngredientList")!;
            Assert.Equal(2000, list.ItemCount);
            Assert.IsType<VirtualizingStackPanel>(list.ItemsPanelRoot);
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 30);
            Assert.Contains(rail.GetVisualDescendants().OfType<FridgeFoodIllustration>(), i => i.Kind == "Produce");
            Assert.Contains(rail.GetVisualDescendants().OfType<FridgeFoodIllustration>(), i => i.Kind == "Jar");
            rail.ContainerFromIndex(0)!.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); PumpCatalog(window);
            Assert.Equal(1, vm.CatalogSelection!.Category.IngredientCategoryID);
            Assert.Equal(2000, list.ItemCount);
            list.SelectedIndex = 0; list.ContainerFromIndex(0)!.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); PumpCatalog(window);
            Assert.Equal(1, list.SelectedIndex);
            list.ScrollIntoView(1999); PumpCatalog(window);
            Assert.NotNull(list.ContainerFromIndex(1999));
            Assert.InRange(list.GetRealizedContainers().Count(), 1, 30);
            Assert.Contains(list.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Ingredient 2000");

            window.Width = 860; window.Height = 600; PumpCatalog(window);
            Assert.False(view.FindControl<Border>("CategoryPanel")!.IsVisible);
            var compact = view.FindControl<ComboBox>("CompactCategory")!;
            Assert.True(compact.IsVisible); Assert.Same(vm.CatalogSelection, compact.SelectedItem);
            compact.Focus(); compact.IsDropDownOpen = true; PumpCatalog(window);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None); PumpCatalog(window);
            Assert.Equal(2, vm.CatalogSelection!.Category.IngredientCategoryID);
            Assert.Empty(vm.Ingredients);
            window.Width = 1440; PumpCatalog(window);
            Assert.True(view.FindControl<Border>("CategoryPanel")!.IsVisible);
            Assert.Same(vm.CatalogSelection, rail.SelectedItem);
            Assert.Equal(1, repository.Reads);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CatalogTranslatedLayoutsKeepControlsInsideViewportAcrossThemesAndStates()
    {
        using var services = RedesignSamples.Services(configure: s =>
        {
            s.AddSingleton<IngredientRepository>(new CatalogFixtureRepository());
            s.AddSingleton<LookupRepository>(new CatalogFixtureLookups());
        });
        using var scope = services.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>();
        vm.LoadAsync().GetAwaiter().GetResult();
        var custom = vm.Categories.Single(c => c.IngredientCategoryID == 9);
        custom.Name = "Custom collection with a deliberately long category name for layout verification";
        var language = L.Current.Language;
        var theme = Application.Current!.RequestedThemeVariant;
        try
        {
            foreach (var next in new[] { "en", "ro", "ru" })
            foreach (var nextTheme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            foreach (var size in new[] { new Size(860, 600), new Size(1280, 800), new Size(1440, 900), new Size(600, 600), new Size(1000, 600) })
            {
                L.Current.Language = next; Application.Current.RequestedThemeVariant = nextTheme;
                var view = new IngredienteListView { DataContext = vm };
                var window = new Window { Width = size.Width, Height = size.Height, Content = view };
                try
                {
                    window.Show(); PumpCatalog(window);
                    foreach (var state in new[] { 0, 9, 10, -1 })
                    {
                        vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == state);
                        vm.ErrorMessage = null; PumpCatalog(window);
                        foreach (var name in new[] { "CatalogSearch", "CatalogSort", "CatalogRefresh", "AddIngredient", "IngredientList" })
                        {
                            var control = view.FindControl<Control>(name)!;
                            var origin = control.TranslatePoint(default, window)!.Value;
                            Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{name}: {next}/{size}/{state}");
                            Assert.InRange(origin.X, 0, size.Width);
                            Assert.True(origin.X + control.Bounds.Width <= size.Width + .5, $"{name} overflow: {next}/{size}/{state}");
                            Assert.True(origin.Y + control.Bounds.Height <= size.Height + .5, $"{name} vertical overflow: {next}/{size}/{state}");
                        }
                        foreach (var scroll in view.FindControl<ListBox>("IngredientList")!.GetVisualDescendants().OfType<ScrollViewer>())
                            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + .5);
                    }
                    vm.ErrorContent = L.Message("Text.TheOperationCouldNotBeCompletedCheckTheAppLog"); PumpCatalog(window);
                    Assert.True(vm.HasError);
                    var retry = view.FindControl<Button>("CatalogRefresh")!;
                    Assert.True(retry.IsEnabled && retry.IsEffectivelyVisible);
                    vm.ErrorMessage = null;
                }
                finally { window.Close(); }
            }
        }
        finally { L.Current.Language = language; Application.Current.RequestedThemeVariant = theme; }
    }

    private static void PumpCatalog(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    }
}
