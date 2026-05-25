using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Data;
using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Planificare;
using MealPrepApp.Views.Planificare;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace MealPrepApp.ViewModels.Retete;

/// <summary>
/// The full-screen recipe detail view: header + description + instructions + ingredient lines,
/// with favorite toggle, edit, delete and back actions. Shown in the shell content region
/// (the tab strip stays visible). <see cref="RecipeId"/> must be set before navigation.
/// </summary>
public sealed partial class ReteteDetailViewModel : ViewModelBase, IAsyncLoadable
{
    private readonly RecipeRepository _recipes;
    private readonly FavoriteRepository _favorites;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IServiceProvider _services;

    /// <summary>The recipe to show. Set by the caller before <see cref="LoadAsync"/> runs.</summary>
    public int RecipeId { get; set; }

    [ObservableProperty]
    private RecipeFull? _recipe;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private ImageSource? _photoSource;

    public bool HasPhoto => PhotoSource is not null;

    partial void OnPhotoSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(HasPhoto));
    }

    public ReteteDetailViewModel(
        RecipeRepository recipes,
        FavoriteRepository favorites,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialog,
        IServiceProvider services)
    {
        _recipes = recipes;
        _favorites = favorites;
        _session = session;
        _navigation = navigation;
        _dialog = dialog;
        _services = services;
    }

    public async Task LoadAsync()
    {
        ClearError();
        IsBusy = true;
        try
        {
            Recipe = await _recipes.GetRecipeFullAsync(RecipeId);
            if (Recipe is null)
            {
                ErrorMessage = "Reteta nu a fost gasita.";
                return;
            }

            // No single-recipe "is favorite?" proc — derive it from the user's favorites list.
            var favorites = await _favorites.GetFavoriteRecipesAsync(_session.CurrentUserId, pageSize: 500);
            IsFavorite = favorites.Any(f => f.RecipeID == RecipeId);

            var photo = await _recipes.GetRecipePhotoAsync(RecipeId);
            PhotoSource = photo is null ? null : ToImageSource(photo.ImageData);
        }
        catch (AppDbException ex)
        {
            ErrorMessage = ex.FriendlyMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFavorite()
    {
        IsBusy = true;
        try
        {
            IsFavorite = await _favorites.ToggleFavoriteAsync(_session.CurrentUserId, RecipeId);
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Edit()
    {
        var editor = _services.GetRequiredService<ReteteEditorViewModel>();
        editor.RecipeId = RecipeId;
        return _navigation.NavigateToAsync(editor);
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Recipe is null)
            return;

        if (!_dialog.Confirm("Sterge reteta", $"Sterge reteta \"{Recipe.Title}\"?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipeAsync(RecipeId, _session.CurrentUserId);
            await GoBackToListAsync();
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ChoosePhoto()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Alege poza retetei",
            Filter = "Imagini|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Toate fisierele|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        try
        {
            var imageData = EncodePhotoForStorage(dialog.FileName);
            await _recipes.SetRecipePhotoAsync(RecipeId, _session.CurrentUserId, imageData, "image/jpeg");
            PhotoSource = ToImageSource(imageData);
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or InvalidOperationException)
        {
            _dialog.ShowError($"Poza nu a putut fi citita. Alege o imagine valida. Detalii: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeletePhoto()
    {
        if (!HasPhoto)
            return;

        if (!_dialog.Confirm("Sterge poza", "Sterge poza acestei retete?"))
            return;

        IsBusy = true;
        try
        {
            await _recipes.DeleteRecipePhotoAsync(RecipeId, _session.CurrentUserId);
            PhotoSource = null;
        }
        catch (AppDbException ex)
        {
            _dialog.ShowError(ex.FriendlyMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static byte[] EncodePhotoForStorage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1200;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static ImageSource ToImageSource(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Opens the shared plan-meal modal pre-filled with this recipe; the slot defaults
    /// to the recipe's own category (or Breakfast if it has none).</summary>
    [RelayCommand]
    private async Task AddToPlan()
    {
        if (Recipe is null)
            return;

        var recipe = new RecipeListItem
        {
            RecipeID = Recipe.RecipeID,
            Title = Recipe.Title,
            CategoryID = Recipe.CategoryID,
            CategoryName = Recipe.CategoryName,
            Servings = Recipe.Servings,
        };

        var vm = _services.GetRequiredService<PlanMealDialogViewModel>();
        await vm.InitForAddAsync(DateTime.Today, Recipe.CategoryID ?? MealSlots.DefaultSlotId, recipe);
        _dialog.ShowDialog<PlanMealDialog>(vm);
    }

    [RelayCommand]
    private Task Back() => GoBackToListAsync();

    private Task GoBackToListAsync() => _navigation.NavigateToAsync<ReteteListViewModel>();
}
