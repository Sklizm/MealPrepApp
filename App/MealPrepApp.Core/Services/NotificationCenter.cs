using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MealPrepApp.Services;
public sealed partial class NotificationCenter:ObservableObject
{
    [ObservableProperty] private string? _message;
    [ObservableProperty] private bool _isError;
    public bool HasMessage=>!string.IsNullOrWhiteSpace(Message);
    partial void OnMessageChanged(string? value)=>OnPropertyChanged(nameof(HasMessage));
    public void Post(string message,bool error=false){IsError=error;Message=message;}
    [RelayCommand] private void Dismiss()=>Message=null;
}
