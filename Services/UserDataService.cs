using System.Text.Json;
using DiaPredictApp.Models;
using Microsoft.Maui.Storage;
using SQLite;

namespace DiaPredictApp.Services;

public static class UserDataService
{
    private const string DatabaseFileName = "diapredict.db3";
    private const string OldJsonFileName = "users.json";

    private static SQLiteConnection? _database;
    private static readonly object DatabaseLock = new();
    private static bool _migrationChecked;

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    private static string OldJsonPath =>
        Path.Combine(FileSystem.AppDataDirectory, OldJsonFileName);

    private static SQLiteConnection GetDatabase()
    {
        lock (DatabaseLock)
        {
            if (_database != null)
            {
                return _database;
            }

            _database = new SQLiteConnection(DatabasePath);
            _database.CreateTable<UserAccount>();

            MigrateOldJsonToSQLiteIfNeeded();

            return _database;
        }
    }

    private static void MigrateOldJsonToSQLiteIfNeeded()
    {
        if (_migrationChecked)
        {
            return;
        }

        _migrationChecked = true;

        if (_database == null)
        {
            return;
        }

        try
        {
            int existingUserCount = _database.Table<UserAccount>().Count();

            if (existingUserCount > 0)
            {
                return;
            }

            if (!File.Exists(OldJsonPath))
            {
                return;
            }

            string json = File.ReadAllText(OldJsonPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            List<UserAccount>? oldUsers = JsonSerializer.Deserialize<List<UserAccount>>(json);

            if (oldUsers == null || oldUsers.Count == 0)
            {
                return;
            }

            foreach (UserAccount user in oldUsers)
            {
                user.Id = 0;

                if (string.IsNullOrWhiteSpace(user.LastRiskResult))
                {
                    user.LastRiskResult = "Belum diprediksi";
                }

                if (string.IsNullOrWhiteSpace(user.LastRiskExplanation))
                {
                    user.LastRiskExplanation = "Belum ada penjelasan.";
                }

                if (string.IsNullOrWhiteSpace(user.LastClinicalSummary))
                {
                    user.LastClinicalSummary = "Belum ada data.";
                }

                if (string.IsNullOrWhiteSpace(user.LastPredictionDateText))
                {
                    user.LastPredictionDateText = "-";
                }

                _database.Insert(user);
            }
        }
        catch
        {
            // Jika migrasi JSON gagal, aplikasi tetap lanjut memakai SQLite kosong.
            // User masih bisa registrasi ulang.
        }
    }

    public static List<UserAccount> LoadUsers()
    {
        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .OrderBy(user => user.FullName)
                .ToList();
        }
    }

    public static void SaveUsers(List<UserAccount> users)
    {
        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            db.DeleteAll<UserAccount>();

            foreach (UserAccount user in users)
            {
                user.Id = 0;
                db.Insert(user);
            }
        }
    }

    public static void SaveUsers()
    {
        // Method ini sengaja dipertahankan agar kode lama yang memanggil SaveUsers()
        // tidak error. Pada SQLite, perubahan langsung disimpan saat Insert/Update/Delete.
    }

    public static bool UsernameExists(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        string normalizedUsername = username.Trim().ToLower();

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .ToList()
                .Any(user => user.Username.Trim().ToLower() == normalizedUsername);
        }
    }

    public static UserAccount? FindByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        string normalizedUsername = username.Trim().ToLower();

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .ToList()
                .FirstOrDefault(user => user.Username.Trim().ToLower() == normalizedUsername);
        }
    }

    public static UserAccount? Authenticate(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        string normalizedUsername = username.Trim().ToLower();
        string normalizedPassword = password.Trim();
        string normalizedRole = role.Trim().ToLower();

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .ToList()
                .FirstOrDefault(user =>
                    user.Username.Trim().ToLower() == normalizedUsername &&
                    user.Password == normalizedPassword &&
                    user.Role.Trim().ToLower() == normalizedRole);
        }
    }

    public static bool RegisterUser(UserAccount user)
    {
        if (user == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.FullName) ||
            string.IsNullOrWhiteSpace(user.Username) ||
            string.IsNullOrWhiteSpace(user.Password) ||
            string.IsNullOrWhiteSpace(user.Role))
        {
            return false;
        }

        if (UsernameExists(user.Username))
        {
            return false;
        }

        user.Id = 0;
        user.FullName = user.FullName.Trim();
        user.Username = user.Username.Trim();
        user.Role = user.Role.Trim();

        if (string.IsNullOrWhiteSpace(user.LastRiskResult))
        {
            user.LastRiskResult = "Belum diprediksi";
        }

        if (string.IsNullOrWhiteSpace(user.LastRiskExplanation))
        {
            user.LastRiskExplanation = "Belum ada penjelasan.";
        }

        if (string.IsNullOrWhiteSpace(user.LastClinicalSummary))
        {
            user.LastClinicalSummary = "Belum ada data.";
        }

        if (string.IsNullOrWhiteSpace(user.LastPredictionDateText))
        {
            user.LastPredictionDateText = "-";
        }

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.Insert(user);
        }

        return true;
    }

    public static List<UserAccount> GetPatients()
    {
        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .ToList()
                .Where(user => user.Role.Trim().Equals("Pasien", StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.FullName)
                .ToList();
        }
    }

    public static List<UserAccount> GetNakes()
    {
        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            return db
                .Table<UserAccount>()
                .ToList()
                .Where(user => user.Role.Trim().Equals("Nakes", StringComparison.OrdinalIgnoreCase))
                .OrderBy(user => user.FullName)
                .ToList();
        }
    }

    public static void UpdateUser(UserAccount updatedUser)
    {
        if (updatedUser == null)
        {
            return;
        }

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();

            UserAccount? existingUser = db
                .Table<UserAccount>()
                .ToList()
                .FirstOrDefault(user => user.Username.Trim().Equals(updatedUser.Username.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existingUser == null)
            {
                return;
            }

            updatedUser.Id = existingUser.Id;
            db.Update(updatedUser);
        }
    }

    public static void UpdateFaceData(
        string username,
        string rawFaceImagePath,
        string normalizedFaceImagePath)
    {
        UserAccount? user = FindByUsername(username);

        if (user == null)
        {
            return;
        }

        user.FaceRegistered = true;
        user.RawFaceImagePath = rawFaceImagePath;
        user.NormalizedFaceImagePath = normalizedFaceImagePath;

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.Update(user);
        }
    }

    public static void UpdateRiskPrediction(
        string username,
        string riskResult,
        int riskScore,
        string riskExplanation,
        string clinicalSummary,
        string predictionDateText)
    {
        UserAccount? user = FindByUsername(username);

        if (user == null)
        {
            return;
        }

        user.LastRiskResult = riskResult;
        user.LastRiskScore = riskScore;
        user.LastRiskExplanation = riskExplanation;
        user.LastClinicalSummary = clinicalSummary;
        user.LastPredictionDateText = predictionDateText;

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.Update(user);
        }
    }

    public static void UpdatePatientDiabetesPrediction(
        string username,
        int age,
        double fastingGlucose,
        double hba1c,
        double bmi,
        int systolicBloodPressure,
        int diastolicBloodPressure,
        int physicalActivityPerWeek,
        bool hasFamilyHistory,
        bool hasClassicSymptoms,
        string riskResult,
        int riskScore,
        string riskExplanation,
        string clinicalSummary,
        string predictionDateText)
    {
        UserAccount? user = FindByUsername(username);

        if (user == null)
        {
            return;
        }

        user.Age = age;
        user.FastingGlucose = fastingGlucose;
        user.Hba1c = hba1c;
        user.Bmi = bmi;
        user.SystolicBloodPressure = systolicBloodPressure;
        user.DiastolicBloodPressure = diastolicBloodPressure;
        user.PhysicalActivityPerWeek = physicalActivityPerWeek;
        user.HasFamilyHistory = hasFamilyHistory;
        user.HasClassicSymptoms = hasClassicSymptoms;

        user.LastRiskResult = riskResult;
        user.LastRiskScore = riskScore;
        user.LastRiskExplanation = riskExplanation;
        user.LastClinicalSummary = clinicalSummary;
        user.LastPredictionDateText = predictionDateText;

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.Update(user);
        }
    }

    public static void DeleteUser(string username)
    {
        UserAccount? user = FindByUsername(username);

        if (user == null)
        {
            return;
        }

        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.Delete(user);
        }
    }

    public static void ClearAllUsers()
    {
        lock (DatabaseLock)
        {
            SQLiteConnection db = GetDatabase();
            db.DeleteAll<UserAccount>();
        }
    }
}