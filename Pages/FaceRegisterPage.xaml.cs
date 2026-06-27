using DiaPredictApp.Services;

namespace DiaPredictApp.Pages;

public partial class FaceRegisterPage : ContentPage
{
    private string _capturedFacePath = "";

    public FaceRegisterPage()
    {
        InitializeComponent();
    }

    private async void OnCaptureFaceClicked(object? sender, EventArgs e)
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
                Title = "Ambil Foto Wajah"
            });

            if (photo == null)
            {
                return;
            }

            string savedPath = await SavePhotoToAppDataAsync(photo, "registered_face_raw.jpg");

            _capturedFacePath = savedPath;
            FacePreviewImage.Source = ImageSource.FromFile(savedPath);
            EmptyPreviewLayout.IsVisible = false;

            CameraStatusLabel.Text = "Status: foto wajah berhasil diambil";
            CameraStatusLabel.TextColor = Color.FromArgb("#159A6B");

            SaveFaceButton.IsEnabled = true;
            SaveFaceButton.BackgroundColor = Color.FromArgb("#0A7C7C");
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

    private async void OnPickFaceClicked(object? sender, EventArgs e)
    {
        try
        {
            IReadOnlyList<FileResult> photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Pilih Foto Wajah",
                SelectionLimit = 1
            });

            FileResult? photo = photos.FirstOrDefault();

            if (photo == null)
            {
                return;
            }

            string savedPath = await SavePhotoToAppDataAsync(photo, "registered_face_raw.jpg");

            _capturedFacePath = savedPath;
            FacePreviewImage.Source = ImageSource.FromFile(savedPath);
            EmptyPreviewLayout.IsVisible = false;

            CameraStatusLabel.Text = "Status: foto wajah berhasil dipilih";
            CameraStatusLabel.TextColor = Color.FromArgb("#159A6B");

            SaveFaceButton.IsEnabled = true;
            SaveFaceButton.BackgroundColor = Color.FromArgb("#0A7C7C");
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

    private async void OnSaveFaceClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_capturedFacePath) || !File.Exists(_capturedFacePath))
        {
            await DisplayAlert(
                "Foto belum tersedia",
                "Ambil atau pilih foto wajah terlebih dahulu.",
                "OK"
            );

            return;
        }

        string normalizedFacePath = Path.Combine(
            FileSystem.AppDataDirectory,
            "registered_face_normalized.png"
        );

        bool processSuccess = FaceRecognitionService.TryCreateNormalizedFaceImage(
            _capturedFacePath,
            normalizedFacePath,
            out string processMessage
        );

        if (!processSuccess)
        {
            await DisplayAlert(
                "Wajah gagal diproses",
                processMessage,
                "OK"
            );

            return;
        }

        Preferences.Set("TempFaceRegistered", true);
        Preferences.Set("TempFaceImagePath", _capturedFacePath);
        Preferences.Set("TempNormalizedFaceImagePath", normalizedFacePath);

        CameraStatusLabel.Text = "WAJAH TERDAFTAR!";
        CameraStatusLabel.TextColor = Color.FromArgb("#159A6B");

        await DisplayAlert(
            "Wajah terdaftar",
            "Foto wajah berhasil diproses menjadi grayscale dan disimpan. Kamu akan dikembalikan ke halaman daftar dalam 3 detik.",
            "OK"
        );

        await Task.Delay(3000);

        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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