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
    public static ServiceProvider Services(IDialogService? dialogs=null)
    {
        var services=new ServiceCollection();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:MealPrepDB"]="Server=unused;Database=unused;Integrated Security=true" }).Build();
        typeof(MealPrepApp.Desktop.App).GetMethod("ConfigureServices",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.Invoke(null,new object[]{services,config});
        services.AddSingleton<IDbConnectionFactory,NoDatabase>(); services.AddSingleton<IDialogService>(dialogs ?? new NoDialogs());
        return services.BuildServiceProvider();
    }
    public static void Render(string output)
    {
        Directory.CreateDirectory(output);
        Application.Current!.Resources["MotionDuration"]=TimeSpan.Zero; // deterministic settled-state captures
        using var services=Services(); using var scope=services.CreateScope();
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
        foreach(var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark})
        {
            Application.Current!.RequestedThemeVariant=theme;
            foreach(var sample in new (string Name,Control View)[]{("home",new AcasaView{DataContext=home}),("recipe",new MealPrepApp.Desktop.Views.Retete.ReteteDetailView{DataContext=detail}),("planner",new MealPrepApp.Desktop.Views.Planificare.PlanificareWeeklyView{DataContext=week}),("components",new ComponentGallery())})
            foreach(var size in new[]{new PixelSize(860,600),new PixelSize(1280,800),new PixelSize(1440,900)})
            {
                foreach(var scale in new[]{1d,1.5d,2d})
                {
                    var vm=provider.GetRequiredService<MealPrepApp.ViewModels.Shell.ShellViewModel>();
                    var window=new ShellWindow {Width=size.Width,Height=size.Height,DataContext=vm};
                    window.Show();Dispatcher.UIThread.RunJobs();vm.CurrentView=sample.View;vm.ActiveTab=sample.Name switch {"recipe"=>"Retete","planner"=>"Planificare",_=>"Acasa"};
                    Dispatcher.UIThread.RunJobs();window.UpdateLayout();
                    using var bitmap=new RenderTargetBitmap(new PixelSize((int)(size.Width*scale),(int)(size.Height*scale)),new Vector(96*scale,96*scale));
                    bitmap.Render(window);bitmap.Save(Path.Combine(output,$"{sample.Name}-{theme}-{size.Width}-{scale*100:0}.png"));
                    vm.CurrentView=null;Dispatcher.UIThread.RunJobs();window.Close();
                }
            }
        }
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
