using DiaPredictApp.Models;
using DiaPredictApp.Services;

namespace DiaPredictApp.Pages;

public partial class RegisterPage : ContentPage
{
    private bool _isFaceRegistered = false;
    private string _selectedRole = "";
    private string _registeredFaceImagePath = "";
    private string _registeredNormalizedFaceImagePath = "";

    public RegisterPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        bool tempFaceRegistered = Preferences.Get("TempFaceRegistered", false);
        string tempFaceImagePath = Preferences.Get("TempFaceImagePath", "");
        string tempNormalizedFaceImagePath = Preferences.Get("TempNormalizedFaceImagePath", "");

        if (tempFaceRegistered &&
            !string.IsNullOrWhiteSpace(tempFaceImagePath) &&
            File.Exists(tempFaceImagePath) &&
            !string.IsNullOrWhiteSpace(tempNormalizedFaceImagePath) &&
            File.Exists(tempNormalizedFaceImagePath))
        {
            _isFaceRegistered = true;
            _registeredFaceImagePath = tempFaceImagePath;
            _registeredNormalizedFaceImagePath = tempNormalizedFaceImagePath;

            FaceRegisterButton.Text = "WAJAH TERDAFTAR";
            FaceRegisterButton.BackgroundColor = Color.FromArgb("#DFF8F1");
            FaceRegisterButton.TextColor = Color.FromArgb("#159A6B");

            Preferences.Remove("TempFaceRegistered");
            Preferences.Remove("TempFaceImagePath");
            Preferences.Remove("TempNormalizedFaceImagePath");

            ValidateRegisterButton();
        }
    }

    private void OnInputChanged(object? sender, TextChangedEventArgs e)
    {
        ValidateRegisterButton();
    }

    private void OnPasienRoleClicked(object? sender, EventArgs e)
    {
        _selectedRole = "Pasien";

        PasienRoleButton.BackgroundColor = Color.FromArgb("#0A7C7C");
        PasienRoleButton.TextColor = Colors.White;

        NakesRoleButton.BackgroundColor = Color.FromArgb("#F9FFFD");
        NakesRoleButton.TextColor = Color.FromArgb("#6F8B8F");

        ValidateRegisterButton();
    }

    private void OnNakesRoleClicked(object? sender, EventArgs e)
    {
        _selectedRole = "Nakes";

        NakesRoleButton.BackgroundColor = Color.FromArgb("#0A7C7C");
        NakesRoleButton.TextColor = Colors.White;

        PasienRoleButton.BackgroundColor = Color.FromArgb("#F9FFFD");
        PasienRoleButton.TextColor = Color.FromArgb("#6F8B8F");

        ValidateRegisterButton();
    }

    private async void OnFaceRegisterClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(FaceRegisterPage));
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        if (!IsFormValid())
        {
            await DisplayAlert(
                "Pendaftaran gagal",
                "Pastikan semua data sudah diisi, password sama, role dipilih, dan wajah sudah terdaftar.",
                "OK"
            );

            return;
        }

        string fullName = FullNameEntry.Text?.Trim() ?? "";
        string username = UsernameEntry.Text?.Trim() ?? "";
        string password = PasswordEntry.Text ?? "";
        string role = _selectedRole;

        if (UserDataService.UsernameExists(username))
        {
            await DisplayAlert(
                "Username sudah digunakan",
                "Silakan gunakan username lain.",
                "OK"
            );

            return;
        }

        string safeUsername = MakeSafeFileName(username);
        string faceFolder = Path.Combine(FileSystem.AppDataDirectory, "faces");
        Directory.CreateDirectory(faceFolder);

        string finalRawFacePath = Path.Combine(faceFolder, $"{safeUsername}_raw.jpg");
        string finalNormalizedFacePath = Path.Combine(faceFolder, $"{safeUsername}_normalized.png");

        File.Copy(_registeredFaceImagePath, finalRawFacePath, true);
        File.Copy(_registeredNormalizedFaceImagePath, finalNormalizedFacePath, true);

        UserAccount newUser = new()
        {
            FullName = fullName,
            Username = username,
            Password = password,
            Role = role,
            FaceRegistered = _isFaceRegistered,
            RawFaceImagePath = finalRawFacePath,
            NormalizedFaceImagePath = finalNormalizedFacePath,
            LastRiskResult = role == "Pasien" ? "Belum diprediksi" : "-",
            LastRiskScore = 0,
            LastRiskExplanation = role == "Pasien"
                ? "Pasien belum melakukan prediksi risiko diabetes."
                : "-",
            LastPredictionDateText = "-"
        };

        UserDataService.RegisterUser(newUser);

        await DisplayAlert(
            "Pendaftaran berhasil",
            $"Akun berhasil dibuat sebagai {role}. Silakan login kembali menggunakan username dan password yang sudah didaftarkan.",
            "OK"
        );

        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackToLoginTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void ValidateRegisterButton()
    {
        bool isValid = IsFormValid();

        RegisterButton.IsEnabled = isValid;
        RegisterButton.BackgroundColor = isValid
            ? Color.FromArgb("#0A7C7C")
            : Color.FromArgb("#B8C9C9");

        if (string.IsNullOrWhiteSpace(_selectedRole))
        {
            StatusLabel.Text = "Status: pilih role Pasien atau Nakes terlebih dahulu.";
            StatusLabel.TextColor = Color.FromArgb("#6F8B8F");
            return;
        }

        if (!_isFaceRegistered)
        {
            StatusLabel.Text = "Status: wajah belum didaftarkan.";
            StatusLabel.TextColor = Color.FromArgb("#6F8B8F");
            return;
        }

        if (string.IsNullOrWhiteSpace(_registeredNormalizedFaceImagePath) ||
            !File.Exists(_registeredNormalizedFaceImagePath))
        {
            StatusLabel.Text = "Status: data grayscale wajah belum tersimpan.";
            StatusLabel.TextColor = Color.FromArgb("#FF6B6B");
            return;
        }

        if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            StatusLabel.Text = "Status: password dan konfirmasi password belum sama.";
            StatusLabel.TextColor = Color.FromArgb("#FF6B6B");
            return;
        }

        if (isValid)
        {
            StatusLabel.Text = "Status: data lengkap. Akun siap didaftarkan.";
            StatusLabel.TextColor = Color.FromArgb("#159A6B");
        }
        else
        {
            StatusLabel.Text = "Status: lengkapi semua data terlebih dahulu.";
            StatusLabel.TextColor = Color.FromArgb("#6F8B8F");
        }
    }

    private bool IsFormValid()
    {
        bool isFullNameFilled = !string.IsNullOrWhiteSpace(FullNameEntry.Text);
        bool isUsernameFilled = !string.IsNullOrWhiteSpace(UsernameEntry.Text);
        bool isPasswordFilled = !string.IsNullOrWhiteSpace(PasswordEntry.Text);
        bool isConfirmFilled = !string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text);
        bool isPasswordSame = PasswordEntry.Text == ConfirmPasswordEntry.Text;
        bool isRoleSelected = !string.IsNullOrWhiteSpace(_selectedRole);

        bool isNormalizedFaceAvailable =
            !string.IsNullOrWhiteSpace(_registeredNormalizedFaceImagePath) &&
            File.Exists(_registeredNormalizedFaceImagePath);

        return isFullNameFilled &&
               isUsernameFilled &&
               isPasswordFilled &&
               isConfirmFilled &&
               isPasswordSame &&
               isRoleSelected &&
               _isFaceRegistered &&
               isNormalizedFaceAvailable;
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '_');
        }

        return value.Trim().ToLower();
    }
}