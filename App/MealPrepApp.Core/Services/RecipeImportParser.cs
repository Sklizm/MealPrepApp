using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MealPrepApp.Models;
namespace MealPrepApp.Services;
public sealed record ImportedIngredient(string Original, string IngredientText, decimal? Quantity, int? UnitID, int? IngredientID);
public sealed record ImportedRecipe(string Original, string? Title, string? Description, string Instructions,
    int? Servings, int? PrepMinutes, int? CookMinutes, IReadOnlyList<ImportedIngredient> Ingredients);
public static class RecipeImportParser
{
    private static readonly TimeSpan RegexLimit = TimeSpan.FromMilliseconds(250);
    private static string Normalize(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(char.ToLowerInvariant).ToArray());
    public static ImportedIngredient ParseIngredient(string text, IReadOnlyList<Ingredient> ingredients, IReadOnlyList<Unit> units)
    {
        var normalized = text.Trim().TrimStart('-', '•', '*').Trim();
        foreach (var pair in new[] { ('½'," 1/2"),('¼'," 1/4"),('¾'," 3/4"),('⅓'," 1/3"),('⅔'," 2/3"),('⅛'," 1/8"),('⅜'," 3/8"),('⅝'," 5/8"),('⅞'," 7/8") }) normalized=normalized.Replace(pair.Item1.ToString(),pair.Item2);
        var match = Regex.Match(normalized.Trim(), @"^(?:(?<whole>\d+)\s+)?(?<n>\d+)\s*/\s*(?<d>\d+)|^\d+(?:[.,]\d+)?",RegexOptions.None,RegexLimit);
        decimal? quantity = null; var rest = normalized.Trim();
        if (match.Success) {
            if (match.Groups["d"].Success && decimal.TryParse(match.Groups["n"].Value, out var n) && decimal.TryParse(match.Groups["d"].Value,out var d) && d>0) {
                var wholeText=match.Groups["whole"].Success ? match.Groups["whole"].Value : "0";
                if(decimal.TryParse(wholeText,NumberStyles.None,CultureInfo.InvariantCulture,out var whole) && whole<99999999 && n/d<99999999 && n/d+whole<99999999) quantity=decimal.Round(n/d+whole,4,MidpointRounding.AwayFromZero);
            }
            else if (decimal.TryParse(match.Value.Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value) && !Regex.IsMatch(match.Value,@"[.,]\d{3}$",RegexOptions.None,RegexLimit)) quantity=value;
            rest=rest[match.Length..].Trim();
            if (rest.StartsWith('-') || rest.StartsWith('–') || rest.StartsWith('—')) quantity=null;
            if (quantity is <=0 or >99999999) quantity=null;
        }
        var unitToken = Regex.Match(rest,@"^[\p{L}.]+",RegexOptions.None,RegexLimit).Value.TrimEnd('.');
        var unit = units.FirstOrDefault(u => Normalize(u.Abbreviation)==Normalize(unitToken) || Normalize(u.Name)==Normalize(unitToken));
        if (unit is null && unitToken.EndsWith('s')) unit=units.FirstOrDefault(u=>Normalize(u.Abbreviation)==Normalize(unitToken[..^1]) || Normalize(u.Name)==Normalize(unitToken[..^1]));
        if (unit is not null) rest=rest[(Regex.Match(rest,@"^[\p{L}.]+",RegexOptions.None,RegexLimit).Length)..].Trim();
        var name=Normalize(rest.Trim().TrimEnd('.'));
        var ingredient=ingredients.FirstOrDefault(i=>Normalize(i.Name)==name);
        return new(text,rest,quantity,unit?.UnitID,ingredient?.IngredientID);
    }
    public static ImportedRecipe ParsePaste(string original, IReadOnlyList<Ingredient> ingredients, IReadOnlyList<Unit> units)
    {
        if (original.Length>200000) throw new InvalidDataException(L.Text("UX.ImportTooLarge"));
        string? title=null; var instructions=new List<string>(); var lines=new List<string>(); var inInstructions=false;
        var inputLines=original.Replace("\r\n","\n").Split('\n').Select(l=>l.Trim()).Where(l=>l.Length>0).ToArray();
        for(var index=0;index<inputLines.Length;index++) {
            var line=inputLines[index];
            if(index==0 && (line.StartsWith("Title:",StringComparison.OrdinalIgnoreCase)||line.StartsWith("Titlu:",StringComparison.OrdinalIgnoreCase)||line.StartsWith("Название:",StringComparison.OrdinalIgnoreCase))) {title=line[(line.IndexOf(':')+1)..].Trim();continue;}
            if(index==0 && inputLines.Length>1 && Normalize(inputLines[1].TrimEnd(':')) is "ingredients" or "ingrediente" or "ингредиенты") {title=line;continue;}
            var key=Normalize(line.TrimEnd(':'));
            if (key is "ingredients" or "ingrediente" or "ингредиенты") continue;
            if (key is "instructions" or "method" or "preparation" or "mod de preparare" or "instructiuni" or "приготовление") { inInstructions=true; continue; }
            if (inInstructions) { instructions.Add(line); continue; }
            lines.Add(line);
        }
        // A single unquantified ingredient list remains available as an unresolved row.
        if (lines.Count==0 && instructions.Count==0 && title is not null) { lines.Add(title); title=null; }
        return new(original,title,null,string.Join("\n\n",instructions),null,null,null,lines.Take(300).Select(l=>ParseIngredient(l,ingredients,units)).ToArray());
    }
    public static ImportedRecipe ParseStructuredPage(string html,IReadOnlyList<Ingredient> ingredients,IReadOnlyList<Unit> units)
    {
        if (html.Length>2*1024*1024) throw new InvalidDataException(L.Text("UX.ImportTooLarge"));
        foreach (Match script in Regex.Matches(html,@"<script\b(?<attrs>[^>]{0,2048})>(?<data>.*?)</script\s*>",RegexOptions.IgnoreCase|RegexOptions.Singleline,RegexLimit)) {
            if (!Regex.IsMatch(script.Groups["attrs"].Value,"type\\s*=\\s*['\"]application/ld\\+json['\"]",RegexOptions.IgnoreCase,RegexLimit)) continue;
            try {
                using var json=JsonDocument.Parse(script.Groups["data"].Value,new JsonDocumentOptions{MaxDepth=64});
                var recipe=FindRecipe(json.RootElement);
                if (recipe is not { } r) continue;
                string? String(string key) => r.TryGetProperty(key,out var v) && v.ValueKind==JsonValueKind.String ? Clean(v.GetString()!) : null;
                var recipeLines=r.TryGetProperty("recipeIngredient",out var il) && il.ValueKind==JsonValueKind.Array ? il.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Take(300).Select(v=>ParseIngredient(Clean(v.GetString()!),ingredients,units)).ToArray() : [];
                var instructions=r.TryGetProperty("recipeInstructions",out var ins) ? string.Join("\n\n",Instructions(ins).Take(300)) : "";
                var yieldText=String("recipeYield") ?? (r.TryGetProperty("recipeYield",out var y)&&y.ValueKind==JsonValueKind.Number ? y.GetRawText() : "");
                var servingsMatch=Regex.Match(yieldText,@"\b\d+\b",RegexOptions.None,RegexLimit);
                int? servings=int.TryParse(servingsMatch.Value,out var servingsValue)&&servingsValue is >0 and <=1000 ? servingsValue : null;
                return new(script.Groups["data"].Value,String("name"),String("description"),instructions,servings,Minutes(String("prepTime")),Minutes(String("cookTime")),recipeLines);
            } catch (JsonException) { }
        }
        throw new InvalidDataException(L.Text("UX.UnsupportedImport"));
    }
    private static JsonElement? FindRecipe(JsonElement node) {
        if (node.ValueKind==JsonValueKind.Object) {
            if (node.TryGetProperty("@type",out var type) && ((type.ValueKind==JsonValueKind.String&&IsRecipe(type.GetString())) || (type.ValueKind==JsonValueKind.Array&&type.EnumerateArray().Any(t=>t.ValueKind==JsonValueKind.String&&IsRecipe(t.GetString()))))) return node;
            foreach (var property in node.EnumerateObject()) if (FindRecipe(property.Value) is { } found) return found;
        } else if (node.ValueKind==JsonValueKind.Array) foreach (var child in node.EnumerateArray()) if (FindRecipe(child) is { } found) return found;
        return null;
    }
    private static bool IsRecipe(string? value) => value is "Recipe" or "https://schema.org/Recipe" or "http://schema.org/Recipe";
    private static IEnumerable<string> Instructions(JsonElement node) {
        if (node.ValueKind==JsonValueKind.String) { yield return Clean(node.GetString()!); }
        else if (node.ValueKind==JsonValueKind.Array) foreach (var child in node.EnumerateArray()) foreach (var text in Instructions(child)) yield return text;
        else if (node.ValueKind==JsonValueKind.Object) {
            if (node.TryGetProperty("text",out var text) && text.ValueKind==JsonValueKind.String) yield return Clean(text.GetString()!);
            if (node.TryGetProperty("itemListElement",out var children)) foreach (var child in Instructions(children)) yield return child;
        }
    }
    private static string Clean(string text) => WebUtility.HtmlDecode(Regex.Replace(text,"<[^>]+>"," ",RegexOptions.None,RegexLimit)).Trim();
    private static int? Minutes(string? value) { try { if (value is null) return null; var n=System.Xml.XmlConvert.ToTimeSpan(value).TotalMinutes; return n is >=0 and <=10080 ? (int)Math.Ceiling(n) : null; } catch (FormatException) { return null; } }
}
