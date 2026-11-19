using MealPrepApp.Data.Repositories;
using MealPrepApp.Models;
using MealPrepApp.Services;
using MealPrepApp.ViewModels.Retete;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace MealPrepApp.Tests;
public sealed class EditorRecoveryTests
{
    private sealed class Lookups() : LookupRepository(new RedesignSamples.NoDatabase()) {
        public override Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<Category>>([]);
        public override Task<IReadOnlyList<Unit>> GetUnitsAsync(CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<Unit>>([new(){UnitID=1,Abbreviation="g"}]);
    }
    private sealed class Ingredients() : IngredientRepository(new RedesignSamples.NoDatabase()) {
        public override Task<IReadOnlyList<Ingredient>> GetIngredientsAsync(int? ingredientCategoryId=null,CancellationToken cancellationToken=default)=>Task.FromResult<IReadOnlyList<Ingredient>>([]);
    }
    [Fact] public async Task RecoveryRetainsUnresolvedTextSelectedPhotoAndPublishedRecipeIdentity() {
        var directory=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var store=new EditorRecoveryService(Path.Combine(directory,"editor.json"));
        try {
            store.Write(new(null,null,null,"Interrupted",null,"Cook",null,null,2,[new(){IngredientText="Unmapped garden greens",Quantity=.3333m}],[],"Set",[1,2,3],[4,5],"Original pasted ingredients",Guid.NewGuid()),store.ReserveRevision());
            using var services=RedesignSamples.Services(configure:s=>{s.AddSingleton(store);s.AddSingleton<LookupRepository>(new Lookups());s.AddSingleton<IngredientRepository>(new Ingredients());});using var scope=services.CreateScope();
            var editor=scope.ServiceProvider.GetRequiredService<ReteteEditorViewModel>();await editor.LoadAsync();Assert.True(editor.CanRestoreRecovery);Assert.Equal("",editor.Title);
            editor.RestoreRecoveryCommand.Execute(null);Assert.Equal("Interrupted",editor.Title);Assert.Equal(new byte[]{1,2,3},editor.PhotoBytes);Assert.Equal("Unmapped garden greens",Assert.Single(editor.IngredientRows).IngredientText);Assert.Null(editor.IngredientRows[0].SelectedIngredient);Assert.False(editor.IngredientRows[0].IsComplete);Assert.Null(editor.RecipeId);
            editor.SaveRecoveryNow();var recovered=store.Read()!;Assert.Equal(new byte[]{4,5},recovered.Thumbnail);Assert.Equal("Set",recovered.PhotoMode);Assert.Equal(.3333m,recovered.Rows[0].Quantity);Assert.True(editor.IsDirty);
            await editor.SaveCommand.ExecuteAsync(null);Assert.True(editor.HasError);Assert.Null(editor.RecipeId);editor.Dispose();
        } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
    private sealed class InterruptedRecipeSave : RecipeRepository
    {
        public List<(Guid Operation, RecipeBundleInput Input)> Requests { get; } = [];
        public InterruptedRecipeSave() : base(new RedesignSamples.NoDatabase()) { }

        public override Task<RecipeBundleResult> SaveBundleAsync(int userId, Guid operationId, RecipeBundleInput input)
        {
            Requests.Add((operationId, input));
            if (Requests.Count == 1) throw new IOException("Fixture response was lost.");
            return Task.FromResult(new RecipeBundleResult(42, [1, 2, 3]));
        }
    }

    [Fact]
    public async Task RecipeRecoveryReplaysTheOriginalPayloadBeforeSavingNewEdits()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var store = new EditorRecoveryService(Path.Combine(directory, "editor.json"));
        var repository = new InterruptedRecipeSave();
        try
        {
            using var services = RedesignSamples.Services(configure: registrations =>
            {
                registrations.AddSingleton(store);
                registrations.AddSingleton<LookupRepository>(new Lookups());
                registrations.AddSingleton<IngredientRepository>(new Ingredients());
                registrations.AddSingleton<RecipeRepository>(repository);
            });
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ISessionService>().SignIn(1, "Recovery fixture");
            var editor = scope.ServiceProvider.GetRequiredService<ReteteEditorViewModel>();
            await editor.LoadAsync();
            editor.Title = "Original save";
            editor.Instructions = "Cook.";
            editor.AddIngredientRowCommand.Execute(null);
            var row = Assert.Single(editor.IngredientRows);
            row.SelectedIngredient = new Ingredient { IngredientID = 1, Name = "Rice" };
            row.SelectedUnit = new Unit { UnitID = 1, Abbreviation = "g" };
            row.Quantity = 0.3333m;

            await editor.SaveCommand.ExecuteAsync(null);
            Assert.True(editor.HasPendingSave);
            var pending = store.Read()!;
            Assert.Equal("Original save", pending.PendingBundle?.Title);
            Assert.Equal(repository.Requests[0].Operation, pending.SaveOperation);

            editor.Title = "Newer draft title";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Equal(2, repository.Requests.Count);
            Assert.Equal(repository.Requests[0], repository.Requests[1]);
            Assert.Equal(0.3333m, repository.Requests[1].Input.Ingredients.Single().Quantity);
            Assert.Equal(42, editor.RecipeId);
            Assert.Equal("Newer draft title", editor.Title);
            Assert.True(editor.IsDirty);
            Assert.False(editor.HasPendingSave);
            Assert.Null(store.Read()?.PendingBundle);
            editor.Dispose();
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact] public void RecoveryIgnoresAnOlderDelayedWriteAndAccountStoresAreSeparate() {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var one=new EditorRecoveryService(Path.Combine(dir,"one.json"));var two=new EditorRecoveryService(Path.Combine(dir,"two.json"));
        try {
            var data=new EditorRecovery(null,null,null,"Newer",null,"",null,null,null,[],[],"Keep",null,null,"",Guid.NewGuid());var old=one.ReserveRevision();var current=one.ReserveRevision();one.Write(data,current);one.Write(data with{Title="Old"},old);
            Assert.Equal("Newer",one.Read()!.Title);Assert.Null(two.Read());one.Delete();one.Write(data,current);Assert.Null(one.Read());
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
