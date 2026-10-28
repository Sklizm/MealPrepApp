namespace MealPrepApp.Models;
public sealed class WeekCopyPreview
{
    public int RecipeID {get;set;}
    public int CategoryID {get;set;}
    public DateTime PlannedDate {get;set;}
    public int? Servings {get;set;}
    public string RecipeTitle {get;set;}="";
    public string CategoryName {get;set;}="";
    public bool IsDuplicate {get;set;}
    public byte[] PreviewToken {get;set;}=Array.Empty<byte>();
}
