using System.Globalization;
using System.Text.Json;
using System.Collections.ObjectModel;
using MealPrepApp.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels;

public sealed partial class AssistantIngredientRow : LocalizedObservableObject
{
    [ObservableProperty] private string _original="";
    [ObservableProperty] private string _name="";
    [ObservableProperty] private Ingredient? _ingredient;
    [ObservableProperty] private Unit? _unit;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInvalidQuantity))]
    private decimal? _quantity;
    [ObservableProperty] private string? _quantityInput;
    partial void OnQuantityInputChanged(string? value)
    {
        Quantity = PresentationValues.ReadDecimal(value);
    }

    public bool HasInvalidQuantity => Quantity is not { } quantity || !AiValidation.Quantity(quantity);
    [ObservableProperty] private LocalizedMessage? _problem;
    [LocalizedProperty] public string? ProblemText => Problem?.ToString();
    partial void OnProblemChanged(LocalizedMessage? value) => OnPropertyChanged(nameof(ProblemText));
    [ObservableProperty] private string? _notes;
    public ObservableCollection<Ingredient> Ingredients {get;}=[];
    public ObservableCollection<Unit> Units {get;}=[];
    [RelayCommand]
    private void ClearIngredientMapping() => Ingredient = null;
    public void Populate(AiIngredient line,AiContext context){Original=line.Original;Name=line.Name;QuantityInput=line.Quantity?.ToString("0.####", CultureInfo.CurrentCulture);Quantity=line.Quantity;Notes=line.Notes;foreach(var i in context.Ingredients)Ingredients.Add(i);foreach(var u in context.Units)Units.Add(u);Ingredient=Ingredients.FirstOrDefault(i=>i.IngredientID==line.IngredientId);Unit=Units.FirstOrDefault(u=>u.UnitID==line.UnitId);}
    public AiIngredient Capture()=>new(Original,Ingredient?.Name??Name,Quantity,Ingredient?.IngredientID,Unit?.UnitID,Unit?.Abbreviation,Notes);
}
public sealed partial class AssistantRecipeRow : LocalizedObservableObject
{
    [ObservableProperty] private string _title="";
    [ObservableProperty] private string _description="";
    [ObservableProperty] private string _instructions="";
    [ObservableProperty] private int? _servings=2;
    [ObservableProperty] private int? _prepMinutes;
    [ObservableProperty] private int? _cookMinutes;
    [ObservableProperty] private Category? _category;
    [ObservableProperty] private string? _source;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PhotoPreview))]
    private ProcessedRecipeImage? _photo;
    public byte[]? PhotoPreview => Photo?.Thumbnail;

    [RelayCommand]
    private void SkipPhoto() => Photo = null;

    [ObservableProperty] private int? _publishedId;
    public byte[]? Version {get;set;}
    public int? OriginalRecipeId {get;set;}
    public Guid OperationId {get;set;}=Guid.NewGuid();
    public Guid? OriginJobId { get; set; }
    public int? ResultIndex { get; set; }
    public ObservableCollection<Category> Categories {get;}=[];
    public ObservableCollection<AssistantIngredientRow> Ingredients {get;}=[];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnresolvedSummary))]
    private int _unresolvedRows;

    [LocalizedProperty]
    public string UnresolvedSummary => L.Text("Ai.UnresolvedRows", UnresolvedRows);

    [ObservableProperty]
    private bool _canEdit = true;

    public void ValidateRows()
    {
        var duplicates = Ingredients.Where(row => row.Ingredient is not null)
            .GroupBy(row => row.Ingredient!.IngredientID)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key).ToHashSet();
        foreach (var row in Ingredients)
        {
            row.Problem = row.Ingredient is null || row.Unit is null ? L.Message("Ai.Error.MappingRequired")
                : duplicates.Contains(row.Ingredient.IngredientID) ? L.Message("Ai.Error.DuplicateRecipeIngredient", row.Ingredient.Name)
                : row.HasInvalidQuantity ? L.Message("Ai.Error.QuantityField") : null;
        }
        UnresolvedRows = Ingredients.Count(row => row.Problem is not null);
    }

    public AiRecipe Capture()=>new(Title,Description,Instructions,Servings,PrepMinutes,CookMinutes,Category?.CategoryID,Ingredients.Select(i=>i.Capture()).ToArray(),Source,Photo);
    public void Populate(AiRecipe recipe,AiContext context){Title=recipe.Title;Description=recipe.Description;Instructions=recipe.Instructions;Servings=recipe.Servings;PrepMinutes=recipe.PrepMinutes;CookMinutes=recipe.CookMinutes;Source=recipe.Source;Photo=recipe.Photo;foreach(var c in context.Categories)Categories.Add(c);Category=Categories.FirstOrDefault(c=>c.CategoryID==recipe.CategoryId);foreach(var i in recipe.Ingredients){var row=new AssistantIngredientRow();row.Populate(i,context);Ingredients.Add(row);}}
}
public sealed partial class AssistantMealRow : ObservableObject
{
    [ObservableProperty] private DateTimeOffset? _date;
    [ObservableProperty] private Category? _slot;
    [ObservableProperty] private AiCandidate? _recipe;
    [ObservableProperty] private decimal _portions=2;
    [ObservableProperty] private string _batchKey="";
    [ObservableProperty] private string _reason="";
    [ObservableProperty] private bool _pinned;
    public int? DraftIndex {get;set;}
    public int? PreparedBatchId {get;set;}
    public ObservableCollection<Category> Slots {get;}=[];
    public ObservableCollection<AiCandidate> Recipes {get;}=[];
    public AiPlanMeal Capture()=>new(DateOnly.FromDateTime((Date??DateTimeOffset.Now).Date),Slot?.CategoryID??0,Recipe?.RecipeId,DraftIndex,Portions,BatchKey,Reason,PreparedBatchId);
}
public sealed partial class AssistantPurchaseRow : ObservableObject
{
    [ObservableProperty] private bool _selected=true;
    [ObservableProperty] private bool _accepted;
    [ObservableProperty] private DateTimeOffset? _purchasedOn;
    [ObservableProperty] private DateTimeOffset? _expiresOn;
    public AssistantIngredientRow Line {get;}=new();
    public AiPurchase Capture()=>new(Line.Original,Line.Ingredient?.Name??Line.Name,Line.Ingredient?.IngredientID,Line.Quantity,Line.Unit?.UnitID,Line.Unit?.Abbreviation,PurchasedOn is {} p?DateOnly.FromDateTime(p.Date):null,ExpiresOn is {} e?DateOnly.FromDateTime(e.Date):null);
}
public sealed record AssistantRecovery(string Prompt,string Source,string Kind,AiPreferences Preferences,DateOnly StartDate,Guid? JobId,AiRecipe[] Recipes,AiPlanMeal[] Meals,AiPurchase[] Purchases,
    Dictionary<int,int> Published,Guid PlanOperation,Guid ReceiptOperation,Guid[] RecipeOperations,int[] ReplaceIds,int?[]? RecipeIds=null,byte[]?[]? RecipeVersions=null,bool[]? PurchaseSelection=null,AiContext? Context=null, string?[][]? RecipeQuantityInput=null, string?[]? PurchaseQuantityInput=null, bool[]? RecipeEditable=null, Guid? PlanJobId=null, Guid? ReceiptJobId=null, AiContext? PlanContext=null, Guid?[]? RecipeOriginJobs=null, int?[]? RecipeResultIndexes=null);
public sealed record AssistantPending(string Path,string Payload,string? ResultJson=null);

public sealed partial class AssistantViewModel : ViewModelBase
{
    private readonly AiClient _api; private readonly ISessionService _session; private LocalJsonStore? _recovery; private LocalJsonStore? _pending;
    private AiContext? _context; private AiJob? _job;
    private Guid? _planJobId;
    private AiContext? _planContext;
    private AiJob? _receiptJob;
    private string? _previewPayload;
    private int _planRevision; private Guid _planOperation=Guid.NewGuid(),_receiptOperation=Guid.NewGuid();
    private CancellationTokenSource? _poll;
    private bool _restoring;
    public AssistantViewModel(AiClient api,ISessionService session){_api=api;_session=session;Server=api.SavedServer;Identifier=session.CurrentUsername;PropertyChanged+=(_,_)=>SaveRecovery();ExcludedIngredients.CollectionChanged+=(_,_)=>SaveRecovery();SelectedSlots.CollectionChanged+=(_,_)=>SaveRecovery();ReplaceMeals.CollectionChanged+=(_,_)=>{InvalidatePreview();SaveRecovery();};Meals.CollectionChanged+=(_,_)=>InvalidatePreview();}
    [ObservableProperty] private string _server="";
    [ObservableProperty] private string _identifier="";
    [ObservableProperty] private bool _connected;
    [ObservableProperty] private bool _requiresSignIn;
    [ObservableProperty] private bool _acceptedNotice;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private LocalizedMessage? _statusContent;
    [LocalizedProperty] public string? Status=>StatusContent?.ToString();
    partial void OnStatusContentChanged(LocalizedMessage? value)=>OnPropertyChanged(nameof(Status));
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyPlan), nameof(PreviewCalories))]
    private AiPlanPreview? _preview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyPlan), nameof(CanSaveRecipe))]
    private bool _hasPending;

    public decimal? PreviewCalories => Preview is { } plan && (plan.NutritionComplete || plan.Calories != 0) ? plan.Calories : null;
    public bool CanApplyPlan => Preview is not null && !HasPending;
    public bool CanSaveRecipe => SelectedRecipe?.CanEdit == true && !HasPending;
    public event Action<AssistantIngredientRow>? IngredientFocusRequested;

    private void InvalidatePreview()
    {
        _planRevision++;
        _previewPayload = null;
        Preview = null;
    }

    partial void OnStartDateChanged(DateTimeOffset? value) => InvalidatePreview();
    partial void OnServingsChanged(decimal value) => InvalidatePreview();
    [ObservableProperty] private string _prompt="";
    [ObservableProperty] private string _source="";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextImport), nameof(IsImageRequest), nameof(IsPlanning), nameof(ShowsPrompt), nameof(ShowsPreferences))]
    private string _kind="suggest";
    public bool IsTextImport => Kind == "import";
    public bool IsImageRequest => Kind is "recipe-photo" or "receipt";
    public bool IsPlanning => Kind == "plan";
    public bool ShowsPrompt => Kind is not ("import" or "receipt");
    public bool ShowsPreferences => Kind is "suggest" or "generate" or "plan";

    private enum ReviewTab { Request, Suggestions, Recipes, Plan, Purchases }
    [ObservableProperty] private int _selectedTab;
    [RelayCommand] private void ShowRequest() => SelectedTab = (int)ReviewTab.Request;
    [ObservableProperty] private decimal _servings=2;
    [ObservableProperty] private int _maxMinutes=45;
    [ObservableProperty] private int _maxRepetitions=2;
    [ObservableProperty] private bool _preferExpiring=true;
    [ObservableProperty] private bool _useLeftovers=true;
    [ObservableProperty] private bool _allowNewRecipes;
    [ObservableProperty] private DateTimeOffset? _startDate=DateTimeOffset.Now;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecipeSelection), nameof(CanSaveRecipe))]
    private AssistantRecipeRow? _selectedRecipe;
    public bool HasRecipeSelection => SelectedRecipe is not null;
    [ObservableProperty] private AiJob? _selectedJob;
    [ObservableProperty] private bool _confirmDuplicate;
    public ObservableCollection<Ingredient> ExcludedIngredients {get;}=[];
    public ObservableCollection<Category> SelectedSlots {get;}=[];
    public ObservableCollection<Ingredient> AllIngredients {get;}=[];
    public ObservableCollection<Category> AllSlots {get;}=[];
    public ObservableCollection<AiCandidate> Candidates {get;}=[];
    public ObservableCollection<AiSuggestion> Suggestions {get;}=[];
    public ObservableCollection<AssistantRecipeRow> Recipes {get;}=[];
    public ObservableCollection<AssistantMealRow> Meals {get;}=[];
    public ObservableCollection<AssistantPurchaseRow> Purchases {get;}=[];
    public ObservableCollection<AiJob> Jobs {get;}=[];
    public ObservableCollection<MealPlanEntry> ExistingMeals {get;}=[];
    public ObservableCollection<MealPlanEntry> ReplaceMeals {get;}=[];
    private readonly Dictionary<int,int> _published=[];
    public Guid? UploadId {get; private set;}
    private string? UploadKind {get; set;}
    public void SetUpload(Guid id)
    {
        UploadId = id;
        UploadKind = Kind;
    }
    public AiContext? Context=>_context;
    public string[] Kinds=>AiValidation.Tasks;
    public AiTaskOption[] Tasks {get;}=AiValidation.Tasks.Select(k=>new AiTaskOption(k)).ToArray();
    [ObservableProperty] private AiTaskOption? _selectedTask;
    partial void OnSelectedTaskChanged(AiTaskOption? value){if(value is not null)Kind=value.Id;}
    partial void OnKindChanged(string value){if(SelectedTask?.Id!=value)SelectedTask=Tasks.FirstOrDefault(t=>t.Id==value);}
    public AiPreferences Preferences=>new(Servings,MaxMinutes,ExcludedIngredients.Select(i=>i.IngredientID).ToArray(),SelectedSlots.Select(c=>c.CategoryID).ToArray(),PreferExpiring,UseLeftovers,MaxRepetitions);
    private DateOnly Date=>DateOnly.FromDateTime((StartDate??DateTimeOffset.Now).Date);
    public async Task ConnectAsync(string password){await Run(async()=>{await _api.ConnectAsync(Server,Identifier,password,_session.CurrentUserId);await LoadAsync();});}
    public Task InitializeAsync() => Run(LoadAsync);
    public async Task LoadAsync()
    {
        Connected=_api.Connected;if(!Connected)return;
        RequiresSignIn = false;
        _restoring=true;
        try{
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MealPrepApp","assistant",_api.AccountKey!);
            _recovery=new(Path.Combine(root,"draft.json"));_pending=new(Path.Combine(root,"pending.json"));HasPending=_pending.Read<AssistantPending>() is not null;
            var capabilities=await _api.CallAsync<AiCapabilities>("GET","/v1/ai/capabilities");Enabled=capabilities.Enabled;
            var row=await _api.CallAsync<AiPreferenceRow>("GET","/v1/ai/preferences");AcceptedNotice=row.NoticeVersion==capabilities.DataNoticeVersion&&row.AcceptedAt.HasValue;
            var prefs=AiJson.Read<AiPreferences>(row.PreferencesJson);Servings=prefs.Servings;MaxMinutes=prefs.MaxMinutes;MaxRepetitions=prefs.MaxRepetitions;PreferExpiring=prefs.PreferExpiring;UseLeftovers=prefs.UseLeftovers;
            await RefreshContext();
            SetPreferences(prefs);
            if(_recovery.Read<AssistantRecovery>() is {} saved){Prompt=saved.Prompt;Source=saved.Source;Kind=saved.Kind;StartDate=new(saved.StartDate.ToDateTime(TimeOnly.MinValue));SetPreferences(saved.Preferences);await RefreshContext();if(saved.Context is not null)_context=saved.Context;_planOperation=saved.PlanOperation;_receiptOperation=saved.ReceiptOperation;_published.Clear();foreach(var pair in saved.Published)_published[pair.Key]=pair.Value;
                ShowProposal(new([],saved.Recipes,saved.Meals,saved.Purchases,[]),true);for(var i=0;i<Recipes.Count&&i<saved.RecipeOperations.Length;i++){Recipes[i].OperationId=saved.RecipeOperations[i];Recipes[i].OriginalRecipeId=saved.RecipeIds?.ElementAtOrDefault(i);Recipes[i].Version=saved.RecipeVersions?.ElementAtOrDefault(i);}for(var i=0;i<Purchases.Count;i++)if(saved.PurchaseSelection?.ElementAtOrDefault(i) is {} selected)Purchases[i].Selected=selected;foreach(var id in saved.ReplaceIds)if(ExistingMeals.FirstOrDefault(m=>m.MealPlanEntryID==id) is {} meal)ReplaceMeals.Add(meal);
                for (var i = 0; i < Recipes.Count; i++)
                {
                    Recipes[i].CanEdit = saved.RecipeEditable?.ElementAtOrDefault(i) ?? true;
                    Recipes[i].OriginJobId = saved.RecipeOriginJobs?.ElementAtOrDefault(i);
                    Recipes[i].ResultIndex = saved.RecipeResultIndexes?.ElementAtOrDefault(i);
                    if (saved.RecipeQuantityInput?.ElementAtOrDefault(i) is not { } inputs) continue;
                    for (var j = 0; j < Recipes[i].Ingredients.Count && j < inputs.Length; j++)
                        Recipes[i].Ingredients[j].QuantityInput = inputs[j];
                }
                for (var i = 0; i < Purchases.Count && i < (saved.PurchaseQuantityInput?.Length ?? 0); i++)
                    Purchases[i].Line.QuantityInput = saved.PurchaseQuantityInput![i];
                if(saved.JobId is {} jobId)_job=await _api.CallAsync<AiJob>("GET",$"/v1/ai/jobs/{jobId}");
                _planContext = saved.PlanContext ?? saved.Context;
                _planJobId = saved.PlanJobId ?? (_job?.Kind == "plan" ? _job.JobId : null);
                _receiptJob = saved.ReceiptJobId is { } receiptId && receiptId != _job?.JobId
                    ? await _api.CallAsync<AiJob>("GET", $"/v1/ai/jobs/{receiptId}")
                    : _job?.Kind == "receipt" ? _job : null;
                if (_receiptJob is not null) await ReceiptStatus();
            }
            await RefreshJobs();StatusContent=L.Message(Enabled?"Ai.Ready":"Ai.Disabled");
        }finally{_restoring=false;}
    }
    private void SetPreferences(AiPreferences p){Servings=p.Servings;MaxMinutes=p.MaxMinutes;MaxRepetitions=p.MaxRepetitions;PreferExpiring=p.PreferExpiring;UseLeftovers=p.UseLeftovers;ExcludedIngredients.Clear();SelectedSlots.Clear();foreach(var i in AllIngredients.Where(i=>(p.ExcludedIngredientIds??[]).Contains(i.IngredientID)))ExcludedIngredients.Add(i);foreach(var c in AllSlots.Where(c=>(p.MealSlotIds??AllSlots.Select(s=>s.CategoryID).ToArray()).Contains(c.CategoryID)))SelectedSlots.Add(c);}
    private async Task RefreshContext(){InvalidatePreview();_context=await _api.CallAsync<AiContext>("GET",$"/v1/ai/context?date={Date:yyyy-MM-dd}&servings={Servings.ToString(System.Globalization.CultureInfo.InvariantCulture)}");AllIngredients.Clear();AllSlots.Clear();Candidates.Clear();ExistingMeals.Clear();foreach(var i in _context.Ingredients)AllIngredients.Add(i);foreach(var c in _context.Categories)AllSlots.Add(c);foreach(var c in _context.Candidates)Candidates.Add(c);foreach(var e in _context.Plan)ExistingMeals.Add(e);}
    [RelayCommand] private Task Refresh()=>Run(async()=>{
        var replacementIds = ReplaceMeals.Select(meal => meal.MealPlanEntryID).ToHashSet();
        await RefreshContext();
        _planContext = _context;
        ReplaceMeals.Clear();
        foreach (var meal in ExistingMeals.Where(meal => replacementIds.Contains(meal.MealPlanEntryID)))
            ReplaceMeals.Add(meal);
        await RefreshJobs();
    });
    private async Task RefreshJobs(){var jobs=await _api.CallAsync<AiJob[]>("GET","/v1/ai/jobs");Jobs.Clear();foreach(var j in jobs)Jobs.Add(j);}
    [RelayCommand] private Task Consent()=>Run(async()=>{await _api.CallAsync<AiPreferenceRow>("POST","/v1/ai/consent",new AiConsent("gemini-free-1",AcceptedNotice));});
    [RelayCommand] private Task SavePreferences()=>Run(async()=>{await _api.CallAsync<AiPreferenceRow>("PUT","/v1/ai/preferences",Preferences);StatusContent=L.Message("Ai.Saved");});
    [RelayCommand] private Task ReviewImport()=>Run(async()=>{var recipe=await _api.CallAsync<AiRecipe>("POST","/v1/recipes/import-preview",new{source=Source});ShowProposal(AiProposal.Empty with{Recipes=[recipe]}, kind:"import");StatusContent=L.Message("Ai.Review");});
    [RelayCommand] private Task Generate()=>Run(async()=>{
        if (IsImageRequest && (UploadId is null || UploadKind != Kind)) throw new AiClientException("ImageUnavailable");
        if(!AcceptedNotice)throw new AiClientException("ConsentRequired");
        var request = new AiRequest(
            Guid.NewGuid(), Kind, L.Current.Language, Prompt, Date, Preferences,
            IsTextImport ? Source : null, IsImageRequest ? UploadId : null, IsPlanning && AllowNewRecipes,
            IsPlanning ? ReplaceMeals.Select(m => m.MealPlanEntryID).ToArray() : [],
            IsPlanning ? Meals.Where(m => m.Pinned).Select(m => m.Capture()).ToArray() : []);
        await RefreshContext();
        _job=await Mutate<AiJob>("/v1/ai/jobs",request);SaveRecovery();ClearPending();await Poll();
    });
    private async Task Poll()
    {
        _poll?.Cancel();
        _poll?.Dispose();
        _poll = new();
        var token = _poll.Token;
        while (_job is { State: "Queued" or "Running" })
        {
            StatusContent = L.Message("Ai.Waiting");
            await Task.Delay(2000, token);
            _job = await _api.CallAsync<AiJob>("GET", $"/v1/ai/jobs/{_job.JobId}", token: token);
            SaveRecovery();
        }

        if (_job is { State: "Completed", ResultJson: { } result } job)
        {
            if (HasPending) throw new AiClientException("PendingReview");
            if (job.WorkJson is { } work) _context = AiJson.Read<AiWork>(work).Context;
            InvalidatePreview();
            var repeatedPlan = job.Kind == "plan" && _planJobId == job.JobId;
            var repeatedReceipt = job.Kind == "receipt" && _receiptJob?.JobId == job.JobId;
            if (job.Kind == "plan" && !repeatedPlan)
            {
                _planJobId = job.JobId;
                _planContext = _context;
                _planOperation = Guid.NewGuid();
            }
            if (job.Kind == "receipt" && !repeatedReceipt)
            {
                _receiptJob = job;
                _receiptOperation = Guid.NewGuid();
            }
            if (repeatedPlan) SelectedTab = (int)ReviewTab.Plan;
            else if (repeatedReceipt) SelectedTab = (int)ReviewTab.Purchases;
            else ShowProposal(AiJson.Read<AiProposal>(result), kind: job.Kind, jobId: job.JobId);
            if (job.Kind == "receipt") await ReceiptStatus();
            StatusContent = L.Message("Ai.Review");
        }
        else
        {
            StatusContent = L.Message(_job?.State == "Cancelled" ? "Ai.Cancelled" : AiMessages.ErrorKey(_job?.ErrorCode));
        }
        await RefreshJobs();
    }

    [RelayCommand] private Task OpenJob()=>Run(async()=>{if(SelectedJob is null)return;_job=await _api.CallAsync<AiJob>("GET",$"/v1/ai/jobs/{SelectedJob.JobId}");await Poll();});
    [RelayCommand] private async Task Cancel(){_poll?.Cancel();if(_job is {} job)try{await _api.CallAsync<AiJob>("POST",$"/v1/ai/jobs/{job.JobId}/cancel");}catch(AiClientException e){ErrorContent=L.Message(AiMessages.ErrorKey(e.Code));return;}StatusContent=L.Message("Ai.Cancelled");}

    public void ShowProposal(AiProposal proposal, bool restoring = false, string? kind = null, Guid? jobId = null)
    {
        if (_context is null) return;
        if (kind is null)
        {
            if (!restoring) _published.Clear();
            Suggestions.Clear();
            Recipes.Clear();
            Meals.Clear();
            Purchases.Clear();
        }
        if (kind == "suggest") Suggestions.Clear();
        if (kind == "plan") Meals.Clear();
        if (kind == "receipt") Purchases.Clear();

        var recipeIndexes = new List<int>();
        foreach (var suggestion in proposal.Suggestions) Suggestions.Add(suggestion);
        for (var i = 0; i < proposal.Recipes.Length; i++)
        {
            var existing = jobId is null ? null : Recipes.FirstOrDefault(row => row.OriginJobId == jobId && row.ResultIndex == i);
            if (existing is not null)
            {
                recipeIndexes.Add(Recipes.IndexOf(existing));
                continue;
            }
            var row = new AssistantRecipeRow { OriginJobId = jobId, ResultIndex = jobId is null ? null : i };
            row.Populate(proposal.Recipes[i], _context);
            if (_published.TryGetValue(Recipes.Count, out var id)) row.PublishedId = id;
            Watch(row);
            recipeIndexes.Add(Recipes.Count);
            Recipes.Add(row);
        }

        if (recipeIndexes.Count > 0) SelectedRecipe = Recipes[recipeIndexes[0]];
        foreach (var meal in proposal.Meals)
            AddMeal(meal.DraftIndex is { } index && index >= 0 && index < recipeIndexes.Count
                ? meal with { DraftIndex = recipeIndexes[index] } : meal);

        foreach (var purchase in proposal.Purchases)
        {
            var row = new AssistantPurchaseRow
            {
                PurchasedOn = purchase.PurchasedOn is { } bought ? new(bought.ToDateTime(TimeOnly.MinValue)) : null,
                ExpiresOn = purchase.ExpiresOn is { } expires ? new(expires.ToDateTime(TimeOnly.MinValue)) : null
            };
            row.Line.Populate(new(purchase.Original, purchase.Name, purchase.Quantity, purchase.IngredientId, purchase.UnitId, purchase.Unit), _context);
            row.PropertyChanged += (_, _) => SaveRecovery();
            row.Line.PropertyChanged += (_, _) => SaveRecovery();
            Purchases.Add(row);
        }

        SelectedTab = kind switch
        {
            "plan" => (int)ReviewTab.Plan,
            "receipt" => (int)ReviewTab.Purchases,
            "suggest" => (int)ReviewTab.Suggestions,
            "import" or "generate" or "recipe-photo" => (int)ReviewTab.Recipes,
            _ when Recipes.Count > 0 => (int)ReviewTab.Recipes,
            _ when Meals.Count > 0 => (int)ReviewTab.Plan,
            _ when Purchases.Count > 0 => (int)ReviewTab.Purchases,
            _ when Suggestions.Count > 0 => (int)ReviewTab.Suggestions,
            _ => SelectedTab
        };
        SaveRecovery();
    }

    private void Watch(AssistantRecipeRow recipe)
    {
        recipe.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AssistantRecipeRow.UnresolvedRows) or nameof(AssistantRecipeRow.UnresolvedSummary)) return;
            if (e.PropertyName == nameof(AssistantRecipeRow.PublishedId)) InvalidatePreview();
            SaveRecovery();
        };
        foreach (var line in recipe.Ingredients) WatchLine(recipe, line);
        recipe.Ingredients.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (AssistantIngredientRow line in e.NewItems) WatchLine(recipe, line);
            recipe.ValidateRows();
            SaveRecovery();
        };
        recipe.ValidateRows();
    }

    private void WatchLine(AssistantRecipeRow recipe, AssistantIngredientRow line)
    {
        line.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AssistantIngredientRow.Problem) or nameof(AssistantIngredientRow.ProblemText)) return;
            if (e.PropertyName is nameof(line.Ingredient) or nameof(line.Unit) or nameof(line.Quantity))
                recipe.ValidateRows();
            SaveRecovery();
        };
    }

    private void AddMeal(AiPlanMeal meal){if(_context is null)return;var row=new AssistantMealRow{Date=new(meal.Date.ToDateTime(TimeOnly.MinValue)),Portions=meal.Portions,BatchKey=meal.BatchKey,Reason=meal.Reason,DraftIndex=meal.DraftIndex,PreparedBatchId=meal.PreparedBatchId};foreach(var c in _context.Categories)row.Slots.Add(c);foreach(var c in _context.Candidates)row.Recipes.Add(c);row.Slot=row.Slots.FirstOrDefault(c=>c.CategoryID==meal.SlotId);row.Recipe=row.Recipes.FirstOrDefault(c=>c.RecipeId==meal.RecipeId);row.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(row.Recipe)&&row.Recipe is not null){row.DraftIndex=null;row.PreparedBatchId=null;}InvalidatePreview();SaveRecovery();};Meals.Add(row);}
    [RelayCommand] private void AddManualMeal(){if(_context is null)return;AddMeal(new(Date,AllSlots.FirstOrDefault()?.CategoryID??0,Candidates.FirstOrDefault()?.RecipeId,null,Servings,Guid.NewGuid().ToString("N"),""));SaveRecovery();}
    [RelayCommand] private void RemoveMeal(AssistantMealRow row){Meals.Remove(row);SaveRecovery();}
    [RelayCommand] private void NewRecipe(){if(_context is null)return;var row=new AssistantRecipeRow();row.Populate(new("","","",2,0,0,null,[]),_context);Watch(row);Recipes.Add(row);SelectedRecipe=row;SaveRecovery();}
    [RelayCommand] private void AddIngredient(){if(SelectedRecipe is null||_context is null)return;var row=new AssistantIngredientRow();row.Populate(new("","",null,null,null),_context);SelectedRecipe.Ingredients.Add(row);}
    [RelayCommand] private void RemoveIngredient(AssistantIngredientRow row){SelectedRecipe?.Ingredients.Remove(row);SaveRecovery();}
    [RelayCommand] private Task CreateIngredient(AssistantIngredientRow row)=>Run(async()=>{if(row.Unit is null||string.IsNullOrWhiteSpace(row.Name))throw new AiClientException("ReviewRequired");await Mutate<MutationResult>("/v1/ingredients",new {operationId=Guid.NewGuid(),name=row.Name,unitId=row.Unit.UnitID});await RefreshContext();row.Ingredients.Clear();foreach(var i in _context!.Ingredients)row.Ingredients.Add(i);row.Ingredient=row.Ingredients.First(i=>i.Name==row.Name);SaveRecovery();ClearPending();});
    [RelayCommand] private Task OpenRecipe(AiSuggestion suggestion)=>Run(async()=>{var r=await _api.CallAsync<RecipeFull>("GET",$"/v1/recipes/{suggestion.RecipeId}");if(_context is null)return;var row=new AssistantRecipeRow {OriginalRecipeId=r.RecipeID,Version=r.RowVersion,CanEdit=r.UserID==_session.CurrentUserId};row.Populate(new(r.Title,r.Description??"",r.Instructions,r.Servings,r.PrepTimeMinutes,r.CookTimeMinutes,r.CategoryID,r.Ingredients.Select(i=>new AiIngredient(i.IngredientName,i.IngredientName,i.Quantity,i.IngredientID,i.UnitID,i.UnitAbbreviation,i.Notes)).ToArray()),_context);Watch(row);Recipes.Add(row);SelectedRecipe=row;SelectedTab=(int)ReviewTab.Recipes;});
    [RelayCommand] private Task SaveRecipe()=>Run(async()=>{
        if (SelectedRecipe is not { CanEdit: true } r) return;
        r.ValidateRows();
        if (r.Ingredients.FirstOrDefault(row => row.Problem is not null) is { } first)
        {
            IngredientFocusRequested?.Invoke(first);
            ErrorContent = first.Problem;
            return;
        }
        if (r.Servings is not (> 0 and <= 1000))
        {
            ErrorContent = L.Message("Ai.Error.RecipeServingsRequired");
            return;
        }
        if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Instructions) || r.Ingredients.Count == 0)
            throw new AiClientException("RecipeReviewRequired");

        var input=new RecipeBundleInput(r.OriginalRecipeId,r.Category?.CategoryID,r.Title,r.Description,r.Instructions,r.PrepMinutes,r.CookMinutes,r.Servings,r.Ingredients.Select(i=>new RecipeIngredientInput{IngredientID=i.Ingredient!.IngredientID,UnitID=i.Unit!.UnitID,Quantity=i.Quantity!.Value,Notes=i.Notes}).ToArray(),r.Version,r.Photo is null ? "Keep" : "Set",r.Photo?.Image,null);
        var result=await Mutate<RecipeBundleResult>("/v1/recipes/save",new AiRecipeSave(r.OperationId,input,r.Source,r.Source is not null ? "import" : "reviewed"));r.OriginalRecipeId=result.Id;r.Version=result.Version;r.PublishedId=result.Id;r.OperationId=Guid.NewGuid();_published[Recipes.IndexOf(r)]=result.Id;SaveRecovery();ClearPending();StatusContent=L.Message("Ai.Saved");
    });
    [RelayCommand] private Task ApplyPlan()=>Run(async()=>{
        var input = await BuildPlanInput();
        if (_previewPayload is null || _previewPayload != AiJson.Write(input))
            throw new AiClientException("StalePreview");
        await Mutate<MutationResult>("/v1/plans/apply", input);
        Meals.Clear();_planOperation=Guid.NewGuid();await RefreshContext();SaveRecovery();ClearPending();StatusContent=L.Message("Ai.Saved");
    });
    private async Task ReceiptStatus(){if(_receiptJob is not {} receipt)return;var accepted=await _api.CallAsync<int[]>("GET",$"/v1/ai/jobs/{receipt.JobId}/purchases");for(var i=0;i<Purchases.Count;i++){Purchases[i].Accepted=accepted.Contains(i);if(Purchases[i].Accepted)Purchases[i].Selected=false;}}
    [RelayCommand] private Task AcceptPurchases()=>Run(async()=>{
        if (_receiptJob is not {} receipt) return;
        if (Purchases.Any(p => p.Selected && !p.Accepted && (p.Line.HasInvalidQuantity ||
            p.Line.Ingredient is null || p.Line.Unit is null || p.PurchasedOn is null)))
            throw new AiClientException("MappingRequired");
        await Mutate<MutationResult>($"/v1/ai/jobs/{receipt.JobId}/purchases",new AiReceiptApply(_receiptOperation,receipt.Version,Purchases.Select((p,i)=>(p,i)).Where(x=>x.p.Selected&&!x.p.Accepted).Select(x=>x.i).ToArray(),Purchases.Select(p=>p.Capture()).ToArray(),ConfirmDuplicate:ConfirmDuplicate));
        foreach(var p in Purchases.Where(p=>p.Selected)){p.Accepted=true;p.Selected=false;}_receiptOperation=Guid.NewGuid();SaveRecovery();ClearPending();StatusContent=L.Message("Ai.SavedPurchases");
    });
    private void ClearPending()
    {
        _pending?.Delete();
        HasPending = false;
    }

    private async Task<T> Mutate<T>(string path, object body)
    {
        var payload = AiJson.Write(body);
        if (_pending?.Read<AssistantPending>() is { } pending)
        {
            if (pending.Path != path || pending.Payload != payload) throw new AiClientException("PendingReview");
            if (pending.ResultJson is { } prior) return AiJson.Read<T>(prior);
        }
        if (_pending is null || !_pending.Write(new AssistantPending(path, payload))) throw new AiClientException("Unavailable");
        HasPending = true;
        try
        {
            var result = await _api.CallAsync<T>("POST", path, body);
            if (!_pending.Write(new AssistantPending(path, payload, AiJson.Write(result)))) throw new AiClientException("PendingReview");
            return result;
        }
        catch (AiClientException e) when (e.Status is >= 400 and < 500 && e.Status is not (401 or 408 or 425 or 429))
        {
            ClearPending();
            throw;
        }

    }

    [RelayCommand] private Task RecoverPending()=>Run(async()=>{
        if(_pending?.Read<AssistantPending>() is not {} pending)return;
        using var body=System.Text.Json.JsonDocument.Parse(pending.Payload);
        var result=await Mutate<System.Text.Json.JsonElement>(pending.Path,body.RootElement);
        if(pending.Path=="/v1/ai/jobs"){
            _job=result.Deserialize<AiJob>(AiJson.Options);
            SaveRecovery();
            ClearPending();
            await Poll();
            return;
        }
        else if(pending.Path=="/v1/recipes/save"){
            var request=body.RootElement.Deserialize<AiRecipeSave>(AiJson.Options)!;var saved=result.Deserialize<RecipeBundleResult>(AiJson.Options)!;
            var row=Recipes.FirstOrDefault(r=>r.OperationId==request.OperationId);
            if(row is not null){row.OriginalRecipeId=saved.Id;row.PublishedId=saved.Id;row.Version=saved.Version;row.OperationId=Guid.NewGuid();_published[Recipes.IndexOf(row)]=saved.Id;}
        }else if(pending.Path=="/v1/plans/apply"){Meals.Clear();_planOperation=Guid.NewGuid();}
        else if(pending.Path.EndsWith("/purchases"))
        {
            var request = body.RootElement.Deserialize<AiReceiptApply>(AiJson.Options)!;
            foreach (var index in request.RowIndexes.Where(index => index >= 0 && index < Purchases.Count))
            {
                Purchases[index].Accepted = true;
                Purchases[index].Selected = false;
            }
            _receiptOperation = Guid.NewGuid();
        }
        SaveRecovery();ClearPending();StatusContent=L.Message("Ai.Recovered");
    });
    [RelayCommand]
    private Task PreviewPlan() => Run(async () =>
    {
        InvalidatePreview();
        var revision = _planRevision;
        var input = await BuildPlanInput();
        var preview = await _api.CallAsync<AiPlanPreview>("POST", "/v1/plans/preview", input);
        if (revision != _planRevision) return;
        _previewPayload = AiJson.Write(input);
        Preview = preview;
    });
    private async Task<AiPlanApply> BuildPlanInput(){
        if(_context is null)throw new AiClientException("ReviewRequired");
        var meals=Meals.Select(m=>m.Capture()).ToArray();
        for(var i=0;i<meals.Length;i++)if(meals[i].DraftIndex is {} index){if(!_published.TryGetValue(index,out var id))throw new AiClientException("ReviewRecipesFirst");meals[i]=meals[i] with{RecipeId=id,DraftIndex=null};}
        var versions=new Dictionary<int,string>();
        foreach(var id in meals.Select(m=>m.RecipeId??0).Distinct()){
            var prior=Meals.Select(m=>m.Recipe).FirstOrDefault(c=>c?.RecipeId==id);var version=prior?.Version??Recipes.FirstOrDefault(r=>r.PublishedId==id)?.Version;
            if(version is null){var r=await _api.CallAsync<RecipeFull>("GET",$"/v1/recipes/{id}");version=r.RowVersion;}
            versions[id]="0x"+Convert.ToHexString(version);
        }
        return new(_planOperation,_planJobId,meals,ReplaceMeals.Select(m=>m.MealPlanEntryID).ToArray(),versions,ReplaceMeals.ToDictionary(p=>p.MealPlanEntryID,p=>p.Version),(_planContext ?? _context).Batches.ToDictionary(b=>b.CookingBatchID,b=>b.Version));
    }
    private async Task Run(Func<Task> action){if(IsBusy)return;IsBusy=true;ClearError();try{await action();}catch(OperationCanceledException){}catch(Exception e){
        Connected = _api.Connected;
        if (e is AiClientException { Code: "VersionConflict" }) InvalidatePreview();
        var code = e is AiClientException a ? a.Code : e is HttpRequestException ? "ConnectionRequired" : null;
        if (code == "SessionExpired") RequiresSignIn = true;
        ErrorContent = L.Message(AiMessages.ErrorKey(HasPending && code != "SessionExpired" ? "PendingReview" : code));
    }finally{IsBusy=false;}}
    public void SaveRecovery(){if(_restoring||_recovery is null)return;_recovery.Write(new AssistantRecovery(Prompt,Source,Kind,Preferences,Date,_job?.JobId,Recipes.Select(r=>r.Capture()).ToArray(),Meals.Select(m=>m.Capture()).ToArray(),Purchases.Select(p=>p.Capture()).ToArray(),new(_published),_planOperation,_receiptOperation,Recipes.Select(r=>r.OperationId).ToArray(),ReplaceMeals.Select(m=>m.MealPlanEntryID).ToArray(),Recipes.Select(r=>r.OriginalRecipeId).ToArray(),Recipes.Select(r=>r.Version).ToArray(),Purchases.Select(p=>p.Selected).ToArray(),_context,Recipes.Select(r=>r.Ingredients.Select(i=>i.QuantityInput).ToArray()).ToArray(),Purchases.Select(p=>p.Line.QuantityInput).ToArray(),Recipes.Select(r=>r.CanEdit).ToArray(),_planJobId,_receiptJob?.JobId,_planContext,Recipes.Select(r=>r.OriginJobId).ToArray(),Recipes.Select(r=>r.ResultIndex).ToArray()));}
    public override void Dispose(){SaveRecovery();_poll?.Cancel();_poll?.Dispose();base.Dispose();}
}

public sealed class AiTaskOption(string id) : LocalizedObservableObject { public string Id {get;}=id; [LocalizedProperty] public string Label=>L.Text(AiMessages.TaskKey(Id)); }

public static class AiMessages
{
 public static string ErrorKey(string? code)=>code switch {
 "QuotaExceeded"=>"Ai.Error.QuotaExceeded","VersionConflict"=>"Ai.Error.VersionConflict","ConsentRequired"=>"Ai.Error.ConsentRequired","WrongDatabase"=>"Ai.Error.WrongDatabase","ConnectionRequired"=>"Ai.Error.ConnectionRequired","ReviewRequired"=>"Ai.Error.ReviewRequired","RecipeReviewRequired"=>"Ai.Error.RecipeReviewRequired","ReviewRecipesFirst"=>"Ai.Error.ReviewRecipesFirst","PendingReview"=>"Ai.Error.PendingReview","SessionExpired"=>"Ai.Error.SessionExpired","InvalidModelOutput"=>"Ai.Error.InvalidModelOutput","UploadFailed" or "InvalidRecipePhoto"=>"Ai.Error.UploadFailed",
 "DuplicateRecipeIngredient"=>"Ai.Error.DuplicateMapping","RecipeServingsRequired"=>"Ai.Error.RecipeServingsRequired",
 "MappingRequired" or "RequestRejected"=>"Ai.Error.MappingRequired","ImageUnavailable"=>"Ai.Error.ImageUnavailable",
 "StalePreview"=>"Ai.Error.StalePreview","InvalidInput" or "InvalidRequest"=>"Ai.Error.InvalidInput",
 "InvalidCredentials"=>"Ai.Error.InvalidCredentials","AccountLocked"=>"Ai.Error.AccountLocked","NotFound"=>"Ai.Error.NotFound",
 "Refused"=>"Ai.Error.Refused","IncompleteResponse"=>"Ai.Error.IncompleteResponse",
 "ContextTooLarge"=>"Ai.Error.ContextTooLarge","ProviderAccessDenied"=>"Ai.Error.ProviderAccessDenied",
 "TimedOut"=>"Ai.Error.TimedOut","Interrupted"=>"Ai.Error.Interrupted",
 "StorageUnavailable" or "ProviderUnavailable"=>"Ai.Error.ConnectionRequired","Disabled"=>"Ai.Disabled",
 _=>"Ai.Error.Unavailable"};
 public static string TaskKey(string kind)=>kind switch {"suggest"=>"Ai.Task.suggest","import"=>"Ai.Task.import","generate"=>"Ai.Task.generate","plan"=>"Ai.Task.plan","receipt"=>"Ai.Task.receipt",_=>"Ai.Task.recipe-photo"};
}
