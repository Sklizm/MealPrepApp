using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Ingrediente;
using Microsoft.Extensions.DependencyInjection;
namespace MealPrepApp.ViewModels.Retete;
public sealed partial class ReteteEditorViewModel
{
    public bool RestoreOnLoad { get; set; }
    private bool _editorDisposed;
    public override void Dispose() { if(_editorDisposed)return;_editorDisposed=true;_recoveryDelay?.Cancel();_recoveryDelay?.Dispose();_recoveryDelay=null;base.Dispose(); }
    private EditorRecoveryService? _recovery;
    private bool _editorLoading;
    private CancellationTokenSource? _recoveryDelay;
    private readonly ObservableCollection<Ingredient> _ingredientCatalog = new();
    private Guid _saveOperation = Guid.NewGuid();
    private string? _saveFingerprint;
    [ObservableProperty] private byte[]? _photoBytes;
    private byte[]? _thumbnailBytes;
    private string PhotoMode { get; set; } = "Keep";
    [ObservableProperty] private bool _canRestoreRecovery;
    [ObservableProperty] private bool _recoverySaveFailed;
    [ObservableProperty] private string _importInput = "";
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private ImportedRecipe? _importReview;
    public ObservableCollection<RecipeIngredientRowViewModel> ImportReviewRows { get; } = new();
    public bool HasImportReview => ImportReview is not null;
    partial void OnImportReviewChanged(ImportedRecipe? value) => OnPropertyChanged(nameof(HasImportReview));
    private object ExtraSnapshot() => new { PhotoMode, PhotoHash = PhotoBytes is null ? null : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(PhotoBytes)), SourceText };
    private void InitializeRecovery() {
        _recovery = _services.GetService<EditorRecoveryService>();
        PropertyChanged += (_,e) => { if (e.PropertyName is nameof(Title) or nameof(Description) or nameof(Instructions) or nameof(PrepTimeMinutes) or nameof(CookTimeMinutes) or nameof(Servings) or nameof(SelectedCategory) or nameof(PhotoBytes) or nameof(SourceText)) ScheduleRecovery(); };
        IngredientRows.CollectionChanged += (_,e) => {
            if (e.NewItems is not null) foreach (RecipeIngredientRowViewModel row in e.NewItems) row.PropertyChanged += RowChanged;
            if (e.OldItems is not null) foreach (RecipeIngredientRowViewModel row in e.OldItems) row.PropertyChanged -= RowChanged;
            ScheduleRecovery();
        };
    }
    private void RowChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e) => ScheduleRecovery();
    private EditorRecovery CaptureRecovery() => new(RecipeId,DraftId,SelectedCategory?.CategoryID,Title,Description,Instructions,PrepTimeMinutes,CookTimeMinutes,Servings,
        IngredientRows.Select(r=>new DraftIngredientInput { IngredientID=r.SelectedIngredient?.IngredientID,UnitID=r.SelectedUnit?.UnitID,Quantity=r.Quantity,Notes=r.Notes,IngredientText=r.IngredientText }).ToArray(),
        _rowVersion,PhotoMode,PhotoBytes,_thumbnailBytes,SourceText,_saveOperation,_saveFingerprint);
    private void ScheduleRecovery() {
        if (_editorDisposed || _editorLoading || _recovery is null || IsBusy) return;
        _recoveryDelay?.Cancel(); _recoveryDelay?.Dispose(); _recoveryDelay=new();
        var token=_recoveryDelay.Token; var data=CaptureRecovery(); var revision=_recovery.ReserveRevision();
        _=SaveAfterIdle(data,revision,token);
    }
    private async Task SaveAfterIdle(EditorRecovery data,long revision,CancellationToken token) {
        try { await Task.Delay(500,token); if (!token.IsCancellationRequested) RecoverySaveFailed=!_recovery!.Write(data,revision); }
        catch (OperationCanceledException) { }
    }
    public void SaveRecoveryNow() { _recoveryDelay?.Cancel(); if (_recovery is not null) RecoverySaveFailed=!_recovery.Write(CaptureRecovery(),_recovery.ReserveRevision()); }
    [RelayCommand] private void RestoreRecovery() {
        if (_recovery?.Read() is not { } data) return;
        _editorLoading=true;
        RecipeId=data.RecipeID; DraftId=data.DraftID; Title=data.Title; Description=data.Description; Instructions=data.Instructions;
        PrepTimeMinutes=data.PrepTimeMinutes; CookTimeMinutes=data.CookTimeMinutes; Servings=data.Servings; SelectedCategory=Categories.FirstOrDefault(c=>c.CategoryID==data.CategoryID);
        _rowVersion=data.RowVersion; PhotoMode=data.PhotoMode; PhotoBytes=data.Photo; _thumbnailBytes=data.Thumbnail; SourceText=data.SourceText; _saveOperation=data.SaveOperation; _saveFingerprint=data.SaveFingerprint;
        IngredientRows.Clear();
        foreach(var line in data.Rows) { var row=NewRow(); row.SelectedIngredient=_allIngredients.FirstOrDefault(i=>i.IngredientID==line.IngredientID); row.SelectedUnit=_allUnits.FirstOrDefault(u=>u.UnitID==line.UnitID); row.Quantity=line.Quantity; row.Notes=line.Notes; row.IngredientText=line.IngredientText??row.SelectedIngredient?.Name??""; IngredientRows.Add(row); }
        CanRestoreRecovery=false; _editorLoading=false;
        OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(IsDraft)); OnPropertyChanged(nameof(ScreenTitle));
    }
    [RelayCommand] private void DismissRecovery() { _recovery?.Delete(); CanRestoreRecovery=false; }
    private Guid OperationForSave(RecipeBundleInput input) {
        var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        if (_saveFingerprint is not null && fingerprint!=_saveFingerprint) _saveOperation=Guid.NewGuid();
        _saveFingerprint=fingerprint; SaveRecoveryNow(); return _saveOperation;
    }
    [RelayCommand] private async Task SelectPhoto() {
        var picker=_services.GetService<IFilePickerService>(); if(picker is null)return;
        var path=await picker.OpenFileDialogAsync(L.Text("Text.Photo"),"Images|*.jpg;*.jpeg;*.png;*.webp"); if(path is null)return;
        try { var image=await RecipeImageProcessor.LoadAsync(path); PhotoMode="Set"; _thumbnailBytes=image.Thumbnail; PhotoBytes=image.Image; ScheduleRecovery(); }
        catch(Exception ex) { AppDiagnostics.Record(ex); ErrorContent=L.Message("Text.InvalidImageFormat"); }
    }
    [RelayCommand] private void RemovePhoto() { PhotoMode="Delete"; _thumbnailBytes=null; PhotoBytes=null; ScheduleRecovery(); }
    [RelayCommand] private async Task CreateInlineIngredient(RecipeIngredientRowViewModel? target) {
        var editor=_services.GetRequiredService<IngredientAddDialogViewModel>(); await editor.LoadAsync(); editor.Name=target?.IngredientText??"";
        if(!await _dialog.ShowDialogAsync(editor))return;
        var fresh=await _ingredients.GetIngredientsAsync(); _ingredientCatalog.Clear(); foreach(var item in fresh)_ingredientCatalog.Add(item);
        if(target is not null)target.SelectedIngredient=fresh.FirstOrDefault(i=>i.IngredientID==editor.CreatedIngredientID);
    }
    [RelayCommand] private async Task ReviewImport() {
        ClearError(); IsBusy=true;
        try {
            ImportedRecipe parsed;
            if(Uri.TryCreate(ImportInput.Trim(),UriKind.Absolute,out var uri)) {
                var html=await RecipePageFetcher.FetchAsync(uri); parsed=RecipeImportParser.ParseStructuredPage(html,_allIngredients,_allUnits);
            } else parsed=RecipeImportParser.ParsePaste(ImportInput,_allIngredients,_allUnits);
            ImportReview=parsed; ImportReviewRows.Clear();
            foreach(var line in parsed.Ingredients) {
                var row=new RecipeIngredientRowViewModel(_allIngredients,_allUnits) { IngredientText=line.IngredientText,Quantity=line.Quantity??0 };
                row.SelectedIngredient=_allIngredients.FirstOrDefault(i=>i.IngredientID==line.IngredientID);
                row.SelectedUnit=_allUnits.FirstOrDefault(u=>u.UnitID==line.UnitID);
                row.RemoveRequested+=(_,_)=>ImportReviewRows.Remove(row); ImportReviewRows.Add(row);
            }
        } catch(Exception ex) { AppDiagnostics.Record(ex); ErrorContent=L.Message("UX.ImportFailedPaste"); }
        finally { IsBusy=false; }
    }
    [RelayCommand] private async Task ApplyImport() {
        if(ImportReview is not { } import)return;
        if(IsDirty && !await _dialog.ConfirmAsync(L.Message("UX.ReviewImport"),L.Message("UX.ReplaceEditor")))return;
        _editorLoading=true; Title=import.Title??""; Description=import.Description; Instructions=import.Instructions;
        Servings=import.Servings; PrepTimeMinutes=import.PrepMinutes; CookTimeMinutes=import.CookMinutes; SourceText=ImportInput+Environment.NewLine+import.Original;
        IngredientRows.Clear(); foreach(var source in ImportReviewRows) {
            var row=NewRow();row.SelectedIngredient=source.SelectedIngredient;row.SelectedUnit=source.SelectedUnit;row.Quantity=source.Quantity;row.Notes=source.Notes;row.IngredientText=source.IngredientText;IngredientRows.Add(row);
        }
        ImportReview=null;ImportReviewRows.Clear();_editorLoading=false;SaveRecoveryNow();
    }
}
