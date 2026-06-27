using DiaPredictApp.Pages;
using DiaPredictApp.Services;

namespace DiaPredictApp;

public partial class MainPage : ContentPage
{
    private bool _isPasswordHidden = true;

    public MainPage()
    {
        InitializeComponent();
    }

    private void TogglePasswordVisibility_Clicked(object? sender, EventArgs e)
    {
        _isPasswordHidden = !_isPasswordHidden;

        PasswordEntry.IsPassword = _isPasswordHidden;
        PasswordToggleButton.Text = _isPasswordHidden ? "Lihat" : "Sembunyi";
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        string inputUsername = UsernameEntry.Text?.Trim() ?? "";
        string inputPassword = PasswordEntry.Text ?? "";
        string inputRole = RolePicker.SelectedItem?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(inputUsername) ||
            string.IsNullOrWhiteSpace(inputPassword) ||
            string.IsNullOrWhiteSpace(inputRole))
        {
            await DisplayAlert(
                "Login gagal",
                "Username, password, dan role wajib diisi.",
                "OK"
            );

            return;
        }

        var user = UserDataService.Authenticate(inputUsername, inputPassword, inputRole);

        if (user == null)
        {
            await DisplayAlert(
                "Login gagal",
                "Username, password, atau role tidak sesuai dengan data pendaftaran.",
                "OK"
            );

            return;
        }

        if (!user.FaceRegistered ||
            string.IsNullOrWhiteSpace(user.NormalizedFaceImagePath) ||
            !File.Exists(user.NormalizedFaceImagePath))
        {
            await DisplayAlert(
                "Verifikasi wajah gagal",
                "Akun ini belum memiliki data wajah yang valid. Silakan daftar wajah terlebih dahulu.",
                "OK"
            );

            return;
        }

        Preferences.Set("CurrentUsername", user.Username);
        Preferences.Set("CurrentRole", user.Role);
        Preferences.Set("CurrentFullName", user.FullName);

        await Shell.Current.GoToAsync(nameof(FaceVerificationPage));
    }

    private async void OnRegisterTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }
}