// Direct invocation for environments that prohibit VSTest local TCP sockets.
// CI uses standard xUnit discovery. UI checks use the same isolated dispatcher as AvaloniaFact.
using System.Reflection;
using MealPrepApp.Tests;
using Avalonia.Headless;

if(args.Contains("--ingredient-catalog-screenshots") || args.Contains("--catalog-screenshots") || args.Any(a => a.StartsWith("--localization-section=")) || args.Contains("--localization-screenshots") || args.Contains("--refinement-screenshots") || args.Contains("--screenshots") || args.Contains("--alignment-screenshots") || args.Contains("--recipe-screenshots")) TestHost.BuildAvaloniaApp().SetupWithoutStarting();
if(args.Contains("--ingredient-catalog-screenshots")) { CatalogRedesignSamples.Render(Path.GetFullPath("dist/catalog-redesign/screenshots")); return 0; }
if(args.Contains("--catalog-screenshots")) { CatalogDialogSamples.Render(Path.GetFullPath("dist/catalog/screenshots")); return 0; }
if(args.Contains("--localization-screenshots") || args.Any(a => a.StartsWith("--localization-section=")))
{
    foreach (var language in new[] { "en", "ru", "ro" })
    {
        MealPrepApp.Services.L.Current.Language = language;
        RedesignSamples.Render(Path.GetFullPath($"docs/localization/screenshots/{language}"), localizationOnly: true, onlySection: args.FirstOrDefault(a => a.StartsWith("--localization-section="))?.Split("=", 2)[1]);
        Console.WriteLine($"Rendered localization matrix: {language}");
    }
    return 0;
}
if (args.Contains("--recipe-screenshots"))
{
    MealPrepApp.Services.L.Current.Language = "en";
    RedesignSamples.Render(Path.GetFullPath("dist/public/screenshots"), recipeOnly: true);
    return 0;
}
if(args.Contains("--alignment-screenshots")) { RedesignSamples.Render(Path.GetFullPath("dist/alignment/after"), alignmentOnly: true); return 0; }
if(args.Contains("--refinement-screenshots")) { RedesignSamples.Render(Path.GetFullPath("dist/refinement/screenshots")); return 0; }
if(args.Contains("--screenshots")) { RedesignSamples.Render(Path.GetFullPath("docs/redesign/screenshots")); return 0; }
using var session = HeadlessUnitTestSession.StartNew(typeof(TestHost), AvaloniaTestIsolationLevel.PerTest);
var filter = args.FirstOrDefault(a => a.StartsWith("--filter="))?[9..];
int count = 0, failures = 0;
var types=args.Contains("--all")?typeof(RegressionTests).Assembly.GetTypes().Where(t=>t.IsClass&&!t.IsAbstract):new[]{args.Contains("--assistant-tests")?typeof(AssistantTests):typeof(RegressionTests)};
var methods=types.SelectMany(t=>t.GetMethods().Select(m=>(Type:t,Method:m)))
 .Where(t=>t.Method.GetCustomAttributes().Any(a=>a.GetType().Name is "FactAttribute" or "AvaloniaFactAttribute" or "TheoryAttribute"))
 .Where(t=>filter is null || (t.Type.Name+"."+t.Method.Name).Contains(filter,StringComparison.OrdinalIgnoreCase))
 .OrderByDescending(t=>t.Method.GetCustomAttributes().Any(a=>a.GetType().Name=="AvaloniaFactAttribute"));
foreach(var test in methods)
{
 var method=test.Method;
 var inline=method.GetCustomAttributes<Xunit.InlineDataAttribute>().ToArray();
 var cases=inline.Length>0?inline.SelectMany(a=>a.GetData(method)):new[]{Array.Empty<object>()};
 foreach(var arguments in cases)
 {
  try
  {
   async Task Invoke(){if(method.Invoke(Activator.CreateInstance(test.Type),arguments) is Task task)await task;}
   if(method.GetCustomAttributes().Any(a=>a.GetType().Name=="AvaloniaFactAttribute"))
    session.Dispatch(async()=>{await Invoke();return 0;},CancellationToken.None).GetAwaiter().GetResult();
   else Task.Run(Invoke).GetAwaiter().GetResult();
   Console.WriteLine($"PASS {test.Type.Name}.{method.Name}");
  }
  catch(Exception ex){failures++;Console.WriteLine($"FAIL {test.Type.Name}.{method.Name}: {ex.InnerException??ex}");}
  count++;
 }
}
Console.WriteLine($"{count-failures}/{count} cases passed (in-process execution with per-test Avalonia sessions; no VSTest discovery).");
return failures==0&&count>0?0:1;
