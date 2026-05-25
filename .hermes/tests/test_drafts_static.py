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


if __name__ == "__main__":
    tests = [
        test_draft_repository_registered_in_di,
        test_recipe_list_exposes_draft_filter_and_commands,
        test_recipe_editor_can_save_and_load_drafts,
        test_draft_controls_are_visible_in_xaml,
    ]
    for test in tests:
        test()
        print(f"PASS {test.__name__}")
