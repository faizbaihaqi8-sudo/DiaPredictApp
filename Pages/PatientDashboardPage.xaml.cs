using DiaPredictApp.Models;
using DiaPredictApp.Services;

namespace DiaPredictApp.Pages;

public partial class PatientDashboardPage : ContentPage
{
    private string _currentUsername = "";

    public PatientDashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _currentUsername = Preferences.Get("CurrentUsername", "");
        UserAccount? user = UserDataService.FindByUsername(_currentUsername);

        if (user == null)
        {
            WelcomeLabel.Text = "Data pengguna tidak ditemukan.";
            return;
        }

        WelcomeLabel.Text = $"Halo, {user.FullName} (@{user.Username}). Silakan isi data untuk melihat estimasi risiko diabetes.";

        RiskResultLabel.Text = user.LastRiskResult;
        RiskScoreLabel.Text = user.LastRiskResult == "Belum diprediksi"
            ? "Skor: -"
            : $"Skor: {user.LastRiskScore}";

        ExplanationLabel.Text = user.LastRiskExplanation;
        ClinicalSummaryLabel.Text = user.LastClinicalSummary;
    }

    private async void OnPredictClicked(object? sender, EventArgs e)
    {
        bool glucoseValid = double.TryParse(GlucoseEntry.Text, out double glucose);
        bool bmiValid = double.TryParse(BmiEntry.Text, out double bmi);

        if (!glucoseValid || !bmiValid)
        {
            await DisplayAlert(
                "Data belum valid",
                "Pastikan glukosa puasa dan BMI diisi dengan angka.",
                "OK"
            );

            return;
        }

        if (glucose <= 0 || glucose > 500 || bmi <= 0 || bmi > 80)
        {
            await DisplayAlert(
                "Data tidak masuk akal",
                "Periksa kembali nilai glukosa dan BMI. Ada data yang berada di luar rentang wajar.",
                "OK"
            );

            return;
        }

        bool hasFamilyHistory = FamilyHistorySwitch.IsToggled;

        DiabetesPredictionResult result = PredictDiabetesRiskWithSimpleFuzzy(
            glucose,
            bmi,
            hasFamilyHistory
        );

        RiskResultLabel.Text = result.RiskCategory;
        RiskScoreLabel.Text = $"Skor: {result.Score}";
        ClinicalSummaryLabel.Text = result.ClinicalSummary;
        ExplanationLabel.Text = result.Explanation;

        string predictionDateText = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        UserDataService.UpdatePatientDiabetesPrediction(
            _currentUsername,
            0,
            glucose,
            0,
            bmi,
            0,
            0,
            0,
            hasFamilyHistory,
            false,
            result.RiskCategory,
            result.Score,
            result.Explanation,
            result.ClinicalSummary,
            predictionDateText
        );

        AssistantAnswerLabel.Text =
            "Hasil prediksi sudah diperbarui.\n\n" +
            GenerateActionPlan(result.RiskCategory) +
            "\n\nKamu juga bisa bertanya ke DiaAssist. Jawaban akan dibantu menggunakan Gemini API jika API key sudah dikonfigurasi.";

        await DisplayAlert(
            "Prediksi tersimpan",
            "Hasil prediksi risiko diabetes berhasil disimpan dan dapat dilihat oleh Nakes.",
            "OK"
        );
    }

    private static DiabetesPredictionResult PredictDiabetesRiskWithSimpleFuzzy(
        double glucose,
        double bmi,
        bool hasFamilyHistory)
    {
        double glucoseNormal = LeftShoulder(glucose, 90, 105);
        double glucoseMedium = Triangle(glucose, 95, 112, 126);
        double glucoseHigh = RightShoulder(glucose, 120, 126);

        double bmiNormal = LeftShoulder(bmi, 23, 25);
        double bmiOverweight = Triangle(bmi, 24, 27, 30);
        double bmiObese = RightShoulder(bmi, 29, 30);

        double familyYes = hasFamilyHistory ? 1.0 : 0.0;
        double familyNo = hasFamilyHistory ? 0.0 : 1.0;

        List<FuzzyRuleResult> rules = new()
        {
            new FuzzyRuleResult(
                "Risiko Tinggi",
                80,
                glucoseHigh,
                "glukosa puasa berada pada kategori tinggi"
            ),

            new FuzzyRuleResult(
                "Risiko Tinggi",
                80,
                FuzzyAnd(glucoseMedium, FuzzyOr(bmiObese, familyYes)),
                "glukosa mulai meningkat dan diperkuat oleh obesitas atau riwayat keluarga"
            ),

            new FuzzyRuleResult(
                "Risiko Tinggi",
                80,
                FuzzyAnd(bmiObese, familyYes),
                "BMI obesitas disertai riwayat keluarga diabetes"
            ),

            new FuzzyRuleResult(
                "Risiko Sedang",
                55,
                glucoseMedium,
                "glukosa puasa berada pada area yang perlu diperhatikan"
            ),

            new FuzzyRuleResult(
                "Risiko Sedang",
                55,
                FuzzyOr(bmiOverweight, familyYes),
                "BMI berlebih atau terdapat riwayat keluarga diabetes"
            ),

            new FuzzyRuleResult(
                "Risiko Rendah",
                25,
                FuzzyAnd(glucoseNormal, bmiNormal, familyNo),
                "glukosa puasa dan BMI masih relatif baik serta tidak ada riwayat keluarga"
            )
        };

        List<FuzzyRuleResult> activeRules = rules
            .Where(rule => rule.Strength > 0)
            .OrderByDescending(rule => rule.Strength)
            .ToList();

        double totalStrength = activeRules.Sum(rule => rule.Strength);

        double fuzzyRiskScore = totalStrength <= 0
            ? 25
            : activeRules.Sum(rule => rule.Strength * rule.OutputValue) / totalStrength;

        int finalScore = (int)Math.Round(fuzzyRiskScore);

        string riskCategory = finalScore switch
        {
            >= 65 => "Risiko Tinggi",
            >= 40 => "Risiko Sedang",
            _ => "Risiko Rendah"
        };

        string clinicalSummary =
            $"Glukosa puasa: {glucose:F1} mg/dL\n" +
            $"BMI: {bmi:F1}\n" +
            $"Riwayat keluarga diabetes: {(hasFamilyHistory ? "Ada" : "Tidak ada")}";

        string explanation = BuildPatientFriendlyExplanation(
            riskCategory,
            finalScore,
            glucose,
            bmi,
            hasFamilyHistory,
            activeRules
        );

        return new DiabetesPredictionResult(
            riskCategory,
            finalScore,
            explanation,
            clinicalSummary
        );
    }

    private static string BuildPatientFriendlyExplanation(
        string riskCategory,
        int finalScore,
        double glucose,
        double bmi,
        bool hasFamilyHistory,
        List<FuzzyRuleResult> activeRules)
    {
        List<string> factors = new();

        if (glucose >= 126)
        {
            factors.Add($"Glukosa puasa {glucose:F1} mg/dL berada pada kategori tinggi.");
        }
        else if (glucose >= 100)
        {
            factors.Add($"Glukosa puasa {glucose:F1} mg/dL mulai berada pada area yang perlu diperhatikan.");
        }
        else
        {
            factors.Add($"Glukosa puasa {glucose:F1} mg/dL masih relatif baik.");
        }

        if (bmi >= 30)
        {
            factors.Add($"BMI {bmi:F1} menunjukkan obesitas.");
        }
        else if (bmi >= 25)
        {
            factors.Add($"BMI {bmi:F1} menunjukkan berat badan berlebih.");
        }
        else
        {
            factors.Add($"BMI {bmi:F1} masih relatif baik.");
        }

        if (hasFamilyHistory)
        {
            factors.Add("Terdapat riwayat keluarga diabetes, sehingga risiko bisa lebih tinggi.");
        }
        else
        {
            factors.Add("Tidak ada riwayat keluarga diabetes yang dimasukkan.");
        }

        List<string> mainPatterns = activeRules
            .Where(rule => rule.OutputValue >= 55)
            .Take(3)
            .Select(rule => rule.Reason)
            .Distinct()
            .ToList();

        string mainPatternText = mainPatterns.Count == 0
            ? "- Tidak ada faktor besar yang dominan meningkatkan risiko."
            : string.Join("\n", mainPatterns.Select(pattern => $"- {CapitalizeFirstLetter(pattern)}."));

        return
            $"Hasil analisis menunjukkan kategori: {riskCategory}.\n" +
            $"Skor risiko: {finalScore}/100.\n\n" +
            "Faktor yang memengaruhi hasil:\n" +
            string.Join("\n", factors.Select(item => $"- {item}")) +
            "\n\n" +
            "Pola utama yang terbaca:\n" +
            mainPatternText +
            "\n\n" +
            GenerateActionPlan(riskCategory) +
            "\n\nCatatan: hasil ini adalah estimasi risiko awal, bukan diagnosis medis.";
    }

    private static string GenerateActionPlan(string riskCategory)
    {
        if (riskCategory == "Risiko Tinggi")
        {
            return
                "Yang sebaiknya dilakukan setelah hasil:\n" +
                "- Konsultasi dengan tenaga kesehatan untuk pemeriksaan lanjutan.\n" +
                "- Pertimbangkan pemeriksaan ulang glukosa puasa atau HbA1c.\n" +
                "- Kurangi minuman manis dan makanan tinggi gula.\n" +
                "- Perbaiki pola makan dan porsi karbohidrat.\n" +
                "- Tingkatkan aktivitas fisik secara bertahap.\n" +
                "- Pantau hasil secara berkala.";
        }

        if (riskCategory == "Risiko Sedang")
        {
            return
                "Yang sebaiknya dilakukan setelah hasil:\n" +
                "- Mulai perbaiki pola makan sebelum risiko meningkat.\n" +
                "- Kurangi konsumsi gula tambahan.\n" +
                "- Tingkatkan aktivitas fisik secara rutin.\n" +
                "- Pantau berat badan dan gula darah secara berkala.\n" +
                "- Konsultasi jika nilai glukosa terus meningkat.";
        }

        if (riskCategory == "Risiko Rendah")
        {
            return
                "Yang sebaiknya dilakukan setelah hasil:\n" +
                "- Pertahankan pola makan seimbang.\n" +
                "- Tetap aktif bergerak secara rutin.\n" +
                "- Hindari konsumsi minuman manis berlebihan.\n" +
                "- Lakukan pengecekan berkala jika memiliki keluhan atau faktor risiko baru.";
        }

        return
            "Yang sebaiknya dilakukan setelah hasil:\n" +
            "- Lakukan prediksi risiko terlebih dahulu agar saran bisa disesuaikan dengan hasil.";
    }

    private static double Triangle(double x, double a, double b, double c)
    {
        if (x <= a || x >= c)
        {
            return 0;
        }

        if (Math.Abs(x - b) < 0.0001)
        {
            return 1;
        }

        if (x > a && x < b)
        {
            return (x - a) / (b - a);
        }

        return (c - x) / (c - b);
    }

    private static double LeftShoulder(double x, double fullUntil, double zeroAt)
    {
        if (x <= fullUntil)
        {
            return 1;
        }

        if (x >= zeroAt)
        {
            return 0;
        }

        return (zeroAt - x) / (zeroAt - fullUntil);
    }

    private static double RightShoulder(double x, double zeroUntil, double fullAt)
    {
        if (x <= zeroUntil)
        {
            return 0;
        }

        if (x >= fullAt)
        {
            return 1;
        }

        return (x - zeroUntil) / (fullAt - zeroUntil);
    }

    private static double FuzzyAnd(params double[] values)
    {
        return values.Min();
    }

    private static double FuzzyOr(params double[] values)
    {
        return values.Max();
    }

    private static string CapitalizeFirstLetter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return char.ToUpper(text[0]) + text[1..];
    }

    private async void OnAssistantSendClicked(object? sender, EventArgs e)
    {
        string question = AssistantQuestionEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(question))
        {
            AssistantAnswerLabel.Text = "Silakan masukkan pertanyaan terlebih dahulu.";
            return;
        }

        AssistantAnswerLabel.Text = "DiaAssist sedang menghubungi Gemini API...";

        string answer = await GenerateAssistantAnswerAsync(question);
        AssistantAnswerLabel.Text = answer;
    }

    private async void OnQuickExplainClicked(object? sender, EventArgs e)
    {
        AssistantQuestionEntry.Text = "Jelaskan hasil saya";
        AssistantAnswerLabel.Text = "DiaAssist sedang menghubungi Gemini API...";

        string answer = await GenerateAssistantAnswerAsync("Jelaskan hasil saya");
        AssistantAnswerLabel.Text = answer;
    }

    private async void OnQuickGlucoseClicked(object? sender, EventArgs e)
    {
        AssistantQuestionEntry.Text = "Apa itu glukosa?";
        AssistantAnswerLabel.Text = "DiaAssist sedang menghubungi Gemini API...";

        string answer = await GenerateAssistantAnswerAsync("Apa itu glukosa?");
        AssistantAnswerLabel.Text = answer;
    }

    private async void OnQuickBmiClicked(object? sender, EventArgs e)
    {
        AssistantQuestionEntry.Text = "Kenapa BMI berpengaruh?";
        AssistantAnswerLabel.Text = "DiaAssist sedang menghubungi Gemini API...";

        string answer = await GenerateAssistantAnswerAsync("Kenapa BMI berpengaruh?");
        AssistantAnswerLabel.Text = answer;
    }

    private async void OnQuickAdviceClicked(object? sender, EventArgs e)
    {
        AssistantQuestionEntry.Text = "Apa yang harus saya lakukan setelah hasil ini?";
        AssistantAnswerLabel.Text = "DiaAssist sedang menghubungi Gemini API...";

        string answer = await GenerateAssistantAnswerAsync("Apa yang harus saya lakukan setelah hasil ini?");
        AssistantAnswerLabel.Text = answer;
    }

    private async Task<string> GenerateAssistantAnswerAsync(string question)
    {
        UserAccount? user = UserDataService.FindByUsername(_currentUsername);

        if (user == null)
        {
            return "Data pengguna tidak ditemukan. Silakan login ulang.";
        }

        if (user.LastRiskResult == "Belum diprediksi")
        {
            return "Kamu belum melakukan prediksi risiko. Isi data glukosa puasa, BMI, dan riwayat keluarga terlebih dahulu, lalu tekan tombol PREDIKSI RISIKO DIABETES.";
        }

        if (GeminiApiService.IsConfigured())
        {
            return await GeminiApiService.ExplainPredictionAsync(
                question,
                user.LastRiskResult,
                user.LastRiskScore,
                user.LastClinicalSummary,
                user.LastRiskExplanation
            );
        }

        return GenerateLocalAssistantAnswer(question, user);
    }

    private string GenerateLocalAssistantAnswer(string question, UserAccount user)
    {
        string q = question.ToLower();

        if (q.Contains("apa yang harus") ||
            q.Contains("harus dilakukan") ||
            q.Contains("setelah hasil") ||
            q.Contains("saran") ||
            q.Contains("lakukan") ||
            q.Contains("solusi") ||
            q.Contains("tindakan"))
        {
            return GenerateActionPlan(user.LastRiskResult);
        }

        if (q.Contains("hasil") || q.Contains("risiko") || q.Contains("kenapa") || q.Contains("jelaskan"))
        {
            return
                $"Hasil terakhir kamu adalah {user.LastRiskResult} dengan skor {user.LastRiskScore}/100.\n\n" +
                $"Ringkasan data:\n{user.LastClinicalSummary}\n\n" +
                $"Penjelasan hasil:\n{user.LastRiskExplanation}";
        }

        if (q.Contains("glukosa") || q.Contains("gula"))
        {
            return "Glukosa puasa menunjukkan kadar gula darah setelah tidak makan dalam beberapa jam. Jika nilainya tinggi, risiko diabetes dapat meningkat.";
        }

        if (q.Contains("bmi") || q.Contains("berat") || q.Contains("obesitas"))
        {
            return "BMI adalah perbandingan berat badan dan tinggi badan. BMI yang tinggi dapat meningkatkan risiko diabetes karena berkaitan dengan gangguan sensitivitas insulin.";
        }

        if (q.Contains("riwayat") || q.Contains("keluarga") || q.Contains("genetik"))
        {
            return "Riwayat keluarga diabetes berarti ada anggota keluarga yang pernah mengalami diabetes. Faktor ini dapat meningkatkan risiko karena ada pengaruh genetik dan pola hidup keluarga.";
        }

        if (q.Contains("xai") || q.Contains("explainable") || q.Contains("alasan model"))
        {
            return "XAI berarti aplikasi tidak hanya memberi hasil risiko, tetapi juga menjelaskan faktor yang memengaruhi hasil, seperti glukosa, BMI, dan riwayat keluarga.";
        }

        return
            "Aku belum memahami pertanyaan itu. Coba tanyakan tentang hasil risiko, glukosa, BMI, riwayat keluarga, XAI, atau saran setelah mendapatkan hasil.";
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        Preferences.Remove("CurrentUsername");
        Preferences.Remove("CurrentRole");
        Preferences.Remove("CurrentFullName");

        await Shell.Current.GoToAsync("//MainPage");
    }
}

public sealed record DiabetesPredictionResult(
    string RiskCategory,
    int Score,
    string Explanation,
    string ClinicalSummary
);

public sealed record FuzzyRuleResult(
    string Category,
    double OutputValue,
    double Strength,
    string Reason
);