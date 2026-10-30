using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using MealPrepApp.Desktop.Localization;
using Avalonia.Threading;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Ingrediente;
using MealPrepApp.ViewModels.Retete;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MealPrepApp.Tests;

public sealed partial class RegressionTests
{
    [Fact]
    public void LocalizationPreferenceMigrationPreservesOtherPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "preferences.json");
        try
        {
            Assert.Equal("en", new PresentationPreferences(path).Language);
            foreach (var language in new[] { "", ",\"Language\":\"xx\"", ",\"Language\":null", ",\"Language\":42" })
            {
                File.WriteAllText(path, "{\"Theme\":2,\"RecipeListView\":true,\"ReducedMotion\":true" + language + "}");
                var preferences = new PresentationPreferences(path);
                Assert.Equal("en", preferences.Language);
                Assert.Equal(Appearance.Dark, preferences.Theme);
                Assert.True(preferences.RecipeListView);
                Assert.True(preferences.ReducedMotion);
                preferences.Language = "ru";
                Assert.Equal("ru", new PresentationPreferences(path).Language);
                preferences.Language = "unsupported";
                Assert.Equal("en", new PresentationPreferences(path).Language);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void LocalizationCatalogsHaveIdenticalKeysAndValidPlaceholders()
    {
        var reference = LocalizationService.Catalogs["en"];
        foreach (var catalog in LocalizationService.Catalogs.Values)
        {
            Assert.Equal(reference.Keys.Order(), catalog.Keys.Order());
            foreach (var (key, text) in catalog)
            {
                Assert.False(string.IsNullOrWhiteSpace(text), key);
                var format = CompositeFormat.Parse(text);
                Assert.Equal(CompositeFormat.Parse(reference[key]).MinimumArgumentCount, format.MinimumArgumentCount);
                Assert.Equal(Regex.Matches(reference[key], @"\{(\d+)[^}]*\}").Select(m => m.Groups[1].Value).Order(),
                    Regex.Matches(text, @"\{(\d+)[^}]*\}").Select(m => m.Groups[1].Value).Order());
                _ = string.Format(CultureInfo.InvariantCulture, format, Enumerable.Repeat<object>(new FormatProbe(), format.MinimumArgumentCount).ToArray());
            }
        }
    }
    private sealed class FormatProbe : IFormattable
    {
        public string ToString(string? format, IFormatProvider? provider) => "value";
    }

    [Fact]
    public void LocalizationPluralRulesCoverRussianAndRomanianBoundaries()
    {
        var service = new LocalizationService { Language = "ru" };
        foreach (var n in new[] { 1, 21, 101 }) Assert.EndsWith(" ингредиент", service.Plural("Count.Ingredients", n));
        foreach (var n in new[] { 2, 4, 22 }) Assert.EndsWith(" ингредиента", service.Plural("Count.Ingredients", n));
        foreach (var n in new[] { 0, 5, 11, 12, 14, 20, 25, 111 }) Assert.EndsWith(" ингредиентов", service.Plural("Count.Ingredients", n));
        service.Language = "ro";
        Assert.Equal("1 ingredient", service.Plural("Count.Ingredients", 1));
        Assert.Equal("0 ingrediente", service.Plural("Count.Ingredients", 0));
        Assert.Equal("19 ingrediente", service.Plural("Count.Ingredients", 19));
        Assert.Equal("20 de ingrediente", service.Plural("Count.Ingredients", 20));
        service.Language = "en";
        Assert.Equal("1 ingredient", service.Plural("Count.Ingredients", 1));
        Assert.Equal("2 ingredients", service.Plural("Count.Ingredients", 2));
        service.Language = "invalid";
        Assert.Equal("en", service.Language);
    }

    [Fact]
    public void LocalizationMessagesAndErrorsKeepTheirIdentityAcrossSwitches()
    {
        L.Current.Language = "en";
        using var model = new FridgeCategory("Produse", 22, true);
        var message = DbExceptionMapper.ToMessage(50004);
        Assert.Contains("another session", message.ToString());
        L.Current.Language = "ru";
        Assert.Contains("другом сеансе", message.ToString());
        Assert.Equal("22 ингредиента", model.Summary);
        Assert.StartsWith("Овощи и фрукты", model.AccessibleName);
        Assert.Equal("Custom category", BuiltInLabels.Display("Custom category"));
        L.Current.Language = "ro";
        Assert.Equal("Produse", BuiltInLabels.Display("Produse"));
    }

    [Fact]
    public void LocalizationPreservesRegionalNumbersAndDateOrder()
    {
        foreach (var region in new[] { "en-US", "ro-RO", "de-DE" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(region);
            var number = 1234.5m.ToString("N2");
            var datePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            foreach (var language in new[] { "en", "ru", "ro" })
            {
                L.Current.Language = language;
                Assert.Equal(number, 1234.5m.ToString("N2"));
                Assert.Equal(datePattern, CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern);
                Assert.Equal(L.Current.UICulture.DateTimeFormat.MonthNames, CultureInfo.CurrentCulture.DateTimeFormat.MonthNames);
            }
        }
    }

    [AvaloniaFact]
    public void LocalizationUpdatesOpenWindowsWithoutDependingOnDataContext()
    {
        L.Current.Language = "en";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var preferences = new PresentationPreferences(path);
        var settings = new SettingsWindow(preferences) { DataContext = new object() };
        var shell = new ShellWindow();
        var menu = (NativeMenuItem)NativeMenu.GetMenu(shell)!.Items[0];
        var settingsMenu = (NativeMenuItem)menu.Menu!.Items[0];
        var connection = new ConnectionSetupWindow(new MealPrepApp.Desktop.Services.ConnectionSettingsStore(
            path + ".connection", basePath: path + ".missing", localPaths: Array.Empty<string>()));
        try
        {
            settings.Show(); connection.Show(); Dispatcher.UIThread.RunJobs();
            Assert.Equal("Settings", settings.Title);
            Assert.Equal("Settings", settingsMenu.Header);
            var server = connection.FindControl<TextBox>("ServerBox")!;
            server.Text = "db.example.com,1433";
            var picker = settings.FindControl<ComboBox>("LanguagePicker")!;
            picker.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            Assert.Equal("ru", L.Current.Language);
            Assert.Equal("Настройки", settings.Title);
            Assert.Equal("Настройки", settingsMenu.Header);
            Assert.Contains("Подключение", connection.Title);
            Assert.Equal("ru", new PresentationPreferences(path).Language);
            Assert.Equal("db.example.com,1433", server.Text);
            picker.SelectedIndex = 2; Dispatcher.UIThread.RunJobs();
            Assert.Equal("Setări", settings.Title);
        }
        finally { connection.Close(); shell.Close(); settings.Close(); if (File.Exists(path)) File.Delete(path); }
    }

    [AvaloniaFact]
    public async Task LocalizationSwitchPreservesDirtyEditorCachedPagesAndCooking()
    {
        L.Current.Language = "en";
        var pantryRepository = new LocalizationPantryRepository();
        using var services = RedesignSamples.Services(configure: s => s.AddSingleton<PantryRepository>(pantryRepository));
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ISessionService>().SignIn(1, "Sample");
        var pantry = scope.ServiceProvider.GetRequiredService<FrigiderViewModel>();
        await pantry.LoadAsync();
        Assert.Equal(1, pantryRepository.Reads);
        pantry.SearchTerm = "Tomato";
        pantry.SelectedCategory = "Produse";
        pantry.SortOrder = PantrySortIds.Quantity;
        var editor = scope.ServiceProvider.GetRequiredService<ReteteEditorViewModel>();
        editor.Title = "Unsaved recipe"; editor.Instructions = "Keep my text"; editor.Servings = 3;
        var session = scope.ServiceProvider.GetRequiredService<CookingSession>();
        session.StartRecipe(new RecipeFull { RecipeID = 10, Servings = 2, Ingredients = [new() { IngredientName = "Tomato", Quantity = 100 }] }, 3);
        session.Ingredients[0].IsChecked = true;
        session.StartTimerCommand.Execute(null);
        var items = pantry.Items; var ingredient = session.Ingredients[0];
        var changes = new List<string?>();
        editor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var mutations = 0;
        items.CollectionChanged += (_, _) => mutations++;
        pantry.ErrorContent = L.Message("Text.TheQuantityMustBePositive");
        foreach (var language in new[] { "ru", "ro", "en" })
        {
            L.Current.Language = language; Dispatcher.UIThread.RunJobs();
            Assert.Same(items, pantry.Items);
            Assert.Equal(1, pantryRepository.Reads);
            Assert.Equal("Tomato", pantry.SearchTerm);
            Assert.Equal("Produse", pantry.SelectedCategory);
            Assert.Equal(PantrySortIds.Quantity, pantry.SortOrder);
            Assert.Equal("Unsaved recipe", editor.Title);
            Assert.Equal("Keep my text", editor.Instructions);
            Assert.Equal(3, editor.Servings);
            Assert.True(editor.IsDirty);
            Assert.Same(ingredient, session.Ingredients[0]); Assert.True(ingredient.IsChecked);
            Assert.True(session.IsTimerRunning);
            Assert.Equal(0, mutations);
            Assert.DoesNotContain(nameof(editor.Title), changes);
            Assert.DoesNotContain(nameof(editor.Servings), changes);
            Assert.DoesNotContain(null, changes);
            Assert.DoesNotContain(string.Empty, changes);
            Assert.Equal(L.Text("Text.TheQuantityMustBePositive"), pantry.ErrorMessage);
        }
        session.StopTimerCommand.Execute(null);
    }
    [AvaloniaFact]
    public async Task LocalizationRequestFinishesInTheCurrentLanguage()
    {
        L.Current.Language = "en";
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var completion = new TaskCompletionSource();
        var connection = new ConnectionSetupWindow(new MealPrepApp.Desktop.Services.ConnectionSettingsStore(path, path, []),
            testConnection: (_, _) => completion.Task);
        try
        {
            connection.Show();
            connection.FindControl<TextBox>("ServerBox")!.Text = "db.example.com,1433";
            connection.FindControl<TextBox>("PasswordBox")!.Text = "synthetic-password";
            var button = connection.FindControl<Button>("TestButton")!;
            var status = connection.FindControl<TextBlock>("Status")!;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(L.Text("Text.CheckingConnection"), status.Text);
            L.Current.Language = "ru"; Dispatcher.UIThread.RunJobs();
            Assert.Equal("Проверка подключения…", status.Text);
            completion.SetException(new InvalidOperationException("synthetic-password"));
            for (var attempt = 0; attempt < 100 && !button.IsEnabled; attempt++) await Task.Yield();
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsEnabled);
            Assert.Equal(L.Text("Text.ConnectionUnavailableCheckTheAddressVpnCertificateAndCredentials"), status.Text);
            Assert.DoesNotContain("synthetic-password", status.Text);
            L.Current.Language = "ro"; Dispatcher.UIThread.RunJobs();
            Assert.Equal(L.Text("Text.ConnectionUnavailableCheckTheAddressVpnCertificateAndCredentials"), status.Text);
        }
        finally { connection.Close(); }
    }

    [AvaloniaFact]
    public void LocalizationCalendarKeepsDateAndInputWhileTranslatingMonthNames()
    {
        L.Current.Language = "en";
        var date = new DateTime(2026, 3, 15);
        var picker = new LocalizedCalendarDatePicker { SelectedDate = date, Width = 200 };
        var window = new Window { Content = picker };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            picker.IsDropDownOpen = true; Dispatcher.UIThread.RunJobs();
            var selectedChanges = 0;
            picker.SelectedDateChanged += (_, _) => selectedChanges++;
            L.Current.Language = "ru"; Dispatcher.UIThread.RunJobs();
            Assert.Equal(date, picker.SelectedDate);
            Assert.Equal(0, selectedChanges);
            var popup = picker.GetVisualDescendants().OfType<Popup>().First();
            var labels = popup.Child!.GetVisualDescendants().OfType<Button>().Select(button => button.Content?.ToString() ?? "");
            Assert.Contains(labels, label => label.Contains("март", StringComparison.OrdinalIgnoreCase));
            var input = picker.GetVisualDescendants().OfType<TextBox>().First();
            input.Focus(); input.Text = "15/";
            L.Current.Language = "ro"; Dispatcher.UIThread.RunJobs();
            Assert.Equal("15/", input.Text);
            Assert.Equal(date, picker.SelectedDate);
            Assert.Equal(0, selectedChanges);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void LocalizationDisposalStopsPresentationNotifications()
    {
        using var category = new FridgeCategory("Produse", 2, false);
        var changes = 0;
        category.PropertyChanged += (_, _) => changes++;
        L.Current.Language = "en";
        Assert.True(changes > 0);
        category.Dispose();
        changes = 0;
        L.Current.Language = "ru";
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task LocalizationClipboardAndSpreadsheetUseCurrentLanguageAndPreserveData()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        try
        {
            using var services = RedesignSamples.Services(configure: registrations =>
                registrations.AddSingleton<IFilePickerService>(new LocalizationFilePicker(path)));
            using var scope = services.CreateScope();
            var shopping = scope.ServiceProvider.GetRequiredService<ShoppingListViewModel>();
            shopping.Rows.Add(new ShoppingListRow { IngredientName = "Custom ingredient", NeededQty = 3, ToBuyQty = 2, UnitAbbreviation = "g", IsPurchased = true });
            L.Current.Language = "en";
            Assert.StartsWith("Shopping list", shopping.PlainText);
            L.Current.Language = "ru";
            Assert.StartsWith("Список покупок", shopping.PlainText);
            Assert.Contains("Custom ingredient", shopping.PlainText);
            await shopping.ExportExcelCommand.ExecuteAsync(null);
            using var workbook = new ClosedXML.Excel.XLWorkbook(path);
            var sheet = workbook.Worksheets.First();
            Assert.Equal("Ингредиент", sheet.Cell(1, 1).GetString());
            Assert.Equal("Купить", sheet.Cell(1, 4).GetString());
            Assert.Equal("Custom ingredient", sheet.Cell(2, 1).GetString());
            Assert.True(shopping.Rows[0].IsPurchased);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private sealed class LocalizationFilePicker(string path) : IFilePickerService
    {
        public Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter) => Task.FromResult<string?>(path);
        public Task<string?> OpenFileDialogAsync(string title, string filter) => Task.FromResult<string?>(null);
    }

    private sealed class LocalizationPantryRepository() : PantryRepository(new RedesignSamples.NoDatabase())
    {
        public int Reads;
        public override Task<IReadOnlyList<PantryItem>> GetPantryAsync(int userId, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<PantryItem>>([new() { IngredientID = 1, IngredientName = "Tomato", Quantity = 1, CategoryName = "Produse" }]);
        }
    }
}
