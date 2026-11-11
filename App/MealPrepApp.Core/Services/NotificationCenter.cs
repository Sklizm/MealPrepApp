using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MealPrepApp.Services;
public sealed partial class NotificationCenter:LocalizedObservableObject
{
    private Func<Task>? _undo;
    public bool HasUndo => _undo is not null;
    public void Post(LocalizedMessage message,Func<Task> undo) { Post(message); _undo=undo;OnPropertyChanged(nameof(HasUndo)); }
    [RelayCommand] private async Task Undo() {
        if(_undo is not { } operation)return;
        try { await operation();Post(L.Message("UX.Undone")); }
        catch(Exception ex) { AppDiagnostics.Record(ex);Post(ex is MealPrepApp.Data.AppDbException db?db.LocalizedMessage:L.Message("UX.UndoFailed"),true); }
    }
    private string? _message;
    [LocalizedProperty]
    public string? Message { get => _content?.ToString() ?? _message; set { _content = null; _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); } }
    [ObservableProperty] private bool _isError;
    public bool HasMessage=>!string.IsNullOrWhiteSpace(Message);
    private LocalizedMessage? _content;
    public void Post(LocalizedMessage message, bool error = false) { _undo=null;OnPropertyChanged(nameof(HasUndo)); IsError = error; _content = message; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(HasMessage)); }
    public void Post(string message,bool error=false){_undo=null;OnPropertyChanged(nameof(HasUndo));_content=null;IsError=error;Message=message;}
    [RelayCommand] private void Dismiss() { _undo=null;Message=null;OnPropertyChanged(nameof(HasUndo)); }
}
