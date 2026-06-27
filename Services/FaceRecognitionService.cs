using OpenCvSharp;
using CvRect = OpenCvSharp.Rect;
using CvSize = OpenCvSharp.Size;
using CvScalar = OpenCvSharp.Scalar;

namespace DiaPredictApp.Services;

public static class FaceRecognitionService
{
    public const int ImageSize = 160;

    // Semakin kecil angka ini, sistem makin ketat.
    
    public const double MaxDifferenceThreshold = 45.0;

    public static bool TryCreateNormalizedFaceImage(
        string sourceImagePath,
        string outputImagePath,
        out string message)
    {
        message = "";

        try
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                message = "File gambar sumber tidak ditemukan.";
                return false;
            }

            using Mat source = Cv2.ImRead(sourceImagePath, ImreadModes.Color);

            if (source.Empty())
            {
                message = "Gambar tidak dapat dibaca oleh OpenCV.";
                return false;
            }

            using Mat cropped = CropCenterSquare(source);

            using Mat gray = new Mat();
            Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGR2GRAY);

            using Mat resized = new Mat();
            Cv2.Resize(gray, resized, new CvSize(ImageSize, ImageSize));

            using Mat equalized = new Mat();
            Cv2.EqualizeHist(resized, equalized);

            using Mat blurred = new Mat();
            Cv2.GaussianBlur(equalized, blurred, new CvSize(3, 3), 0);

            string? directory = Path.GetDirectoryName(outputImagePath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            bool saved = Cv2.ImWrite(outputImagePath, blurred);

            if (!saved)
            {
                message = "Gambar hasil grayscale gagal disimpan.";
                return false;
            }

            message = "Gambar wajah berhasil diproses menjadi grayscale.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Terjadi error saat memproses gambar: {ex.Message}";
            return false;
        }
    }

    public static FaceComparisonResult CompareNormalizedFaceImages(
        string registeredFacePath,
        string verificationFacePath,
        double threshold = MaxDifferenceThreshold)
    {
        if (string.IsNullOrWhiteSpace(registeredFacePath) || !File.Exists(registeredFacePath))
        {
            return new FaceComparisonResult(
                false,
                999,
                threshold,
                "File wajah pendaftaran tidak ditemukan."
            );
        }

        if (string.IsNullOrWhiteSpace(verificationFacePath) || !File.Exists(verificationFacePath))
        {
            return new FaceComparisonResult(
                false,
                999,
                threshold,
                "File wajah verifikasi tidak ditemukan."
            );
        }

        using Mat registered = Cv2.ImRead(registeredFacePath, ImreadModes.Grayscale);
        using Mat verification = Cv2.ImRead(verificationFacePath, ImreadModes.Grayscale);

        if (registered.Empty() || verification.Empty())
        {
            return new FaceComparisonResult(
                false,
                999,
                threshold,
                "Salah satu gambar wajah tidak dapat dibaca."
            );
        }

        using Mat registeredResized = new Mat();
        using Mat verificationResized = new Mat();

        Cv2.Resize(registered, registeredResized, new CvSize(ImageSize, ImageSize));
        Cv2.Resize(verification, verificationResized, new CvSize(ImageSize, ImageSize));

        using Mat difference = new Mat();
        Cv2.Absdiff(registeredResized, verificationResized, difference);

        CvScalar meanDifference = Cv2.Mean(difference);
        double differenceScore = meanDifference.Val0;

        bool isMatch = differenceScore <= threshold;

        string message = isMatch
            ? $"Wajah cocok. Difference score: {differenceScore:F2}, threshold: {threshold:F2}."
            : $"Wajah tidak cocok. Difference score: {differenceScore:F2}, threshold: {threshold:F2}.";

        return new FaceComparisonResult(
            isMatch,
            differenceScore,
            threshold,
            message
        );
    }

    private static Mat CropCenterSquare(Mat source)
    {
        int width = source.Width;
        int height = source.Height;
        int size = Math.Min(width, height);

        int x = (width - size) / 2;
        int y = (height - size) / 2;

        CvRect roi = new CvRect(x, y, size, size);

        return new Mat(source, roi).Clone();
    }
}

public sealed record FaceComparisonResult(
    bool IsMatch,
    double DifferenceScore,
    double Threshold,
    string Message
);