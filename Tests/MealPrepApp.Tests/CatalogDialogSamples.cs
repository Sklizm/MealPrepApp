using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Desktop.Localization;
using MealPrepApp.Desktop.Services;
using MealPrepApp.Desktop.Views;
using MealPrepApp.Desktop.Views.Ingrediente;
using MealPrepApp.Desktop.Views.Shared;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Ingrediente;
using Microsoft.Extensions.DependencyInjection;

namespace MealPrepApp.Tests;

/// <summary>Database-free catalog, dialog and popup captures, including localized validation.</summary>
public static class CatalogDialogSamples
{
    public static void Render(string output)
    {
        Application.Current!.Resources["MotionDuration"]=TimeSpan.Zero;
        Application.Current.Resources["MotionEnabled"]=false;
        using var services=RedesignSamples.Services(configure:s=>
        {
            s.AddSingleton<IngredientRepository>(new CatalogFixtureRepository());
            s.AddSingleton<LookupRepository>(new CatalogFixtureLookups());
        });
        foreach(var language in new[]{"en","ru","ro"})
        foreach(var theme in new[]{ThemeVariant.Light,ThemeVariant.Dark})
        {
            L.Current.Language=language;
            Application.Current.RequestedThemeVariant=theme;
            var directory=Path.Combine(output,language); Directory.CreateDirectory(directory);
            foreach(var scale in new[]{1d,1.5d,2d})
            {
                using var scope=services.CreateScope(); var provider=scope.ServiceProvider;
                var catalog=provider.GetRequiredService<IngredienteListViewModel>(); catalog.LoadAsync().GetAwaiter().GetResult();
                foreach(var size in new[]{new Size(860,600),new Size(1280,800),new Size(1440,900)})
                {
                    var window=new Window {Width=size.Width,Height=size.Height,Content=new IngredienteListView{DataContext=catalog}};
                    window.Show(); Save(window,$"ingredients-{theme}-{size.Width}-{scale*100:0}"); window.Close();
                }
                var add=provider.GetRequiredService<IngredientAddDialogViewModel>(); add.LoadAsync().GetAwaiter().GetResult();
                var nutrition=provider.GetRequiredService<IngredientNutritionDialogViewModel>(); nutrition.IngredientName="Apple";
                nutrition.Units.Add(new Unit {UnitID=1,Name="Gram",Abbreviation="g"}); nutrition.SelectedUnit=nutrition.Units[0];
                var message=new MessageDialog(); message.FindControl<TextBlock>("HeaderText")!.Text=L.Text("Text.Delete");
                message.FindControl<TextBlock>("MessageBody")!.Text=L.Text("Text.DeleteThisRecipeSPhoto");
                message.Configure(MessageDialogKind.Confirm,L.Text("Text.Delete"),L.Text("Text.Cancel"),true);
                var copy=new MessageDialog(); copy.FindControl<TextBlock>("HeaderText")!.Text=L.Text("Text.WeekCopyPreview");
                copy.FindControl<TextBlock>("MessageBody")!.Text=L.Text("Text.CopyWeekStartingOn");copy.Configure(MessageDialogKind.Confirm,L.Text("Text.CopyWeek"),L.Text("Text.Cancel"));
                var connection=new ConnectionSetupWindow(new ConnectionSettingsStore(Path.Combine(Path.GetTempPath(),$"catalog-fixture-{Guid.NewGuid()}.json"),basePath:Path.Combine(Path.GetTempPath(),"missing-fixture.json"),localPaths:[]));
                var plan=new MealPrepApp.Desktop.Views.Planificare.PlanMealDialog {DataContext=provider.GetRequiredService<MealPrepApp.ViewModels.Planificare.PlanMealDialogViewModel>()};
                foreach(var (name,window) in new (string,Window)[]{
                    ("ingredient",new IngredientAddDialog{DataContext=add}),
                    ("nutrition",new IngredientNutritionDialog{DataContext=nutrition}),
                    ("pantry",new PantryItemDialog{DataContext=provider.GetRequiredService<PantryItemDialogViewModel>()}),
                    ("meal",plan),
                    ("password",new MealPrepApp.Desktop.Views.Auth.ChangePasswordDialog{DataContext=provider.GetRequiredService<MealPrepApp.ViewModels.Auth.ChangePasswordViewModel>()}),
                    ("reset-password",new MealPrepApp.Desktop.Views.Auth.ForgotPasswordDialog{DataContext=provider.GetRequiredService<MealPrepApp.ViewModels.Auth.ForgotPasswordViewModel>()}),
                    ("confirmation",message),("copy-week",copy),("unsaved",DesktopDialogService.CreateLeaveDialog()),
                    ("settings",new SettingsWindow(provider.GetRequiredService<PresentationPreferences>())),("connection",connection)})
                {
                    window.Show(); Save(window,$"{name}-{theme}-{scale*100:0}");
                    if(name=="ingredient") { add.SaveCommand.Execute(null); Save(window,$"ingredient-validation-{theme}-{scale*100:0}"); }
                    if(name=="nutrition") { nutrition.BasisQuantity=0; nutrition.SaveCommand.Execute(null); Save(window,$"nutrition-validation-{theme}-{scale*100:0}"); }
                    window.Close();
                }
                var picker=new LocalizedCalendarDatePicker{SelectedDate=new DateTime(2026,3,15),Width=220};
                var host=new Window {Width=520,Height=480,Content=picker};host.Show();picker.IsDropDownOpen=true;Pump();
                var popup=picker.GetVisualDescendants().OfType<Popup>().First();var calendar=popup.Child!.GetVisualDescendants().OfType<Calendar>().Single();
                foreach(var mode in new[]{CalendarMode.Month,CalendarMode.Year,CalendarMode.Decade})
                { calendar.DisplayMode=mode;Pump();Save(popup.Child!,$"calendar-{mode}-{theme}-{scale*100:0}"); }
                host.Close();
                var combo=new ComboBox {ItemsSource=new[]{L.Text("Text.AllCategories"),L.Text("Text.Uncategorized"),"Custom collection"},Width=280,SelectedIndex=0};
                host=new Window {Width=440,Height=320,Content=combo};host.Show();combo.IsDropDownOpen=true;Pump();
                popup=combo.GetVisualDescendants().OfType<Popup>().First();Save(popup.Child!,$"dropdown-{theme}-{scale*100:0}");host.Close();
                var autocomplete=new AutoCompleteBox {ItemsSource=new[]{"Apple","Apricot","Avocado"},MinimumPrefixLength=0,Width=280,Text="A"};
                host=new Window {Width=440,Height=320,Content=autocomplete};host.Show();autocomplete.IsDropDownOpen=true;Pump();
                popup=autocomplete.GetVisualDescendants().OfType<Popup>().First();if(popup.Child?.Bounds.Width>0) Save(popup.Child,$"autocomplete-{theme}-{scale*100:0}");host.Close();
                var target=new Button {Content=L.Text("Text.Nutrition"),Width=180,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center,VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center};
                host=new Window{Width=440,Height=320,Content=target}; host.Show();
                var menu=new ContextMenu {ItemsSource=new[]{new MenuItem {Header=L.Text("Text.Nutrition")},new MenuItem {Header=L.Text("Text.Delete"),Classes={"Destructive"}}}};
                target.ContextMenu=menu; menu.Open(target);Pump(); Save(menu,$"context-menu-{theme}-{scale*100:0}");menu.Close();
                var tip=new ToolTip {Content=L.Text("Text.Nutrition")};ToolTip.SetTip(target,tip);ToolTip.SetIsOpen(target,true);Pump();
                if(tip!=null) Save(tip,$"tooltip-{theme}-{scale*100:0}");host.Close();

                void Save(Control visual,string name)
                {
                    Pump(); visual.UpdateLayout();
                    using var bitmap=new RenderTargetBitmap(new PixelSize(Math.Max(1,(int)(visual.Bounds.Width*scale)),Math.Max(1,(int)(visual.Bounds.Height*scale))),new Vector(96*scale,96*scale));
                    bitmap.Render(visual);bitmap.Save(Path.Combine(directory,name+".png"));
                }
            }
            Console.WriteLine($"Rendered catalog/dialog fixtures: {language}, {theme}");
        }
    }
    private static void Pump() { Dispatcher.UIThread.RunJobs(); }
}
