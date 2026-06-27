using DiaPredictApp.Services;

namespace DiaPredictApp.Pages;

public partial class FaceVerificationPage : ContentPage
{
    private string _verificationFacePath = "";

    public FaceVerificationPage()
    {
        InitializeComponent();
    }

    private async void OnCaptureVerificationClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(
                    "Kamera tidak tersedia",
                    "Perangkat ini tidak mendukung pengambilan foto langsung. Gunakan tombol Pilih Foto.",
                    "OK"
                );

                return;
            }

            FileResult? photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Ambil Foto Verifikasi"
            });

            if (photo == null)
            {
                return;
            }

            string savedPath = await SavePhotoToAppDataAsync(photo, "verification_face_raw.jpg");

            _verificationFacePath = savedPath;
            VerificationPreviewImage.Source = ImageSource.FromFile(savedPath);
            EmptyPreviewLayout.IsVisible = false;

            VerificationStatusLabel.Text = "Status: foto verifikasi berhasil diambil";
            VerificationStatusLabel.TextColor = Color.FromArgb("#159A6B");

            VerifyButton.IsEnabled = true;
            VerifyButton.BackgroundColor = Color.FromArgb("#0A7C7C");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Gagal membuka kamera",
                $"Terjadi masalah saat membuka kamera.\n\nDetail: {ex.Message}\n\nGunakan tombol Pilih Foto sebagai cadangan.",
                "OK"
            );
        }
    }

    private async void OnPickVerificationClicked(object? sender, EventArgs e)
    {
        try
        {
            IReadOnlyList<FileResult> photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Pilih Foto Verifikasi",
                SelectionLimit = 1
            });

            FileResult? photo = photos.FirstOrDefault();

            if (photo == null)
            {
                return;
            }

            string savedPath = await SavePhotoToAppDataAsync(photo, "verification_face_raw.jpg");

            _verificationFacePath = savedPath;
            VerificationPreviewImage.Source = ImageSource.FromFile(savedPath);
            EmptyPreviewLayout.IsVisible = false;

            VerificationStatusLabel.Text = "Status: foto verifikasi berhasil dipilih";
            VerificationStatusLabel.TextColor = Color.FromArgb("#159A6B");

            VerifyButton.IsEnabled = true;
            VerifyButton.BackgroundColor = Color.FromArgb("#0A7C7C");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Gagal memilih foto",
                $"Terjadi masalah saat memilih foto.\n\nDetail: {ex.Message}",
                "OK"
            );
        }
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        string currentUsername = Preferences.Get("CurrentUsername", "");
        var user = UserDataService.FindByUsername(currentUsername);

        if (user == null)
        {
            await DisplayAlert(
                "Verifikasi gagal",
                "Data pengguna yang sedang login tidak ditemukan.",
                "OK"
            );

            await Shell.Current.GoToAsync("//MainPage");
            return;
        }

        string registeredNormalizedFacePath = user.NormalizedFaceImagePath;

        if (!user.FaceRegistered ||
            string.IsNullOrWhiteSpace(registeredNormalizedFacePath) ||
            !File.Exists(registeredNormalizedFacePath))
        {
            VerificationStatusLabel.Text = "Status: data wajah pendaftaran tidak ditemukan";
            VerificationStatusLabel.TextColor = Color.FromArgb("#FF6B6B");

            await DisplayAlert(
                "Verifikasi gagal",
                "Data wajah pendaftaran tidak ditemukan. Silakan daftar ulang.",
                "OK"
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(_verificationFacePath) ||
            !File.Exists(_verificationFacePath))
        {
            VerificationStatusLabel.Text = "Status: foto verifikasi belum tersedia";
            VerificationStatusLabel.TextColor = Color.FromArgb("#FF6B6B");

            await DisplayAlert(
                "Verifikasi gagal",
                "Ambil atau pilih foto verifikasi terlebih dahulu.",
                "OK"
            );

            return;
        }

        string verificationNormalizedFacePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "verification_face_normalized.png"
        );

        bool processSuccess = FaceRecognitionService.TryCreateNormalizedFaceImage(
            _verificationFacePath,
            verificationNormalizedFacePath,
            out string processMessage
        );

        if (!processSuccess)
        {
            VerificationStatusLabel.Text = "Status: foto verifikasi gagal diproses";
            VerificationStatusLabel.TextColor = Color.FromArgb("#FF6B6B");

            await DisplayAlert(
                "Verifikasi gagal",
                processMessage,
                "OK"
            );

            return;
        }

        VerifyButton.IsEnabled = false;
        VerifyButton.BackgroundColor = Color.FromArgb("#B8C9C9");

        VerificationStatusLabel.Text = "Status: membandingkan wajah grayscale...";
        VerificationStatusLabel.TextColor = Color.FromArgb("#0A7C7C");

        await Task.Delay(800);

        FaceComparisonResult result = FaceRecognitionService.CompareNormalizedFaceImages(
            registeredNormalizedFacePath,
            verificationNormalizedFacePath
        );

        if (!result.IsMatch)
        {
            VerificationStatusLabel.Text = "WAJAH TIDAK COCOK";
            VerificationStatusLabel.TextColor = Color.FromArgb("#FF6B6B");

            VerifyButton.IsEnabled = true;
            VerifyButton.BackgroundColor = Color.FromArgb("#0A7C7C");

            await DisplayAlert(
                "Verifikasi gagal",
                $"Wajah tidak cocok dengan data pendaftaran.\n\nDifference score: {result.DifferenceScore:F2}\nThreshold: {result.Threshold:F2}",
                "OK"
            );

            return;
        }

        VerificationStatusLabel.Text = "WAJAH TERVERIFIKASI!";
        VerificationStatusLabel.TextColor = Color.FromArgb("#159A6B");

        await DisplayAlert(
            "Verifikasi berhasil",
            $"Wajah cocok.\n\nDifference score: {result.DifferenceScore:F2}\nThreshold: {result.Threshold:F2}\n\nKamu akan diarahkan ke dashboard.",
            "OK"
        );

        if (user.Role == "Nakes")
        {
            await Shell.Current.GoToAsync(nameof(NakesDashboardPage));
        }
        else
        {
            await Shell.Current.GoToAsync(nameof(PatientDashboardPage));
        }
    }

    private async void OnFailedSimulationClicked(object? sender, EventArgs e)
    {
        VerificationStatusLabel.Text = "WAJAH TIDAK COCOK";
        VerificationStatusLabel.TextColor = Color.FromArgb("#FF6B6B");

        await DisplayAlert(
            "Verifikasi gagal",
            "Wajah tidak cocok dengan data pendaftaran.",
            "OK"
        );
    }

    private async void OnBackToLoginClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }

    private static async Task<string> SavePhotoToAppDataAsync(FileResult photo, string fileName)
    {
        string targetPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        await using Stream sourceStream = await photo.OpenReadAsync();
        await using FileStream targetStream = File.Create(targetPath);

        await sourceStream.CopyToAsync(targetStream);

        return targetPath;
    }
}