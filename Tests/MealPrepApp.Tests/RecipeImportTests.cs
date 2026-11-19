using System.Net;
using MealPrepApp.Models;
using MealPrepApp.Services;
using Xunit;
using SkiaSharp;
namespace MealPrepApp.Tests;
public sealed class RecipeImportTests
{
    private static readonly Ingredient[] Ingredients=[new(){IngredientID=1,Name="Flour",DefaultUnitID=1},new(){IngredientID=2,Name="Milk",DefaultUnitID=2}];
    private static readonly Unit[] Units=[new(){UnitID=1,Abbreviation="g",Name="gram"},new(){UnitID=2,Abbreviation="ml",Name="millilitre"},new(){UnitID=3,Abbreviation="cup",Name="cup"}];
    [Theory] [InlineData("1 1/2 cups Flour",1.5,3)] [InlineData("1½ cup Flour",1.5,3)] [InlineData("½ cup Flour",.5,3)] [InlineData("1,5 g Flour",1.5,1)] [InlineData("1.25g Flour",1.25,1)]
    public void FractionsAndRegionalDecimalsRetainOriginalAndMapUnits(string text,double quantity,int unit) {
        var result=RecipeImportParser.ParseIngredient(text,Ingredients,Units);
        Assert.Equal((decimal)quantity,result.Quantity);Assert.Equal(unit,result.UnitID);Assert.Equal(1,result.IngredientID);Assert.Equal(text,result.Original);
    }
    [Theory] [InlineData("1,000 g Flour")] [InlineData("1-2 g Flour")] [InlineData("Salt to taste")] [InlineData("1/0 cup Flour")]
    public void AmbiguousOrAbsentQuantitiesRemainUnresolved(string text) {
        var result=RecipeImportParser.ParseIngredient(text,Ingredients,Units);Assert.Null(result.Quantity);Assert.Equal(text,result.Original);
    }
    [Fact] public void MissingUnitNeverBecomesAnIngredientsDefaultMassUnit() {
        var result=RecipeImportParser.ParseIngredient("1 Flour",Ingredients,Units);Assert.Equal(1,result.IngredientID);Assert.Null(result.UnitID);
    }
    [Fact] public void PasteSupportsARecipeAndAnIngredientListWithoutDroppingUnquantifiedLines() {
        var recipe=RecipeImportParser.ParsePaste("Pancakes\nIngredients:\n200 g Flour\n100 ml Milk\nInstructions:\nMix.\nCook.",Ingredients,Units);
        Assert.Equal("Pancakes",recipe.Title);Assert.Equal(2,recipe.Ingredients.Count);Assert.Contains("Cook.",recipe.Instructions);
        var list=RecipeImportParser.ParsePaste("Salt to taste\n200 g Flour",Ingredients,Units);
        Assert.Null(list.Title);Assert.Equal(2,list.Ingredients.Count);Assert.Null(list.Ingredients[0].Quantity);
    }
    [Fact] public void StructuredGraphPreservesSourceAndFlattensInstructionSections() {
        var html="""<html><script type="application/ld+json">{"@graph":[{"@type":"WebPage"},{"@type":["Thing","Recipe"],"name":"Pancakes","recipeYield":"4 servings","prepTime":"PT10M","recipeIngredient":["1½ cups Flour"],"recipeInstructions":[{"@type":"HowToSection","itemListElement":[{"@type":"HowToStep","text":"Mix &amp; cook."}]}]}]}</script></html>""";
        var result=RecipeImportParser.ParseStructuredPage(html,Ingredients,Units);Assert.Equal("Pancakes",result.Title);Assert.Equal(4,result.Servings);Assert.Equal(10,result.PrepMinutes);Assert.Equal("Mix & cook.",result.Instructions);Assert.Contains("@graph",result.Original);Assert.Equal(1.5m,Assert.Single(result.Ingredients).Quantity);
        Assert.Throws<InvalidDataException>(()=>RecipeImportParser.ParseStructuredPage("<p>No structured data</p>",Ingredients,Units));
    }
    [Theory]
    [InlineData("""["6"]""", 6)]
    [InlineData("""["6 servings", 6]""", 6)]
    [InlineData("""["6", "8"]""", null)]
    [InlineData("""["6–8 servings"]""", null)]
    [InlineData("""["-6"]""", null)]
    [InlineData("6", 6)]
    public void StructuredYieldSupportsArraysWithoutChoosingBetweenConflictingCounts(string recipeYield, int? servings)
    {
        var html = $$"""<script type="application/ld+json">{"@type":"Recipe","name":"Fixture","recipeYield":{{recipeYield}}}</script>""";
        Assert.Equal(servings, RecipeImportParser.ParseStructuredPage(html, Ingredients, Units).Servings);
    }

    [Fact]
    public void RecipeImageCandidatesPreferRecipeMetadataAndResolveCdnAndRelativeUrls()
    {
        var html = """
            <meta content="/fallback.jpg" property="og:image">
            <script type="application/ld+json">{"@type":"Recipe","name":"Fixture","image":[
              {"contentUrl":"https://cdn.example.test/photo.jpg?size=large&amp;format=jpg"},
              "photos/cover.png",
              "http://example.test/ignored.jpg"
            ]}</script>
            """;
        var recipe = RecipeImportParser.ParseStructuredPage(html, Ingredients, Units);
        string[] expected = [
            "https://cdn.example.test/photo.jpg?size=large&format=jpg",
            "https://example.test/recipes/photos/cover.png",
            "https://example.test/fallback.jpg"
        ];
        Assert.Equal(expected, RecipeLinkImporter.GetPhotoCandidates(new Uri("https://example.test/recipes/sample"), html, recipe));
    }

    [Fact]
    public void ImportedPhotoPreservesFullAspectRatioAndCenterCropsTheThumbnail()
    {
        using var bitmap = new SKBitmap(3000, 2000);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            canvas.Clear(SKColors.Red);
            paint.Color = SKColors.Green;
            canvas.DrawRect(new SKRect(0, 160, 3000, 1840), paint);
        }
        using var input = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var processed = RecipeImageProcessor.ProcessImportedPhoto(input.ToArray());
        using var full = SKBitmap.Decode(processed.Image);
        using var preview = SKBitmap.Decode(processed.Thumbnail);
        Assert.Equal((2048, 1365), (full.Width, full.Height));
        Assert.InRange(processed.Image.Length, 1, RecipeImageProcessor.MaximumImportedPhotoBytes);
        Assert.Equal((320, 180), (preview.Width, preview.Height));
        Assert.True(full.GetPixel(100, 0).Red > 150);
        Assert.True(preview.GetPixel(100, 0).Green > 80);
    }

    [Fact]
    public async Task BlockedOrAbsentPhotoDoesNotFailTheTextImport()
    {
        Assert.Null(await RecipeLinkImporter.FetchPhotoAsync([], CancellationToken.None));
        Assert.Null(await RecipeLinkImporter.FetchPhotoAsync(["https://127.0.0.1/image.jpg"], CancellationToken.None));
    }

    [Theory] [InlineData("127.0.0.1")] [InlineData("10.1.2.3")] [InlineData("172.16.4.5")] [InlineData("192.168.1.1")] [InlineData("169.254.169.254")] [InlineData("100.86.1.1")] [InlineData("0.0.0.0")] [InlineData("::1")] [InlineData("::ffff:127.0.0.1")] [InlineData("fc00::1")] [InlineData("fe80::1")] [InlineData("2002:7f00:1::1")]
    public void InternalAndTunnelAddressesAreRejected(string text)=>Assert.False(RecipePageFetcher.IsPublicAddress(IPAddress.Parse(text)));
    [Theory] [InlineData("http://example.com/recipe")] [InlineData("https://user:password@example.com/recipe")] [InlineData("https://127.0.0.1/")] [InlineData("https://[::1]/")] [InlineData("https://example.com:8443/recipe")]
    public void FetchRequiresPublicHttpsWithoutCredentialsOrCustomPorts(string url)=>Assert.Throws<InvalidDataException>(()=>RecipePageFetcher.ValidateUri(new(url)));
    [Fact] public void NormalPublicAddressesPassPolicyAndLargeInputIsBounded() {
        Assert.True(RecipePageFetcher.IsPublicAddress(IPAddress.Parse("8.8.8.8")));Assert.True(RecipePageFetcher.IsPublicAddress(IPAddress.Parse("2606:4700:4700::1111")));
        Assert.Throws<InvalidDataException>(()=>RecipeImportParser.ParsePaste(new string('x',200001),Ingredients,Units));
    }
}
