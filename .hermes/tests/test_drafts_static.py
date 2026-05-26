from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def test_draft_repository_registered_in_di():
    app = read("App/MealPrepApp/App.xaml.cs")
    assert "services.AddSingleton<DraftRepository>();" in app


def test_recipe_list_exposes_draft_filter_and_commands():
    vm = read("App/MealPrepApp/ViewModels/Retete/ReteteListViewModel.cs")
    assert "DraftRepository" in vm
    assert "ObservableCollection<RecipeDraftListItem> Drafts" in vm
    assert "ShowDrafts" in vm
    assert "OpenDraft" in vm
    assert "DeleteDraft" in vm


def test_recipe_editor_can_save_and_load_drafts():
    vm = read("App/MealPrepApp/ViewModels/Retete/ReteteEditorViewModel.cs")
    assert "DraftRepository" in vm
    assert "public int? DraftId" in vm
    assert "SaveDraft" in vm
    assert "PopulateFromDraftAsync" in vm
    assert "DraftIngredientInput" in vm


def test_draft_controls_are_visible_in_xaml():
    list_xaml = read("App/MealPrepApp/Views/Retete/ReteteListView.xaml")
    editor_xaml = read("App/MealPrepApp/Views/Retete/ReteteEditorView.xaml")
    assert "Drafts" in list_xaml
    assert "OpenDraftCommand" in list_xaml
    assert "DeleteDraftCommand" in list_xaml
    assert "Salveaza ca draft" in editor_xaml
    assert "SaveDraftCommand" in editor_xaml


def test_recipe_photos_are_wired_in_detail_screen():
    repo = read("App/MealPrepApp/Data/Repositories/RecipeRepository.cs")
    vm = read("App/MealPrepApp/ViewModels/Retete/ReteteDetailViewModel.cs")
    xaml = read("App/MealPrepApp/Views/Retete/ReteteDetailView.xaml")
    assert "SetRecipePhotoAsync" in repo
    assert "GetRecipePhotoAsync" in repo
    assert "DeleteRecipePhotoAsync" in repo
    assert "PhotoSource" in vm
    assert "private async Task ChoosePhoto" in vm
    assert "private async Task DeletePhoto" in vm
    assert "Adauga poza" in xaml
    assert "Schimba poza" in xaml
    assert "Sterge poza" in xaml
    assert "PhotoSource" in xaml
    assert "MaxHeight" not in xaml


def test_recipe_list_cards_show_photo_thumbnails():
    model = read("App/MealPrepApp/Models/Recipe.cs")
    vm = read("App/MealPrepApp/ViewModels/Retete/ReteteListViewModel.cs")
    xaml = read("App/MealPrepApp/Views/Retete/ReteteListView.xaml")
    app = read("App/MealPrepApp/App.xaml")
    converter = read("App/MealPrepApp/Converters/CommonConverters.cs")
    assert "byte[]? PhotoData" in model
    assert "LoadRecipePhotosAsync" in vm
    assert "GetRecipePhotoAsync" in vm
    assert "ByteArrayToImageSource" in app
    assert "ByteArrayToImageSourceConverter" in converter
    assert "Source=\"{Binding PhotoData, Converter={StaticResource ByteArrayToImageSource}}\"" in xaml
    assert "RecipeCardPhoto" in xaml


def test_standalone_loading_window_before_shell():
    app = read("App/MealPrepApp/App.xaml.cs")
    login_codebehind = read("App/MealPrepApp/Views/Auth/LoginWindow.xaml.cs")
    shell_codebehind = read("App/MealPrepApp/Views/ShellWindow.xaml.cs")
    shell_vm = read("App/MealPrepApp/ViewModels/Shell/ShellViewModel.cs")
    splash_xaml = read("App/MealPrepApp/Views/StartupLoadingWindow.xaml")
    splash_codebehind = read("App/MealPrepApp/Views/StartupLoadingWindow.xaml.cs")
    shell_xaml = read("App/MealPrepApp/Views/ShellWindow.xaml")
    assert "services.AddTransient<StartupLoadingWindow>();" in app
    assert "GetRequiredService<StartupLoadingWindow>()" in login_codebehind
    assert "loadingWindow.Show();" in login_codebehind
    assert "Hide();" in login_codebehind
    assert "Task.Delay(TimeSpan.FromMilliseconds(3500))" in login_codebehind
    assert "Task.WhenAll(shell.InitializeBeforeShowAsync(), minimumDisplay)" in login_codebehind
    assert login_codebehind.index("await Task.WhenAll(shell.InitializeBeforeShowAsync(), minimumDisplay)") < login_codebehind.index("shell.Show();")
    assert "InitializeBeforeShowAsync" in shell_codebehind
    assert "Loaded += OnLoaded" not in shell_codebehind
    assert "private bool _isInitializing" not in shell_vm
    assert "LoadingOverlay" not in shell_xaml
    assert "StartupLoadingWindow" in splash_codebehind
    assert "x:Name=\"StartupSpinnerRotate\"" in splash_xaml
    assert "RepeatBehavior=\"Forever\"" in splash_xaml
    assert "Pregatim tabloul de bord" in splash_xaml


def test_nutrition_foundation_is_proc_backed_and_wired_to_recipe_detail():
    run_all = read("Database/run_all.sql")
    conversions = read("Database/17_unit_conversions.sql")
    nutrition_table = read("Database/18_ingredient_nutrition.sql")
    proc = read("Database/procs/14_nutrition.sql")
    app = read("App/MealPrepApp/App.xaml.cs")
    models = read("App/MealPrepApp/Models/Nutrition.cs")
    repo = read("App/MealPrepApp/Data/Repositories/NutritionRepository.cs")
    ingredient_vm = read("App/MealPrepApp/ViewModels/Ingrediente/IngredientNutritionDialogViewModel.cs")
    ingredient_list_vm = read("App/MealPrepApp/ViewModels/Ingrediente/IngredienteListViewModel.cs")
    ingredient_list_xaml = read("App/MealPrepApp/Views/Ingrediente/IngredienteListView.xaml")
    nutrition_dialog = read("App/MealPrepApp/Views/Ingrediente/IngredientNutritionDialog.xaml")
    detail_vm = read("App/MealPrepApp/ViewModels/Retete/ReteteDetailViewModel.cs")
    detail_xaml = read("App/MealPrepApp/Views/Retete/ReteteDetailView.xaml")
    assert ":r 17_unit_conversions.sql" in run_all
    assert ":r 18_ingredient_nutrition.sql" in run_all
    assert ":r procs/14_nutrition.sql" in run_all
    assert "CREATE TABLE dbo.UnitConversions" in conversions
    assert "UQ_UnitConversions_From_To" in conversions
    assert "CREATE TABLE dbo.IngredientNutrition" in nutrition_table
    assert "CK_IngredientNutrition_Calories" in nutrition_table
    assert "CREATE OR ALTER PROCEDURE dbo.sp_GetIngredientNutrition" in proc
    assert "CREATE OR ALTER PROCEDURE dbo.sp_SetIngredientNutrition" in proc
    assert "CREATE OR ALTER PROCEDURE dbo.sp_GetRecipeNutrition" in proc
    assert "MissingNutritionCount" in proc
    assert "UnconvertibleIngredientCount" in proc
    assert "services.AddSingleton<NutritionRepository>();" in app
    assert "services.AddTransient<IngredientNutritionDialogViewModel>();" in app
    assert "services.AddTransient<IngredientNutritionDialog>();" in app
    assert "public sealed class IngredientNutrition" in models
    assert "public sealed class RecipeNutritionSummary" in models
    assert "GetRecipeNutritionAsync" in repo
    assert "SetIngredientNutritionAsync" in repo
    assert "SaveAsync" in ingredient_vm
    assert "EditNutrition" in ingredient_list_vm
    assert "Nutritie" in ingredient_list_xaml
    assert "IngredientNutritionDialog" in nutrition_dialog
    assert "NutritionRepository" in detail_vm
    assert "Nutrition" in detail_vm
    assert "Nutritie estimata" in detail_xaml
    assert "MissingNutritionCount" in detail_xaml


def test_forgot_password_flow_is_backed_by_proc_and_wired_to_login():
    proc = read("Database/procs/01_users.sql")
    repo = read("App/MealPrepApp/Data/Repositories/UserRepository.cs")
    mapper = read("App/MealPrepApp/Data/DbExceptionMapper.cs")
    app = read("App/MealPrepApp/App.xaml.cs")
    login_vm = read("App/MealPrepApp/ViewModels/Auth/LoginViewModel.cs")
    forgot_vm = read("App/MealPrepApp/ViewModels/Auth/ForgotPasswordViewModel.cs")
    login_xaml = read("App/MealPrepApp/Views/Auth/LoginView.xaml")
    login_codebehind = read("App/MealPrepApp/Views/Auth/LoginWindow.xaml.cs")
    dialog_xaml = read("App/MealPrepApp/Views/Auth/ForgotPasswordDialog.xaml")
    dialog_codebehind = read("App/MealPrepApp/Views/Auth/ForgotPasswordDialog.xaml.cs")
    assert "CREATE OR ALTER PROCEDURE dbo.sp_ResetForgottenPassword" in proc
    assert "@UsernameOrEmail NVARCHAR(255)" in proc
    assert "@Email           NVARCHAR(255)" in proc
    assert "THROW 50005" in proc
    assert "PASSWORD_RESET" in proc
    assert "FailedLoginCount = 0" in proc
    assert "LockedUntil = NULL" in proc
    assert "ResetForgottenPasswordAsync" in repo
    assert "sp_ResetForgottenPassword" in repo
    assert "50005" in mapper
    assert "Nu am gasit un cont cu aceste date." in mapper
    assert "services.AddTransient<ForgotPasswordViewModel>();" in app
    assert "services.AddTransient<ForgotPasswordDialog>();" in app
    assert "ForgotPasswordRequested" in login_vm
    assert "ForgotPasswordCommand" in login_xaml
    assert "Ai uitat parola?" in login_xaml
    assert "ShowForgotPasswordDialog" in login_codebehind
    assert "GetRequiredService<ForgotPasswordDialog>()" in login_codebehind
    assert "ResetAsync" in forgot_vm
    assert "ResetForgottenPasswordAsync" in forgot_vm
    assert "Resetare parola" in dialog_xaml
    assert "Email cont" in dialog_xaml
    assert "Reseteaza parola" in dialog_xaml
    assert "PrefillIdentifier" in dialog_codebehind


if __name__ == "__main__":
    tests = [
        test_draft_repository_registered_in_di,
        test_recipe_list_exposes_draft_filter_and_commands,
        test_recipe_editor_can_save_and_load_drafts,
        test_draft_controls_are_visible_in_xaml,
        test_recipe_photos_are_wired_in_detail_screen,
        test_recipe_list_cards_show_photo_thumbnails,
        test_standalone_loading_window_before_shell,
        test_nutrition_foundation_is_proc_backed_and_wired_to_recipe_detail,
        test_forgot_password_flow_is_backed_by_proc_and_wired_to_login,
    ]
    for test in tests:
        test()
        print(f"PASS {test.__name__}")
