using System.Text.Json;
using System.Collections.ObjectModel;
using MealPrepApp.Data.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPrepApp.Models;
using MealPrepApp.Services;

namespace MealPrepApp.ViewModels;

public sealed partial class AssistantIngredientRow : ObservableObject
{
    [ObservableProperty] private string _original="";
    [ObservableProperty] private string _name="";
    [ObservableProperty] private Ingredient? _ingredient;
    [ObservableProperty] private Unit? _unit;
    [ObservableProperty] private decimal? _quantity;
    [ObservableProperty] private string? _notes;
    public ObservableCollection<Ingredient> Ingredients {get;}=[];
    public ObservableCollection<Unit> Units {get;}=[];
    public void Populate(AiIngredient line,AiContext context){Original=line.Original;Name=line.Name;Quantity=line.Quantity;Notes=line.Notes;foreach(var i in context.Ingredients)Ingredients.Add(i);foreach(var u in context.Units)Units.Add(u);Ingredient=Ingredients.FirstOrDefault(i=>i.IngredientID==line.IngredientId);Unit=Units.FirstOrDefault(u=>u.UnitID==line.UnitId);}
    public AiIngredient Capture()=>new(Original,Ingredient?.Name??Name,Quantity,Ingredient?.IngredientID,Unit?.UnitID,Unit?.Abbreviation,Notes);
}
public sealed partial class AssistantRecipeRow : ObservableObject
{
    [ObservableProperty] private string _title="";
    [ObservableProperty] private string _description="";
    [ObservableProperty] private string _instructions="";
    [ObservableProperty] private int? _servings=2;
    [ObservableProperty] private int? _prepMinutes;
    [ObservableProperty] private int? _cookMinutes;
    [ObservableProperty] private Category? _category;
    [ObservableProperty] private string? _source;
    [ObservableProperty] private int? _publishedId;
    public byte[]? Version {get;set;}
    public int? OriginalRecipeId {get;set;}
    public Guid OperationId {get;set;}=Guid.NewGuid();
    public ObservableCollection<Category> Categories {get;}=[];
    public ObservableCollection<AssistantIngredientRow> Ingredients {get;}=[];
    public AiRecipe Capture()=>new(Title,Description,Instructions,Servings,PrepMinutes,CookMinutes,Category?.CategoryID,Ingredients.Select(i=>i.Capture()).ToArray(),Source);
    public void Populate(AiRecipe recipe,AiContext context){Title=recipe.Title;Description=recipe.Description;Instructions=recipe.Instructions;Servings=recipe.Servings;PrepMinutes=recipe.PrepMinutes;CookMinutes=recipe.CookMinutes;Source=recipe.Source;foreach(var c in context.Categories)Categories.Add(c);Category=Categories.FirstOrDefault(c=>c.CategoryID==recipe.CategoryId);foreach(var i in recipe.Ingredients){var row=new AssistantIngredientRow();row.Populate(i,context);Ingredients.Add(row);}}
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
    Dictionary<int,int> Published,Guid PlanOperation,Guid ReceiptOperation,Guid[] RecipeOperations,int[] ReplaceIds,int?[]? RecipeIds=null,byte[]?[]? RecipeVersions=null,bool[]? PurchaseSelection=null,AiContext? Context=null);
public sealed record AssistantPending(string Path,string Payload,string? ResultJson=null);

public sealed partial class AssistantViewModel : ViewModelBase
{
    private readonly AiClient _api; private readonly ISessionService _session; private LocalJsonStore? _recovery; private LocalJsonStore? _pending;
    private AiContext? _context; private AiJob? _job; private Guid _planOperation=Guid.NewGuid(),_receiptOperation=Guid.NewGuid();
    private CancellationTokenSource? _poll;
    private bool _restoring;
    public AssistantViewModel(AiClient api,ISessionService session){_api=api;_session=session;Server=api.SavedServer;Identifier=session.CurrentUsername;PropertyChanged+=(_,_)=>SaveRecovery();ExcludedIngredients.CollectionChanged+=(_,_)=>SaveRecovery();SelectedSlots.CollectionChanged+=(_,_)=>SaveRecovery();ReplaceMeals.CollectionChanged+=(_,_)=>SaveRecovery();}
    [ObservableProperty] private string _server="";
    [ObservableProperty] private string _identifier="";
    [ObservableProperty] private bool _connected;
    [ObservableProperty] private bool _acceptedNotice;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private LocalizedMessage? _statusContent;
    [LocalizedProperty] public string? Status=>StatusContent?.ToString();
    partial void OnStatusContentChanged(LocalizedMessage? value)=>OnPropertyChanged(nameof(Status));
    [ObservableProperty] private AiPlanPreview? _preview;
    [ObservableProperty] private string _prompt="";
    [ObservableProperty] private string _source="";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTextImport), nameof(IsImageRequest), nameof(IsPlanning))]
    private string _kind="suggest";
    public bool IsTextImport => Kind == "import";
    public bool IsImageRequest => Kind is "recipe-photo" or "receipt";
    public bool IsPlanning => Kind == "plan";

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
    [NotifyPropertyChangedFor(nameof(HasRecipeSelection))]
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
    public Guid? UploadId {get;set;}
    public AiContext? Context=>_context;
    public string[] Kinds=>AiValidation.Tasks;
    public AiTaskOption[] Tasks {get;}=AiValidation.Tasks.Select(k=>new AiTaskOption(k)).ToArray();
    [ObservableProperty] private AiTaskOption? _selectedTask;
    partial void OnSelectedTaskChanged(AiTaskOption? value){if(value is not null)Kind=value.Id;}
    partial void OnKindChanged(string value){if(SelectedTask?.Id!=value)SelectedTask=Tasks.FirstOrDefault(t=>t.Id==value);}
    public AiPreferences Preferences=>new(Servings,MaxMinutes,ExcludedIngredients.Select(i=>i.IngredientID).ToArray(),SelectedSlots.Select(c=>c.CategoryID).ToArray(),PreferExpiring,UseLeftovers,MaxRepetitions);
    private DateOnly Date=>DateOnly.FromDateTime((StartDate??DateTimeOffset.Now).Date);
    public async Task ConnectAsync(string password){await Run(async()=>{await _api.ConnectAsync(Server,Identifier,password,_session.CurrentUserId);await LoadAsync();});}
    public async Task LoadAsync()
    {
        Connected=_api.Connected;if(!Connected)return;
        _restoring=true;
        try{
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MealPrepApp","assistant",_api.AccountKey!);
            _recovery=new(Path.Combine(root,"draft.json"));_pending=new(Path.Combine(root,"pending.json"));
            var capabilities=await _api.CallAsync<AiCapabilities>("GET","/v1/ai/capabilities");Enabled=capabilities.Enabled;
            var row=await _api.CallAsync<AiPreferenceRow>("GET","/v1/ai/preferences");AcceptedNotice=row.NoticeVersion==capabilities.DataNoticeVersion&&row.AcceptedAt.HasValue;
            var prefs=AiJson.Read<AiPreferences>(row.PreferencesJson);Servings=prefs.Servings;MaxMinutes=prefs.MaxMinutes;MaxRepetitions=prefs.MaxRepetitions;PreferExpiring=prefs.PreferExpiring;UseLeftovers=prefs.UseLeftovers;
            await RefreshContext();
            SetPreferences(prefs);
            if(_recovery.Read<AssistantRecovery>() is {} saved){Prompt=saved.Prompt;Source=saved.Source;Kind=saved.Kind;StartDate=new(saved.StartDate.ToDateTime(TimeOnly.MinValue));SetPreferences(saved.Preferences);await RefreshContext();if(saved.Context is not null)_context=saved.Context;_planOperation=saved.PlanOperation;_receiptOperation=saved.ReceiptOperation;_published.Clear();foreach(var pair in saved.Published)_published[pair.Key]=pair.Value;
                ShowProposal(new([],saved.Recipes,saved.Meals,saved.Purchases,[]),true);for(var i=0;i<Recipes.Count&&i<saved.RecipeOperations.Length;i++){Recipes[i].OperationId=saved.RecipeOperations[i];Recipes[i].OriginalRecipeId=saved.RecipeIds?.ElementAtOrDefault(i);Recipes[i].Version=saved.RecipeVersions?.ElementAtOrDefault(i);}for(var i=0;i<Purchases.Count;i++)if(saved.PurchaseSelection?.ElementAtOrDefault(i) is {} selected)Purchases[i].Selected=selected;foreach(var id in saved.ReplaceIds)if(ExistingMeals.FirstOrDefault(m=>m.MealPlanEntryID==id) is {} meal)ReplaceMeals.Add(meal);
                if(saved.JobId is {} jobId)_job=await _api.CallAsync<AiJob>("GET",$"/v1/ai/jobs/{jobId}");
            }
            await RefreshJobs();StatusContent=L.Message(Enabled?"Ai.Ready":"Ai.Disabled");
        }finally{_restoring=false;}
    }
    private void SetPreferences(AiPreferences p){Servings=p.Servings;MaxMinutes=p.MaxMinutes;MaxRepetitions=p.MaxRepetitions;PreferExpiring=p.PreferExpiring;UseLeftovers=p.UseLeftovers;ExcludedIngredients.Clear();SelectedSlots.Clear();foreach(var i in AllIngredients.Where(i=>(p.ExcludedIngredientIds??[]).Contains(i.IngredientID)))ExcludedIngredients.Add(i);foreach(var c in AllSlots.Where(c=>(p.MealSlotIds??AllSlots.Select(s=>s.CategoryID).ToArray()).Contains(c.CategoryID)))SelectedSlots.Add(c);}
    private async Task RefreshContext(){_context=await _api.CallAsync<AiContext>("GET",$"/v1/ai/context?date={Date:yyyy-MM-dd}&servings={Servings.ToString(System.Globalization.CultureInfo.InvariantCulture)}");AllIngredients.Clear();AllSlots.Clear();Candidates.Clear();ExistingMeals.Clear();foreach(var i in _context.Ingredients)AllIngredients.Add(i);foreach(var c in _context.Categories)AllSlots.Add(c);foreach(var c in _context.Candidates)Candidates.Add(c);foreach(var e in _context.Plan)ExistingMeals.Add(e);}
    [RelayCommand] private Task Refresh()=>Run(async()=>{await RefreshContext();await RefreshJobs();});
    private async Task RefreshJobs(){Jobs.Clear();foreach(var j in await _api.CallAsync<AiJob[]>("GET","/v1/ai/jobs"))Jobs.Add(j);}
    [RelayCommand] private Task Consent()=>Run(async()=>{await _api.CallAsync<AiPreferenceRow>("POST","/v1/ai/consent",new AiConsent("gemini-free-1",AcceptedNotice));});
    [RelayCommand] private Task SavePreferences()=>Run(async()=>{await _api.CallAsync<AiPreferenceRow>("PUT","/v1/ai/preferences",Preferences);StatusContent=L.Message("Ai.Saved");});
    [RelayCommand] private Task ReviewImport()=>Run(async()=>{var recipe=await _api.CallAsync<AiRecipe>("POST","/v1/recipes/import-preview",new{source=Source});ShowProposal(AiProposal.Empty with{Recipes=[recipe]});StatusContent=L.Message("Ai.Review");});
    [RelayCommand] private Task Generate()=>Run(async()=>{
        if(!AcceptedNotice)throw new AiClientException("ConsentRequired");
        var request = new AiRequest(
            Guid.NewGuid(), Kind, L.Current.Language, Prompt, Date, Preferences,
            IsTextImport ? Source : null, IsImageRequest ? UploadId : null, IsPlanning && AllowNewRecipes,
            IsPlanning ? ReplaceMeals.Select(m => m.MealPlanEntryID).ToArray() : [],
            IsPlanning ? Meals.Where(m => m.Pinned).Select(m => m.Capture()).ToArray() : []);
        await RefreshContext();_planOperation=Guid.NewGuid();_receiptOperation=Guid.NewGuid();
        _job=await Mutate<AiJob>("/v1/ai/jobs",request);SaveRecovery();_pending?.Delete();await Poll();
    });
    private async Task Poll(){_poll?.Cancel();_poll?.Dispose();_poll=new();var token=_poll.Token;while(_job is {State:"Queued" or "Running"}){StatusContent=L.Message("Ai.Waiting");await Task.Delay(2000,token);_job=await _api.CallAsync<AiJob>("GET",$"/v1/ai/jobs/{_job.JobId}",token:token);SaveRecovery();}if(_job?.State=="Completed"&&_job.ResultJson is {} result){if(_job.WorkJson is {} work)_context=AiJson.Read<AiWork>(work).Context;ShowProposal(AiJson.Read<AiProposal>(result));if(_job.Kind=="receipt")await ReceiptStatus();StatusContent=L.Message("Ai.Review");}else StatusContent=L.Message(AiMessages.ErrorKey(_job?.ErrorCode));await RefreshJobs();}
    [RelayCommand] private Task OpenJob()=>Run(async()=>{if(SelectedJob is null)return;_job=await _api.CallAsync<AiJob>("GET",$"/v1/ai/jobs/{SelectedJob.JobId}");await Poll();});
    [RelayCommand] private async Task Cancel(){_poll?.Cancel();if(_job is {} job)try{await _api.CallAsync<AiJob>("POST",$"/v1/ai/jobs/{job.JobId}/cancel");}catch{}StatusContent=L.Message("Ai.Cancelled");}
    public void ShowProposal(AiProposal proposal, bool restoring = false)
    {
        if (_context is null) return;
        if (!restoring) _published.Clear();
        Suggestions.Clear();
        Recipes.Clear();
        Meals.Clear();
        Purchases.Clear();

        foreach (var suggestion in proposal.Suggestions) Suggestions.Add(suggestion);
        for (var i = 0; i < proposal.Recipes.Length; i++)
        {
            var row = new AssistantRecipeRow();
            row.Populate(proposal.Recipes[i], _context);
            if (_published.TryGetValue(i, out var id)) row.PublishedId = id;
            Watch(row);
            Recipes.Add(row);
        }
        SelectedRecipe = Recipes.FirstOrDefault();
        foreach (var meal in proposal.Meals) AddMeal(meal);
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

        if (Recipes.Count > 0) SelectedTab = (int)ReviewTab.Recipes;
        else if (Meals.Count > 0) SelectedTab = (int)ReviewTab.Plan;
        else if (Purchases.Count > 0) SelectedTab = (int)ReviewTab.Purchases;
        else if (Suggestions.Count > 0) SelectedTab = (int)ReviewTab.Suggestions;
        SaveRecovery();
    }

    private void Watch(AssistantRecipeRow recipe)
    {
        recipe.PropertyChanged += (_, _) => SaveRecovery();
        foreach (var line in recipe.Ingredients) line.PropertyChanged += (_, _) => SaveRecovery();
    }

    private void AddMeal(AiPlanMeal meal){if(_context is null)return;var row=new AssistantMealRow{Date=new(meal.Date.ToDateTime(TimeOnly.MinValue)),Portions=meal.Portions,BatchKey=meal.BatchKey,Reason=meal.Reason,DraftIndex=meal.DraftIndex,PreparedBatchId=meal.PreparedBatchId};foreach(var c in _context.Categories)row.Slots.Add(c);foreach(var c in _context.Candidates)row.Recipes.Add(c);row.Slot=row.Slots.FirstOrDefault(c=>c.CategoryID==meal.SlotId);row.Recipe=row.Recipes.FirstOrDefault(c=>c.RecipeId==meal.RecipeId);row.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(row.Recipe)&&row.Recipe is not null){row.DraftIndex=null;row.PreparedBatchId=null;}SaveRecovery();};Meals.Add(row);}
    [RelayCommand] private void AddManualMeal(){if(_context is null)return;AddMeal(new(Date,AllSlots.FirstOrDefault()?.CategoryID??0,Candidates.FirstOrDefault()?.RecipeId,null,Servings,Guid.NewGuid().ToString("N"),""));SaveRecovery();}
    [RelayCommand] private void RemoveMeal(AssistantMealRow row){Meals.Remove(row);SaveRecovery();}
    [RelayCommand] private void NewRecipe(){if(_context is null)return;var row=new AssistantRecipeRow();row.Populate(new("","","",2,0,0,null,[]),_context);Watch(row);Recipes.Add(row);SelectedRecipe=row;SaveRecovery();}
    [RelayCommand] private void AddIngredient(){if(SelectedRecipe is null||_context is null)return;var row=new AssistantIngredientRow();row.Populate(new("","",null,null,null),_context);row.PropertyChanged+=(_,_)=>SaveRecovery();SelectedRecipe.Ingredients.Add(row);SaveRecovery();}
    [RelayCommand] private void RemoveIngredient(AssistantIngredientRow row){SelectedRecipe?.Ingredients.Remove(row);SaveRecovery();}
    [RelayCommand] private Task CreateIngredient(AssistantIngredientRow row)=>Run(async()=>{if(row.Unit is null||string.IsNullOrWhiteSpace(row.Name))throw new AiClientException("ReviewRequired");await Mutate<MutationResult>("/v1/ingredients",new {operationId=Guid.NewGuid(),name=row.Name,unitId=row.Unit.UnitID});await RefreshContext();row.Ingredients.Clear();foreach(var i in _context!.Ingredients)row.Ingredients.Add(i);row.Ingredient=row.Ingredients.First(i=>i.Name==row.Name);SaveRecovery();_pending?.Delete();});
    [RelayCommand] private Task OpenRecipe(AiSuggestion suggestion)=>Run(async()=>{var r=await _api.CallAsync<RecipeFull>("GET",$"/v1/recipes/{suggestion.RecipeId}");if(_context is null)return;var row=new AssistantRecipeRow {OriginalRecipeId=r.RecipeID,Version=r.RowVersion};row.Populate(new(r.Title,r.Description??"",r.Instructions,r.Servings,r.PrepTimeMinutes,r.CookTimeMinutes,r.CategoryID,r.Ingredients.Select(i=>new AiIngredient(i.IngredientName,i.IngredientName,i.Quantity,i.IngredientID,i.UnitID,i.UnitAbbreviation,i.Notes)).ToArray()),_context);Watch(row);Recipes.Add(row);SelectedRecipe=row;SelectedTab=(int)ReviewTab.Recipes;});
    [RelayCommand] private Task SaveRecipe()=>Run(async()=>{
        if(SelectedRecipe is not {} r) return;
        if(r.Ingredients.Any(i=>i.Ingredient is null||i.Unit is null||!AiValidation.Quantity(i.Quantity??0)))throw new AiClientException("ReviewRequired");
        var input=new RecipeBundleInput(r.OriginalRecipeId,r.Category?.CategoryID,r.Title,r.Description,r.Instructions,r.PrepMinutes,r.CookMinutes,r.Servings,r.Ingredients.Select(i=>new RecipeIngredientInput{IngredientID=i.Ingredient!.IngredientID,UnitID=i.Unit!.UnitID,Quantity=i.Quantity!.Value,Notes=i.Notes}).ToArray(),r.Version,"Keep",null,null);
        var result=await Mutate<RecipeBundleResult>("/v1/recipes/save",new AiRecipeSave(r.OperationId,input,r.Source ?? (r.OriginalRecipeId is null && IsTextImport ? Source : null),Kind));r.OriginalRecipeId=result.Id;r.Version=result.Version;r.PublishedId=result.Id;r.OperationId=Guid.NewGuid();_published[Recipes.IndexOf(r)]=result.Id;SaveRecovery();_pending?.Delete();StatusContent=L.Message("Ai.Saved");
    });
    [RelayCommand] private Task ApplyPlan()=>Run(async()=>{
        await Mutate<MutationResult>("/v1/plans/apply",await BuildPlanInput());
        Meals.Clear();_planOperation=Guid.NewGuid();await RefreshContext();SaveRecovery();_pending?.Delete();StatusContent=L.Message("Ai.Saved");
    });
    private async Task ReceiptStatus(){if(_job is not{Kind:"receipt"})return;var accepted=await _api.CallAsync<int[]>("GET",$"/v1/ai/jobs/{_job.JobId}/purchases");for(var i=0;i<Purchases.Count;i++){Purchases[i].Accepted=accepted.Contains(i);if(Purchases[i].Accepted)Purchases[i].Selected=false;}}
    [RelayCommand] private Task AcceptPurchases()=>Run(async()=>{
        if(_job is not{Kind:"receipt"})return;
        await Mutate<MutationResult>($"/v1/ai/jobs/{_job.JobId}/purchases",new AiReceiptApply(_receiptOperation,_job.Version,Purchases.Select((p,i)=>(p,i)).Where(x=>x.p.Selected&&!x.p.Accepted).Select(x=>x.i).ToArray(),Purchases.Select(p=>p.Capture()).ToArray(),ConfirmDuplicate:ConfirmDuplicate));
        foreach(var p in Purchases.Where(p=>p.Selected)){p.Accepted=true;p.Selected=false;}_receiptOperation=Guid.NewGuid();SaveRecovery();_pending?.Delete();StatusContent=L.Message("Ai.SavedPurchases");
    });
    private async Task<T> Mutate<T>(string path,object body){
        var payload=AiJson.Write(body);
        if(_pending?.Read<AssistantPending>() is {} pending&&(pending.Path!=path||pending.Payload!=payload))throw new AiClientException("PendingReview");
        if(_pending is null||!_pending.Write(new AssistantPending(path,payload)))throw new AiClientException("Unavailable");
        try{
            var recorded=_pending.Read<AssistantPending>();if(recorded?.ResultJson is {} prior)return AiJson.Read<T>(prior);
            var result=await _api.CallAsync<T>("POST",path,body);
            if(!_pending.Write(new AssistantPending(path,payload,AiJson.Write(result))))throw new AiClientException("Unavailable");return result;
        }
        catch(AiClientException e)when(e.Status is >=400 and <500 && e.Status is not (401 or 429)){_pending.Delete();throw;}
    }
    [RelayCommand] private Task RecoverPending()=>Run(async()=>{
        if(_pending?.Read<AssistantPending>() is not {} pending)return;
        using var body=System.Text.Json.JsonDocument.Parse(pending.Payload);
        var result=await Mutate<System.Text.Json.JsonElement>(pending.Path,body.RootElement);
        if(pending.Path=="/v1/ai/jobs"){_job=result.Deserialize<AiJob>(AiJson.Options);await Poll();}
        else if(pending.Path=="/v1/recipes/save"){
            var request=body.RootElement.Deserialize<AiRecipeSave>(AiJson.Options)!;var saved=result.Deserialize<RecipeBundleResult>(AiJson.Options)!;
            var row=Recipes.FirstOrDefault(r=>r.OperationId==request.OperationId);
            if(row is not null){row.OriginalRecipeId=saved.Id;row.PublishedId=saved.Id;row.Version=saved.Version;row.OperationId=Guid.NewGuid();_published[Recipes.IndexOf(row)]=saved.Id;}
        }else if(pending.Path=="/v1/plans/apply"){Meals.Clear();_planOperation=Guid.NewGuid();}
        else if(pending.Path.EndsWith("/purchases")){foreach(var p in Purchases)p.Selected=false;}
        SaveRecovery();_pending?.Delete();StatusContent=L.Message("Ai.Recovered");
    });
    [RelayCommand] private Task PreviewPlan()=>Run(async()=>{Preview=await _api.CallAsync<AiPlanPreview>("POST","/v1/plans/preview",await BuildPlanInput());});
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
        return new(_planOperation,_job?.Kind=="plan"?_job.JobId:null,meals,ReplaceMeals.Select(m=>m.MealPlanEntryID).ToArray(),versions,ReplaceMeals.ToDictionary(p=>p.MealPlanEntryID,p=>p.Version),(_job?.WorkJson is {} work?AiJson.Read<AiWork>(work).Context.Batches:_context.Batches).ToDictionary(b=>b.CookingBatchID,b=>b.Version));
    }
    private async Task Run(Func<Task> action){if(IsBusy)return;IsBusy=true;ClearError();try{await action();}catch(OperationCanceledException){}catch(Exception e){ErrorContent=L.Message(AiMessages.ErrorKey(e is AiClientException a?a.Code:null));}finally{IsBusy=false;}}
    public void SaveRecovery(){if(_restoring||_recovery is null)return;_recovery.Write(new AssistantRecovery(Prompt,Source,Kind,Preferences,Date,_job?.JobId,Recipes.Select(r=>r.Capture()).ToArray(),Meals.Select(m=>m.Capture()).ToArray(),Purchases.Select(p=>p.Capture()).ToArray(),new(_published),_planOperation,_receiptOperation,Recipes.Select(r=>r.OperationId).ToArray(),ReplaceMeals.Select(m=>m.MealPlanEntryID).ToArray(),Recipes.Select(r=>r.OriginalRecipeId).ToArray(),Recipes.Select(r=>r.Version).ToArray(),Purchases.Select(p=>p.Selected).ToArray(),_context));}
    public override void Dispose(){SaveRecovery();_poll?.Cancel();_poll?.Dispose();base.Dispose();}
}

public sealed class AiTaskOption(string id) : LocalizedObservableObject { public string Id {get;}=id; [LocalizedProperty] public string Label=>L.Text(AiMessages.TaskKey(Id)); }

public static class AiMessages
{
 public static string ErrorKey(string? code)=>code switch {
 "QuotaExceeded"=>"Ai.Error.QuotaExceeded","VersionConflict"=>"Ai.Error.VersionConflict","ConsentRequired"=>"Ai.Error.ConsentRequired","WrongDatabase"=>"Ai.Error.WrongDatabase","ConnectionRequired"=>"Ai.Error.ConnectionRequired","ReviewRequired"=>"Ai.Error.ReviewRequired","ReviewRecipesFirst"=>"Ai.Error.ReviewRecipesFirst","PendingReview"=>"Ai.Error.PendingReview","SessionExpired"=>"Ai.Error.SessionExpired","InvalidModelOutput"=>"Ai.Error.InvalidModelOutput","UploadFailed"=>"Ai.Error.UploadFailed",_=>"Ai.Error.Unavailable"};
 public static string TaskKey(string kind)=>kind switch {"suggest"=>"Ai.Task.suggest","import"=>"Ai.Task.import","generate"=>"Ai.Task.generate","plan"=>"Ai.Task.plan","receipt"=>"Ai.Task.receipt",_=>"Ai.Task.recipe-photo"};
}
