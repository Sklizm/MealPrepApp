using MealPrepApp.Models;

namespace MealPrepApp.Services;

public sealed record AiPlanShortage(int IngredientId,string Name,int UnitId,string Unit,decimal Required,decimal FromPantry,decimal Missing,bool NeedsReview);
public sealed record AiPlanPreview(int CookingBatches,int CookingMinutes,decimal Calories,decimal ProteinGrams,bool NutritionComplete,AiPlanShortage[] Shopping);
public static class AiPlanCalculator
{
    /// <summary>Simulates FEFO against a private stock copy. Shared batches consume ingredients once.</summary>
    public static AiPlanPreview Preview(AiPlanMeal[] meals,AiContext context)
    {
        var stock=context.Stock.Select(b=>new InventoryBatch{BatchID=b.BatchID,IngredientID=b.IngredientID,UnitID=b.UnitID,RemainingQuantity=b.RemainingQuantity,ExpiresOn=b.ExpiresOn,Version=b.Version}).ToArray();
        var lines=new List<AiPlanShortage>();var minutes=0;var count=0;var calories=0m;var protein=0m;var complete=true;
        foreach(var group in meals.GroupBy(m=>m.BatchKey).OrderBy(g=>g.Min(m=>m.Date)))
        {
            var first=group.First();var portions=group.Sum(m=>m.Portions);
            if(group.Select(m=>(m.RecipeId,m.PreparedBatchId)).Distinct().Count()!=1 || portions<=0){complete=false;continue;}
            var recipe=context.Candidates.FirstOrDefault(r=>r.RecipeId==first.RecipeId);
            if(recipe is null||recipe.Servings is not >0){complete=false;continue;}
            var n=recipe.Nutrition;
            var basis=recipe.Servings.Value;
            if(first.PreparedBatchId is {} preparedId)
            {
                var batch=context.Batches.FirstOrDefault(b=>b.CookingBatchID==preparedId);
                try{n=batch?.NutritionSnapshot is {} snapshot?AiJson.Read<RecipeNutritionSummary>(snapshot):null;basis=n?.Servings??0;}
                catch(System.Text.Json.JsonException){n=null;basis=0;}
            }
            if(n is null||!n.HasCompleteNutrition)complete=false;
            if(n is not null&&basis>0){calories+=n.TotalCalories*portions/basis;protein+=n.TotalProteinGrams*portions/basis;}else complete=false;
            if(first.PreparedBatchId.HasValue)continue;
            count++;minutes+=(recipe.PrepMinutes??0)+(recipe.CookMinutes??0);
            var full=new RecipeFull{RecipeID=recipe.RecipeId,Servings=recipe.Servings,Ingredients=recipe.Ingredients.ToList()};
            var review=InventoryCalculator.ReviewConsumption(full,portions,stock,context.UnitMap,group.Min(m=>m.Date));
            foreach(var line in review.Readiness.Ingredients)lines.Add(new(line.IngredientID,line.Name,line.UnitID,context.Units.FirstOrDefault(u=>u.UnitID==line.UnitID)?.Abbreviation??"",line.Required,Math.Min(line.Required,line.Available),line.Missing,line.NeedsReview));
            foreach(var deduction in review.Deductions)stock.Single(b=>b.BatchID==deduction.BatchID).RemainingQuantity-=deduction.Quantity;
        }
        return new(count,minutes,calories,protein,complete,lines.GroupBy(l=>(l.IngredientId,l.UnitId)).Select(g=>new AiPlanShortage(g.Key.IngredientId,g.First().Name,g.Key.UnitId,g.First().Unit,g.Sum(l=>l.Required),g.Sum(l=>l.FromPantry),g.Sum(l=>l.Missing),g.Any(l=>l.NeedsReview))).ToArray());
    }
}
