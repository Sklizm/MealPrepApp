using System.Diagnostics;
using System.Security;
using MealPrepApp.Services;
namespace MealPrepApp.Desktop.Services;
/// <summary>Best-effort native notification. The shell always retains the in-app alarm.</summary>
public static class TimerAlarmService
{
    public static async Task<bool> NotifyAsync(string name) {
        try {
            ProcessStartInfo start;
            if (OperatingSystem.IsMacOS()) {
                static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";
                start = new("/usr/bin/osascript"); start.ArgumentList.Add("-e");
                start.ArgumentList.Add($"display notification {Quote(name)} with title {Quote(L.Text("Text.TimeIsUp"))} sound name \"Glass\"");
            }
            else if (OperatingSystem.IsWindows()) {
                start = new("powershell.exe"); start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("$ErrorActionPreference='Stop'; Add-Type -AssemblyName System; [System.Media.SystemSounds]::Exclamation.Play(); [Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] > $null; $xml=New-Object Windows.Data.Xml.Dom.XmlDocument; $xml.LoadXml('<toast><visual><binding template=\"ToastGeneric\"><text>MealPrepApp</text><text>'+[System.Security.SecurityElement]::Escape($env:MEALPREP_TIMER_NAME)+'</text></binding></visual><audio src=\"ms-winsoundevent:Notification.Default\"/></toast>'); [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('MealPrepApp').Show([Windows.UI.Notifications.ToastNotification]::new($xml))");
                start.Environment["MEALPREP_TIMER_NAME"] = name;
            }
            else {
                start = new("notify-send"); foreach (var arg in new[] { "--app-name=MealPrepApp", "--hint=string:sound-name:complete", L.Text("Text.TimeIsUp"), name }) start.ArgumentList.Add(arg);
                _ = PlayLinuxSound();
            }
            start.UseShellExecute = false; start.CreateNoWindow = true; start.RedirectStandardError = true; start.RedirectStandardOutput = true;
            using var process = Process.Start(start); if (process is null) return false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(true); return false; }
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or SecurityException) { return false; }
    }
    private static async Task PlayLinuxSound() {
        try {
            var info = new ProcessStartInfo("canberra-gtk-play") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            info.ArgumentList.Add("-i"); info.ArgumentList.Add("complete"); using var process = Process.Start(info);
            if (process is null) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(timeout.Token); } catch (OperationCanceledException) { process.Kill(true); }
        } catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException) { }
    }
}
