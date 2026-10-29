using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using MealPrepApp.Data;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.ViewModels.Retete;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace MealPrepApp.Tests;
public static class RedesignSamples
{
    public static ServiceProvider Services(IDialogService? dialogs=null, Action<IServiceCollection>? configure=null)
    {
        var services=new ServiceCollection();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:MealPrepDB"]="Server=unused;Database=unused;Integrated Security=true" }).Build();
        typeof(MealPrepApp.Desktop.App).GetMethod("ConfigureServices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{services,config});
        services.AddSingleton<IDbConnectionFactory,NoDatabase>(); services.AddSingleton<IDialogService>(dialogs ?? new NoDialogs());
        services.AddSingleton(new PresentationPreferences(Path.Combine(Path.GetTempPath(), $"mealprep-fixture-{Guid.NewGuid()}.json")));
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }
    public static void Render(string output)
    {
        Directory.CreateDirectory(output);
        Application.Current!.Resources["MotionDuration"]=TimeSpan.Zero; // deterministic settled-state captures
        Application.Current.Resources["MotionEnabled"]=false;
        using var services=Services(configure: s => s.AddSingleton<MealPrepApp.Data.Repositories.ShoppingListRepository,SampleShopping>()); using var scope=services.CreateScope();
        var provider=scope.ServiceProvider;
        provider.GetRequiredService<ISessionService>().SignIn(1,"Codrin");
        var home=provider.GetRequiredService<AcasaViewModel>();
        home.HasDrafts=true;
        foreach(var title in new[]{"Paste cu roșii și busuioc","Somon la cuptor","Salată de năut"}) home.RecentRecipes.Add(new RecentRecipe { RecipeID=1,Title=title, CategoryName="Prânz",Servings=2 });
        home.TodayMeals.Add(new MealPlanEntry {RecipeTitle="Terci cu mere și scorțișoară",CategoryName="Mic dejun",Servings=2});
        home.TodayMeals.Add(new MealPlanEntry {RecipeTitle="Paste cu roșii și busuioc",CategoryName="Prânz",Servings=2});
        var detail=provider.GetRequiredService<ReteteDetailViewModel>();
        detail.Recipe=new RecipeFull { RecipeID=1,UserID=1,Title="Paste cu roșii și busuioc",Description="O masă simplă, cu arome proaspete. Perfectă pentru serile din timpul săptămânii.", CategoryName="Prânz",AuthorUsername="Codrin",Servings=2,PrepTimeMinutes=10,CookTimeMinutes=20,Instructions="Fierbe pastele în apă cu sare, conform instrucțiunilor de pe ambalaj.\n\nÎncălzește uleiul și adaugă roșiile. Lasă sosul să fiarbă ușor, apoi amestecă pastele cu sosul și busuiocul.",Ingredients=new() { new(){IngredientName="Paste",Quantity=200,UnitAbbreviation="g"},new(){IngredientName="Roșii",Quantity=400,UnitAbbreviation="g"},new(){IngredientName="Ulei de măsline",Quantity=20,UnitAbbreviation="ml"},new(){IngredientName="Busuioc proaspăt",Quantity=5,UnitAbbreviation="g"} } };
        detail.Nutrition=new RecipeNutritionSummary {TotalCalories=980,TotalProteinGrams=26,TotalCarbsGrams=148,TotalFatGrams=28};
        var week=provider.GetRequiredService<PlanificareWeeklyViewModel>();
        foreach(var label in new[]{"Mic dejun","Prânz","Cină","Gustare"}) week.SlotHeaders.Add(label);
        for(int d=0;d<7;d++) week.Rows.Add(new WeekRowViewModel {Date=week.WeekStart.AddDays(d),DayLabel=MealSlots.DayRowLabel(week.WeekStart.AddDays(d)),Cells=Enumerable.Range(1,4).Select(slot=>new WeekCellViewModel {Date=week.WeekStart.AddDays(d),CategoryId=slot,Entries=slot==2 ? new[]{new MealPlanEntry {RecipeTitle=d%2==0 ? "Paste cu roșii" : "Salată de năut",CategoryName="Prânz",CategoryID=slot,Servings=2}}:Array.Empty<MealPlanEntry>()}).ToArray()});
        foreach(var row in week.Rows) foreach(var cell in row.Cells) foreach(var entry in cell.Entries) {entry.PlannedDate=cell.Date;week.AgendaEntries.Add(entry);}
        var dessert=new MealPlanEntry {RecipeTitle="Mere coapte",CategoryName="Desert",CategoryID=5,Servings=2,PlannedDate=week.WeekStart};
        week.SupplementaryEntries.Add(dessert);week.AgendaEntries.Add(dessert);
        var recipes=provider.GetRequiredService<ReteteListViewModel>();
        recipes.Categories.Add(new Category {CategoryID=0, Name="Toate categoriile"}); recipes.SelectedCategory=recipes.Categories[0];
        foreach(var title in new[]{"Paste cu roșii și busuioc","Somon la cuptor","Salată de năut","Supă de legume","Terci cu mere","Mere coapte"}) recipes.Recipes.Add(new RecipeListItem {RecipeID=1,Title=title,CategoryName="Prânz",Servings=2,PrepTimeMinutes=10,CookTimeMinutes=20});
        var pantry=provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.FrigiderViewModel>(); pantry.UseGrouping=true;
        var ingredients=provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.IngredienteListViewModel>(); ingredients.UseGrouping=true;
        foreach(var name in new[]{"Roșii","Busuioc","Morcovi","Ardei","Năut","Orez"})
        {
            pantry.Items.Add(new PantryItem {IngredientName=name,Quantity=250,UnitAbbreviation="g",CategoryName="Legume"});
            ingredients.Ingredients.Add(new Ingredient {Name=name,IngredientCategoryName="Legume",DefaultUnitAbbreviation="g"});
        }
        ingredients.Categories.Add(new IngredientCategory {Name="Toate categoriile"}); ingredients.SelectedCategory=ingredients.Categories[0];
        var shopping=provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.ShoppingListViewModel>();
        shopping.LoadAsync().GetAwaiter().GetResult();
        var reports=provider.GetRequiredService<MealPrepApp.ViewModels.Rapoarte.StatisticiLunareViewModel>();
        reports.Stats=new MonthlyStats {TotalMealsPlanned=14,DistinctRecipes=6,DistinctIngredients=12};reports.HasData=true;
        reports.TopRecipes.Add(new TopRecipe {Title="Paste cu roșii și busuioc",PlannedCount=4,CategoryName="Prânz"});
        reports.TopIngredients.Add(new TopIngredient {Name="Roșii",UsageCount=8});
        reports.SlotCounts.Add(new MealPrepApp.ViewModels.Rapoarte.SlotCount("Prânz",8));
        reports.SlotCounts.Add(new MealPrepApp.ViewModels.Rapoarte.SlotCount("Cină",6));
        var cooking=provider.GetRequiredService<CookingSession>();cooking.StartRecipe(detail.Recipe,3);cooking.Ingredients[0].IsChecked=true;
        var reportRoot=provider.GetRequiredService<MealPrepApp.ViewModels.Rapoarte.RapoarteRootViewModel>();reportRoot.CurrentSubView=reports;
        var plannerRoot=provider.GetRequiredService<PlanificareRootViewModel>();plannerRoot.CurrentSubView=week;
        foreach(var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark})
        {
            Application.Current!.RequestedThemeVariant=theme;
            foreach(var sample in new (string Name,Control View)[]{("home",new AcasaView{DataContext=home}),("recipe",new MealPrepApp.Desktop.Views.Retete.ReteteDetailView{DataContext=detail}),("planner",new MealPrepApp.Desktop.Views.Planificare.PlanificareRootView{DataContext=plannerRoot}),("components",new ComponentGallery()),
                ("recipes",new MealPrepApp.Desktop.Views.Retete.ReteteListView{DataContext=recipes}),
                ("pantry",new MealPrepApp.Desktop.Views.Ingrediente.FrigiderView{DataContext=pantry}),
                ("shopping",new MealPrepApp.Desktop.Views.Ingrediente.ShoppingListView{DataContext=shopping}),
                ("ingredients",new MealPrepApp.Desktop.Views.Ingrediente.IngredienteListView{DataContext=ingredients}),
                ("reports",new MealPrepApp.Desktop.Views.Rapoarte.RapoarteRootView{DataContext=reportRoot}),
                ("cooking",new MealPrepApp.Desktop.Views.Retete.CookingView{DataContext=new CookingViewModel(cooking)})})
            foreach(var size in new[]{new PixelSize(860,600),new PixelSize(1280,800),new PixelSize(1440,900)})
            {
                foreach(var scale in new[]{1d,1.5d,2d})
                {
                    var vm=provider.GetRequiredService<MealPrepApp.ViewModels.Shell.ShellViewModel>();
                    var window=new ShellWindow {Width=size.Width,Height=size.Height};
                    // Assign the fixture after Opened so production initialization cannot query or replace it.
                    window.Show();window.DataContext=vm;Dispatcher.UIThread.RunJobs();vm.CurrentView=sample.View;
                    vm.ActiveTab=sample.Name switch {"recipe" or "recipes" or "cooking"=>"Retete","planner"=>"Planificare","pantry"=>"Frigider","shopping"=>"Cumparaturi","ingredients"=>"Ingrediente","reports"=>"Rapoarte",_=>"Acasa"};
                    Dispatcher.UIThread.RunJobs();window.UpdateLayout();
                    using var bitmap=new RenderTargetBitmap(new PixelSize((int)(size.Width*scale),(int)(size.Height*scale)),new Vector(96*scale,96*scale));
                    bitmap.Render(window);bitmap.Save(Path.Combine(output,$"{sample.Name}-{theme}-{size.Width}-{scale*100:0}.png"));
                    vm.CurrentView=null;Dispatcher.UIThread.RunJobs();window.Close();
                }
            }
        }
        RenderIconFrames(output);
        RenderAuxiliary(provider,output);
        var manifest=Directory.GetFiles(output,"*.png").Order().Select(path => new {File=Path.GetFileName(path),Bytes=new FileInfo(path).Length,SHA256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()});
        File.WriteAllText(Path.Combine(output,"manifest.json"),System.Text.Json.JsonSerializer.Serialize(manifest,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
    }
    private static void RenderAuxiliary(IServiceProvider provider,string output)
    {
        foreach(var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark})
        foreach(var scale in new[]{1d,1.5d,2d})
        {
            Application.Current!.RequestedThemeVariant=theme;
            var login=new MealPrepApp.Desktop.Views.Auth.LoginWindow(provider.GetRequiredService<MealPrepApp.ViewModels.Auth.LoginViewModel>(),provider.GetRequiredService<MealPrepApp.ViewModels.Auth.RegisterViewModel>());
            var register=new Window {Width=520,Height=700,Content=new ScrollViewer {Content=new Border {Padding=new Thickness(32),Child=new MealPrepApp.Desktop.Views.Auth.RegisterView {DataContext=provider.GetRequiredService<MealPrepApp.ViewModels.Auth.RegisterViewModel>()}}}};
            register.Bind(Window.BackgroundProperty,register.GetResourceObservable("AppBackground"));
            var settings=new SettingsWindow();
            var connection=new ConnectionSetupWindow(new MealPrepApp.Desktop.Services.ConnectionSettingsStore(Path.Combine(Path.GetTempPath(),$"fixture-{Guid.NewGuid()}.json"),basePath:Path.Combine(Path.GetTempPath(),"missing-mealprep-fixture.json"),localPaths:Array.Empty<string>()));
            var ingredient=new MealPrepApp.Desktop.Views.Ingrediente.IngredientAddDialog {DataContext=provider.GetRequiredService<MealPrepApp.ViewModels.Ingrediente.IngredientAddDialogViewModel>()};
            var plan=new MealPrepApp.Desktop.Views.Planificare.PlanMealDialog {DataContext=provider.GetRequiredService<PlanMealDialogViewModel>()};
            var error=new MealPrepApp.Desktop.Views.Shared.MessageDialog();
            error.FindControl<TextBlock>("HeaderText")!.Text="Încărcarea nu a reușit";
            error.FindControl<TextBlock>("MessageBody")!.Text="Verifică conexiunea și încearcă din nou.";
            error.Configure(MealPrepApp.Desktop.Views.Shared.MessageDialogKind.Error,null,null);
            foreach(var sample in new (string Name,Window Window)[]{("login",login),("register",register),("settings",settings),("connection",connection),("ingredient-dialog",ingredient),("plan-dialog",plan),("error-dialog",error)})
            {
                var window=sample.Window;window.Show();Dispatcher.UIThread.RunJobs();window.UpdateLayout();
                using var bitmap=new RenderTargetBitmap(new PixelSize((int)(window.Bounds.Width*scale),(int)(window.Bounds.Height*scale)),new Vector(96*scale,96*scale));
                bitmap.Render(window);bitmap.Save(Path.Combine(output,$"{sample.Name}-{theme}-{scale*100:0}.png"));window.Close();
            }
        }
    }
    private static void RenderIconFrames(string output)
    {
        foreach(var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark})
        foreach(var progress in new[]{0d,.25,.5,.75,1d})
        {
            Application.Current!.RequestedThemeVariant=theme;
            var panel=new StackPanel {Orientation=Avalonia.Layout.Orientation.Horizontal,Spacing=12,Margin=new Thickness(16)};
            foreach(var kind in Enum.GetValues<MealPrepApp.Desktop.Controls.NavigationIconKind>())
            {
                var icon=new MealPrepApp.Desktop.Controls.AnimatedNavigationIcon {Kind=kind,Width=36,Height=36,Progress=progress};
                panel.Children.Add(new Border {Width=60,Height=60,Child=icon});
            }
            var window=new Window {Width=524,Height=92,Content=panel};
            window.Bind(Window.BackgroundProperty,window.GetResourceObservable("Surface"));
            window.Show();window.UpdateLayout();
            using var bitmap=new RenderTargetBitmap(new PixelSize(1572,276),new Vector(288,288));
            bitmap.Render(window);bitmap.Save(Path.Combine(output,$"icons-{theme}-{progress*100:0}.png"));window.Close();
        }
    }
    private sealed class SampleShopping() : MealPrepApp.Data.Repositories.ShoppingListRepository(new NoDatabase())
    {
        public override Task<IReadOnlyList<ShoppingSnapshot>> GetSnapshotsAsync(int userId,CancellationToken cancellationToken=default) =>
            Task.FromResult<IReadOnlyList<ShoppingSnapshot>>(new[]{new ShoppingSnapshot {SnapshotID=1,StartDate=DateTime.Today,EndDate=DateTime.Today.AddDays(7),CreatedAt=DateTime.UtcNow.Date.AddHours(10)}});
        public override Task<IReadOnlyList<ShoppingListRow>> GetSnapshotItemsAsync(int userId,int snapshotId,CancellationToken cancellationToken=default) =>
            Task.FromResult<IReadOnlyList<ShoppingListRow>>(new[]{"Roșii","Paste","Ulei de măsline"}.Select(name => new ShoppingListRow {ItemID=1,IngredientName=name,ToBuyQty=200,UnitAbbreviation="g",CategoryName="Pentru săptămâna aceasta",IsPurchased=name=="Paste"}).ToArray());
    }
    public sealed class NoDatabase:IDbConnectionFactory { public Microsoft.Data.SqlClient.SqlConnection Create()=>throw new InvalidOperationException("Sample mode has no database."); }
    public sealed class NoDialogs:IDialogService
    {
        public Task<bool> ConfirmAsync(string title,string message)=>Task.FromResult(false);
        public Task ShowErrorAsync(string message)=>Task.CompletedTask;
        public Task ShowInfoAsync(string title,string message)=>Task.CompletedTask;
        public Task<bool> ShowDialogAsync(object viewModel)=>Task.FromResult(false);
    }
}
