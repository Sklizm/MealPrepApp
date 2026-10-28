namespace MealPrepApp.ViewModels.Auth;

public sealed class ForgotPasswordViewModel : ViewModelBase
{
    public string Identifier { get; set; } = "";
    public string Email { get; set; } = "";

    public Task<bool> ResetAsync(string newPassword, string confirmPassword)
    {
        ErrorMessage = "Contacteaza administratorul pentru recuperarea contului.";
        return Task.FromResult(false);
    }
}
