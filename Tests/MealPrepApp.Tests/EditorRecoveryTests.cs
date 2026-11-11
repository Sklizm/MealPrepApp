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
    [Fact] public void RecoveryIgnoresAnOlderDelayedWriteAndAccountStoresAreSeparate() {
        var dir=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());var one=new EditorRecoveryService(Path.Combine(dir,"one.json"));var two=new EditorRecoveryService(Path.Combine(dir,"two.json"));
        try {
            var data=new EditorRecovery(null,null,null,"Newer",null,"",null,null,null,[],[],"Keep",null,null,"",Guid.NewGuid());var old=one.ReserveRevision();var current=one.ReserveRevision();one.Write(data,current);one.Write(data with{Title="Old"},old);
            Assert.Equal("Newer",one.Read()!.Title);Assert.Null(two.Read());one.Delete();one.Write(data,current);Assert.Null(one.Read());
        }finally{if(Directory.Exists(dir))Directory.Delete(dir,true);}
    }
}
