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


if __name__ == "__main__":
    tests = [
        test_draft_repository_registered_in_di,
        test_recipe_list_exposes_draft_filter_and_commands,
        test_recipe_editor_can_save_and_load_drafts,
        test_draft_controls_are_visible_in_xaml,
        test_recipe_photos_are_wired_in_detail_screen,
        test_recipe_list_cards_show_photo_thumbnails,
        test_standalone_loading_window_before_shell,
    ]
    for test in tests:
        test()
        print(f"PASS {test.__name__}")
