// Direct invocation for environments that prohibit VSTest local TCP sockets.
// CI continues to use the standard xUnit runner. Keep UI checks on the main thread.
using System.Reflection;
using MealPrepApp.Tests;

TestHost.BuildAvaloniaApp().SetupWithoutStarting();
if(args.Contains("--refinement-screenshots")) { RedesignSamples.Render(Path.GetFullPath("dist/refinement/screenshots")); return 0; }
if(args.Contains("--screenshots")) { RedesignSamples.Render(Path.GetFullPath("docs/redesign/screenshots")); return 0; }
var tests = new RegressionTests();
int count = 0, failures = 0;
foreach (var method in typeof(RegressionTests).GetMethods()
    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "AvaloniaFactAttribute"))
    .OrderByDescending(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "AvaloniaFactAttribute")))
{
    try
    {
        if (method.GetCustomAttributes().Any(a => a.GetType().Name == "AvaloniaFactAttribute"))
        {
            if (method.Invoke(tests, null) is Task uiTask) uiTask.GetAwaiter().GetResult();
        }
        else
        {
            Task.Run(async () =>
            {
                if (method.Invoke(tests, null) is Task task) await task;
            }).GetAwaiter().GetResult();
        }
        Console.WriteLine($"PASS {method.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {method.Name}: {ex.InnerException ?? ex}");
    }
    count++;
}
Console.WriteLine($"{count - failures}/{count} regression methods passed (direct in-process execution; no VSTest discovery).");
return failures == 0 ? 0 : 1;
