using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MealPrepApp.Services;
public sealed partial class NotificationCenter:LocalizedObservableObject
{
    private string? _message;
    [LocalizedProperty]
    public string? Message { get => _content?.ToString() ?? _message; set { _content = null; _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); } }
    [ObservableProperty] private bool _isError;
    public bool HasMessage=>!string.IsNullOrWhiteSpace(Message);
    private LocalizedMessage? _content;
    public void Post(LocalizedMessage message, bool error = false) { IsError = error; _content = message; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(HasMessage)); }
    public void Post(string message,bool error=false){_content=null;IsError=error;Message=message;}
    [RelayCommand] private void Dismiss()=>Message=null;
}
