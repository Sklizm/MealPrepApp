// Direct invocation for environments that prohibit VSTest local TCP sockets.
// CI uses standard xUnit discovery. UI checks use the same isolated dispatcher as AvaloniaFact.
using System.Reflection;
using MealPrepApp.Tests;
using Avalonia.Headless;

if(args.Contains("--refinement-screenshots") || args.Contains("--screenshots") || args.Contains("--alignment-screenshots")) TestHost.BuildAvaloniaApp().SetupWithoutStarting();
if(args.Contains("--alignment-screenshots")) { RedesignSamples.Render(Path.GetFullPath("dist/alignment/after"), alignmentOnly: true); return 0; }
if(args.Contains("--refinement-screenshots")) { RedesignSamples.Render(Path.GetFullPath("dist/refinement/screenshots")); return 0; }
if(args.Contains("--screenshots")) { RedesignSamples.Render(Path.GetFullPath("docs/redesign/screenshots")); return 0; }
using var session = HeadlessUnitTestSession.StartNew(typeof(TestHost), AvaloniaTestIsolationLevel.PerTest);
var filter = args.FirstOrDefault(a => a.StartsWith("--filter="))?[9..];
var tests = new RegressionTests();
int count = 0, failures = 0;
foreach (var method in typeof(RegressionTests).GetMethods()
    .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "AvaloniaFactAttribute"))
    .Where(m => filter == null || m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
    .OrderByDescending(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "AvaloniaFactAttribute")))
{
    try
    {
        if (method.GetCustomAttributes().Any(a => a.GetType().Name == "AvaloniaFactAttribute"))
        {
            session.Dispatch(async () =>
            {
                if (method.Invoke(tests, null) is Task uiTask) await uiTask;
                return 0;
            }, CancellationToken.None).GetAwaiter().GetResult();
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
Console.WriteLine($"{count - failures}/{count} regression methods passed (in-process execution with per-test Avalonia sessions; no VSTest discovery).");
return failures == 0 ? 0 : 1;
