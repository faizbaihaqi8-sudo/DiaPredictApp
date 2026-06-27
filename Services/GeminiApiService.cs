using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DiaPredictApp.Services;

public static class GeminiApiService
{
    // Isi API key Gemini kamu di sini.
    // Jangan upload API key ini ke GitHub.
    private const string ApiKey = "ISI_API_KEY_GEMINI_KAMU_DI_SINI";

    // Model Gemini yang digunakan untuk DiaAssist.
    private const string ModelName = "gemini-3.5-flash";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(ApiKey)
               && !ApiKey.Contains("ISI_API_KEY", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<string> ExplainPredictionAsync(
        string patientQuestion,
        string riskResult,
        int riskScore,
        string clinicalSummary,
        string localExplanation)
    {
        if (!IsConfigured())
        {
            return
                "Gemini API belum dikonfigurasi.\n\n" +
                "Masukkan API key Gemini terlebih dahulu di file Services/GeminiApiService.cs pada bagian ApiKey.";
        }

        string prompt = BuildPatientExplanationPrompt(
            patientQuestion,
            riskResult,
            riskScore,
            clinicalSummary,
            localExplanation
        );

        GeminiRequest requestBody = new()
        {
            Contents =
            [
                new GeminiContent
                {
                    Parts =
                    [
                        new GeminiPart
                        {
                            Text = prompt
                        }
                    ]
                }
            ],
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = 0.30,
                MaxOutputTokens = 450
            }
        };

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{ModelName}:generateContent";

        try
        {
            string jsonBody = JsonSerializer.Serialize(requestBody, JsonOptions);

            using HttpRequestMessage request = new(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", ApiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await HttpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return
                    "Gemini API gagal dihubungi.\n\n" +
                    $"Status: {(int)response.StatusCode} {response.ReasonPhrase}\n\n" +
                    "Kemungkinan penyebab:\n" +
                    "- API key salah atau belum aktif.\n" +
                    "- Model Gemini tidak tersedia di akun kamu.\n" +
                    "- Internet belum aktif.\n" +
                    "- Kuota API habis.\n\n" +
                    "Detail teknis:\n" +
                    responseText;
            }

            GeminiResponse? parsedResponse = JsonSerializer.Deserialize<GeminiResponse>(responseText, JsonOptions);

            string? answer = parsedResponse?
                .Candidates?
                .FirstOrDefault()?
                .Content?
                .Parts?
                .FirstOrDefault()?
                .Text;

            if (string.IsNullOrWhiteSpace(answer))
            {
                return
                    "Gemini API berhasil dihubungi, tetapi jawaban kosong.\n\n" +
                    "Coba ulangi pertanyaan dengan kalimat yang lebih jelas.";
            }

            return CleanGeminiText(answer);
        }
        catch (TaskCanceledException)
        {
            return
                "Koneksi ke Gemini API terlalu lama atau timeout.\n\n" +
                "Periksa koneksi internet, lalu coba lagi.";
        }
        catch (Exception ex)
        {
            return
                "Terjadi error saat menghubungi Gemini API.\n\n" +
                $"Detail error: {ex.Message}";
        }
    }

    private static string BuildPatientExplanationPrompt(
        string patientQuestion,
        string riskResult,
        int riskScore,
        string clinicalSummary,
        string localExplanation)
    {
        return
            "You are DiaAssist, a patient-friendly health explanation assistant inside a diabetes risk screening prototype.\n\n" +

            "Important safety rules:\n" +
            "1. Do not diagnose the patient.\n" +
            "2. Do not prescribe medication.\n" +
            "3. Do not replace a doctor or healthcare professional.\n" +
            "4. Explain the result in simple Indonesian language.\n" +
            "5. Focus on education, risk awareness, and general lifestyle suggestions.\n" +
            "6. Always recommend consulting healthcare professionals for medical confirmation.\n" +
            "7. Do not mention fuzzy logic, algorithm details, source code, or API technical details unless the user specifically asks.\n" +
            "8. Do not use Markdown formatting such as bold text, tables, headings, code blocks, or numbered headings.\n" +
            "9. Write in plain text only.\n" +
            "10. Keep the answer concise, maximum 5 short bullet points plus 1 closing note.\n\n" +

            "Patient question:\n" +
            patientQuestion + "\n\n" +

            "Prediction result from the application:\n" +
            $"- Risk category: {riskResult}\n" +
            $"- Risk score: {riskScore}/100\n\n" +

            "Patient data summary:\n" +
            clinicalSummary + "\n\n" +

            "Local explanation generated by the application:\n" +
            localExplanation + "\n\n" +

            "Task:\n" +
            "Answer the patient's question in Bahasa Indonesia. Use a calm, clear, and supportive tone. " +
            "Use short bullet points. Make it understandable for laypeople.";
    }

    private static string CleanGeminiText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        string cleaned = text.Trim();

        cleaned = Regex.Replace(cleaned, @"\*\*(.*?)\*\*", "$1");
        cleaned = Regex.Replace(cleaned, @"__(.*?)__", "$1");
        cleaned = Regex.Replace(cleaned, @"^\s*#{1,6}\s*", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*[\*\u2022]\s+", "- ", RegexOptions.Multiline);

        cleaned = cleaned
            .Replace("```", "")
            .Replace("`", "")
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();

        while (cleaned.Contains("\n\n\n"))
        {
            cleaned = cleaned.Replace("\n\n\n", "\n\n");
        }

        return cleaned;
    }

    private sealed class GeminiRequest
    {
        public List<GeminiContent> Contents { get; set; } = [];
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    private sealed class GeminiContent
    {
        public List<GeminiPart> Parts { get; set; } = [];
    }

    private sealed class GeminiPart
    {
        public string Text { get; set; } = "";
    }

    private sealed class GeminiGenerationConfig
    {
        public double Temperature { get; set; }
        public int MaxOutputTokens { get; set; }
    }

    private sealed class GeminiResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
    }
}