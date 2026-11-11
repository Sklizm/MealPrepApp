using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Localization;
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
    public async Task CatalogFiltersIntersectLocallyWithCompleteCountsAndStableSorts()
    {
        var repository = new CatalogFixtureRepository();
        using var provider = RedesignSamples.Services(configure: s => { s.AddSingleton<IngredientRepository>(repository); s.AddSingleton<LookupRepository>(new CatalogFixtureLookups()); });
        using var scope = provider.CreateScope();
        var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>();
        await vm.LoadAsync();
        Assert.False(vm.HasError);
        Assert.Equal(new[] { 5, 2, 1, 1, 0, 1 }, vm.CategoryIndex.Select(c => c.Count));
        vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == 1);
        vm.SearchTerm = "ap"; Assert.Equal("Apple", Assert.Single(vm.Ingredients).Name);
        Assert.Equal(5, vm.CategoryIndex[0].Count);
        vm.SearchTerm = "  "; Assert.Equal(2, vm.Ingredients.Count);
        vm.SelectedCategory = vm.Categories[0];
        vm.SortOrder = IngredientSortIds.NameAscending; Assert.Equal(new[] { "Apple", "Basil", "Milk", "Rice", "Salt" }, vm.Ingredients.Select(i => i.Name));
        vm.SortOrder = IngredientSortIds.NameDescending; Assert.Equal(new[] { "Salt", "Rice", "Milk", "Basil", "Apple" }, vm.Ingredients.Select(i => i.Name));
        vm.SortOrder = IngredientSortIds.Category; Assert.Equal(new[] { "Apple", "Basil", "Milk", "Rice", "Salt" }, vm.Ingredients.Select(i => i.Name));
        vm.SortOrder = IngredientSortIds.MissingNutrition; Assert.Equal(new[] { "Basil", "Rice", "Salt", "Apple", "Milk" }, vm.Ingredients.Select(i => i.Name));
        vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == -1); Assert.Equal("Salt", Assert.Single(vm.Ingredients).Name);
        vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == 9); Assert.Equal("Rice", Assert.Single(vm.Ingredients).Name);
        Assert.Equal(1, repository.Reads); Assert.All(repository.CategoryArguments, Assert.Null);
        vm.SearchTerm = "Rice"; await vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(9, vm.SelectedCategory!.IngredientCategoryID); Assert.Equal("Rice", vm.SearchTerm); Assert.Equal(IngredientSortIds.MissingNutrition, vm.SortOrder);
        Assert.Equal(2, repository.Reads); Assert.Single(vm.Ingredients);
        vm.SelectedCategory = vm.Categories.Single(c => c.IngredientCategoryID == 10); vm.SearchTerm = "";
        var emptyCategory = vm.EmptyMessage; vm.SearchTerm = "missing"; Assert.NotEqual(emptyCategory, vm.EmptyMessage);
        repository.Rows = []; await vm.RetryCommand.ExecuteAsync(null); Assert.NotEqual(emptyCategory, vm.EmptyMessage);
    }

    [Fact]
    public async Task CatalogNutritionConfirmationRefreshesButCancellationDoesNot()
    {
        var repository = new CatalogFixtureRepository(); var dialogs = new CatalogResultDialogs();
        using var provider = RedesignSamples.Services(dialogs, s => { s.AddSingleton<IngredientRepository>(repository); s.AddSingleton<LookupRepository>(new CatalogFixtureLookups()); });
        using var scope = provider.CreateScope(); var vm = scope.ServiceProvider.GetRequiredService<IngredienteListViewModel>(); await vm.LoadAsync();
        await vm.EditNutritionCommand.ExecuteAsync(vm.Ingredients[0]); Assert.Equal(1, repository.Reads);
        dialogs.Result = true;
        await vm.EditNutritionCommand.ExecuteAsync(vm.Ingredients[0]); Assert.Equal(2, repository.Reads);
        await vm.AddIngredientCommand.ExecuteAsync(null); Assert.Equal(3, repository.Reads);
    }

    [AvaloniaFact]
    public void CatalogCalendarTodayHonorsConstraintsAndKeepsTemplateParts()
    {
        var picker = new LocalizedCalendarDatePicker { Width=220, SelectedDate=DateTime.Today.AddDays(-5), DisplayDateStart=DateTime.Today.AddDays(-10), DisplayDateEnd=DateTime.Today.AddDays(-1) };
        var window = new Window { Content=picker, Width=400, Height=200 };
        try
        {
            window.Show(); picker.IsDropDownOpen=true; Dispatcher.UIThread.RunJobs();
            var popup = picker.GetVisualDescendants().OfType<Popup>().First();
            var today = popup.Child!.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Today");
            var calendar = popup.Child!.GetVisualDescendants().OfType<Calendar>().Single();
            Assert.False(today.IsEnabled);
            Assert.Equal(42, calendar.GetVisualDescendants().OfType<CalendarDayButton>().Count());
            picker.DisplayDateEnd = DateTime.Today.AddDays(10); picker.IsDropDownOpen=false; picker.IsDropDownOpen=true; Dispatcher.UIThread.RunJobs();
            Assert.True(today.IsEnabled);
            calendar.DisplayMode=CalendarMode.Year; Dispatcher.UIThread.RunJobs(); calendar.UpdateLayout(); Assert.True(picker.IsDropDownOpen);
            Assert.All(calendar.GetVisualDescendants().OfType<Button>().Where(b=>b.Name is "PART_PreviousButton" or "PART_NextButton"), b=>Assert.True(b.Bounds.Width >= 36));
            var selectedBefore=picker.SelectedDate; var displayBefore=calendar.DisplayDate;
            var monthsBefore=calendar.GetVisualDescendants().OfType<CalendarButton>().Select(b=>b.Content?.ToString()).ToArray();
            var languageBefore=L.Current.Language;
            try { L.Current.Language=languageBefore == "ru" ? "ro" : "ru"; Dispatcher.UIThread.RunJobs(); Assert.Equal(displayBefore,calendar.DisplayDate); Assert.Equal(selectedBefore,picker.SelectedDate);
                Assert.NotEqual(string.Join("|",monthsBefore),string.Join("|",calendar.GetVisualDescendants().OfType<CalendarButton>().Select(b=>b.Content?.ToString()))); }
            finally { L.Current.Language=languageBefore; }
            calendar.DisplayMode=CalendarMode.Decade; Dispatcher.UIThread.RunJobs(); Assert.True(picker.IsDropDownOpen);
            today.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(DateTime.Today, picker.SelectedDate); Assert.False(picker.IsDropDownOpen);
            Assert.True(picker.GetVisualDescendants().OfType<TextBox>().Single().IsFocused);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CatalogDialogsKeepActionsVisibleAndEnterOutOfMultilineAndEscapeCancels()
    {
        using var provider = RedesignSamples.Services(); using var scope=provider.CreateScope();
        var vm=scope.ServiceProvider.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanMealDialogViewModel>();
        var dialog=new MealPrepApp.Desktop.Views.Planificare.PlanMealDialog { DataContext=vm };
        try
        {
            dialog.Show(); Dispatcher.UIThread.RunJobs(); dialog.UpdateLayout();
            Assert.True(dialog.Bounds.Height <= 560);
            var save=dialog.GetVisualDescendants().OfType<Button>().Single(b=>ReferenceEquals(b.Command,vm.SaveCommand));
            Assert.True(save.TranslatePoint(default,dialog)!.Value.Y+save.Bounds.Height <= dialog.Bounds.Height);
            var notes=dialog.GetVisualDescendants().OfType<TextBox>().Single(t=>t.AcceptsReturn); notes.Focus();
            dialog.KeyPressQwerty(PhysicalKey.Enter,RawInputModifiers.None); dialog.KeyReleaseQwerty(PhysicalKey.Enter,RawInputModifiers.None); Dispatcher.UIThread.RunJobs();
            Assert.False(vm.HasError);
            vm.IsBusy=true; dialog.KeyPressQwerty(PhysicalKey.Escape,RawInputModifiers.None); dialog.KeyReleaseQwerty(PhysicalKey.Escape,RawInputModifiers.None); Assert.True(dialog.IsVisible);
            vm.IsBusy=false; dialog.KeyPressQwerty(PhysicalKey.Escape,RawInputModifiers.None); Assert.False(dialog.IsVisible);
        }
        finally { dialog.Close(); }
    }

    [AvaloniaFact]
    public void CatalogDestructiveConfirmationDoesNotUseEnterAsDefault()
    {
        var dialog=new MealPrepApp.Desktop.Views.Shared.MessageDialog();
        dialog.Configure(MealPrepApp.Desktop.Views.Shared.MessageDialogKind.Confirm,L.Text("Text.Delete"),L.Text("Text.Cancel"),true);
        try
        {
            dialog.Show(); Dispatcher.UIThread.RunJobs();
            var action=dialog.FindControl<Button>("OkButton")!;
            Assert.Contains("Danger",action.Classes); Assert.DoesNotContain("Primary",action.Classes);
            dialog.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});
            Assert.True(dialog.IsVisible);
            dialog.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});
            Assert.False(dialog.IsVisible);
        }
        finally { dialog.Close(); }
    }

    private sealed class CatalogResultDialogs : IDialogService
    {
        public bool Result;
        public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(false);
        public Task ShowErrorAsync(string message)=>Task.CompletedTask;
        public Task ShowInfoAsync(string title,string message)=>Task.CompletedTask;
        public Task<bool> ShowDialogAsync(object viewModel)=>Task.FromResult(Result);
    }
}

public sealed class CatalogFixtureRepository() : IngredientRepository(new RedesignSamples.NoDatabase())
{
    public int Reads;
    public List<int?> CategoryArguments { get; } = [];
    public Ingredient[] Rows = [
        new() { IngredientID=1, Name="Apple", IngredientCategoryID=1, IngredientCategoryName="Produse", DefaultUnitAbbreviation="g", HasNutrition=true },
        new() { IngredientID=2, Name="Basil", IngredientCategoryID=1, IngredientCategoryName="Produse", DefaultUnitAbbreviation="g" },
        new() { IngredientID=3, Name="Milk", IngredientCategoryID=2, IngredientCategoryName="Lactate si oua", DefaultUnitAbbreviation="ml", HasNutrition=true },
        new() { IngredientID=4, Name="Rice", IngredientCategoryID=9, IngredientCategoryName="Custom collection", DefaultUnitAbbreviation="g" },
        new() { IngredientID=5, Name="Salt", DefaultUnitAbbreviation="g" }
    ];
    public override Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(int? ingredientCategoryId=null,CancellationToken cancellationToken=default)
    { Reads++; CategoryArguments.Add(ingredientCategoryId); return Task.FromResult<IReadOnlyList<Ingredient>>(Rows); }
}
public sealed class CatalogFixtureLookups() : LookupRepository(new RedesignSamples.NoDatabase())
{
    public override Task<IReadOnlyList<IngredientCategory>> GetIngredientCategoriesAsync(CancellationToken cancellationToken=default) => Task.FromResult<IReadOnlyList<IngredientCategory>>([
        new() { IngredientCategoryID=1, Name="Produse" }, new() { IngredientCategoryID=2, Name="Lactate si oua" }, new() { IngredientCategoryID=9, Name="Custom collection" }, new() { IngredientCategoryID=10, Name="Conserve" }
    ]);
    public override Task<IReadOnlyList<Unit>> GetUnitsAsync(CancellationToken cancellationToken=default) => Task.FromResult<IReadOnlyList<Unit>>([new() { UnitID=1, Name="Gram", Abbreviation="g" }]);
}
