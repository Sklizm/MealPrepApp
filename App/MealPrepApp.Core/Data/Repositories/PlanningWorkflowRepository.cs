using System.Text.Json;
using MealPrepApp.Models;
namespace MealPrepApp.Data.Repositories;
public sealed record WeekTemplate(int TemplateID,string Name,long Version,DateTime CreatedAt);
public class PlanningWorkflowRepository(IDbConnectionFactory factory) : InventoryRepository(factory)
{
    public virtual Task<IReadOnlyList<WeekTemplate>> GetTemplatesAsync(int userId) => QueryProcAsync<WeekTemplate>("sp_GetWeekTemplates",new {UserID=userId});
    public virtual Task<MutationResult> SaveTemplateAsync(int userId,Guid operation,string name,DateTime start) => Mutation("sp_SaveWeekTemplate",new {UserID=userId,OperationID=operation,Name=name,WeekStart=start.Date});
    public virtual Task<MutationResult> ApplyTemplateAsync(int userId,Guid operation,WeekTemplate template,DateTime start) => Mutation("sp_ApplyWeekTemplate",new {UserID=userId,OperationID=operation,template.TemplateID,ExpectedVersion=template.Version,WeekStart=start.Date});
    public virtual Task<MutationResult> MoveAsync(int userId,Guid operation,MealPlanEntry entry,DateTime date,int slot) => Mutation("sp_MovePlannedMeal",new {UserID=userId,OperationID=operation,entry.MealPlanEntryID,ExpectedVersion=entry.Version,PlannedDate=date.Date,CategoryID=slot});
    public virtual Task<MutationResult> UndoMoveAsync(int userId,Guid operation,int moveId) => Mutation("sp_UndoPlannerMove",new {UserID=userId,OperationID=operation,MoveID=moveId});
}
