using SQLite;

namespace DiaPredictApp.Models;

public class UserAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string FullName { get; set; } = "";

    [Indexed(Unique = true)]
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string Role { get; set; } = "";

    public bool FaceRegistered { get; set; }

    public string RawFaceImagePath { get; set; } = "";

    public string NormalizedFaceImagePath { get; set; } = "";

    public string LastRiskResult { get; set; } = "Belum diprediksi";

    public int LastRiskScore { get; set; }

    public string LastRiskExplanation { get; set; } = "Belum ada penjelasan.";

    public string LastClinicalSummary { get; set; } = "Belum ada data.";

    public string LastPredictionDateText { get; set; } = "-";

    public int Age { get; set; }

    public double FastingGlucose { get; set; }

    public double Hba1c { get; set; }

    public double Bmi { get; set; }

    public int SystolicBloodPressure { get; set; }

    public int DiastolicBloodPressure { get; set; }

    public int PhysicalActivityPerWeek { get; set; }

    public bool HasFamilyHistory { get; set; }

    public bool HasClassicSymptoms { get; set; }
}