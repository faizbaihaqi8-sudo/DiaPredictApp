using DiaPredictApp.Models;
using DiaPredictApp.Services;

namespace DiaPredictApp.Pages;

public partial class NakesDashboardPage : ContentPage
{
    public NakesDashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        string currentUsername = Preferences.Get("CurrentUsername", "");
        var nakes = UserDataService.FindByUsername(currentUsername);

        if (nakes != null)
        {
            WelcomeLabel.Text = $"Halo, {nakes.FullName}. Berikut data pasien dan status risiko diabetes.";
        }
        else
        {
            WelcomeLabel.Text = "Berikut data pasien dan status risiko diabetes.";
        }

        LoadPatientData();
    }

    private void LoadPatientData()
    {
        List<UserAccount> patients = UserDataService.GetPatients();

        TotalPatientsLabel.Text = patients.Count.ToString();

        int highRiskCount = patients.Count(patient => patient.LastRiskResult == "Risiko Tinggi");
        int predictedCount = patients.Count(patient => patient.LastRiskResult != "Belum diprediksi");

        HighRiskLabel.Text = highRiskCount.ToString();
        PredictedPatientsLabel.Text = predictedCount.ToString();

        EmptyPatientsLabel.IsVisible = patients.Count == 0;
        PatientsCollectionView.IsVisible = patients.Count > 0;
        PatientsCollectionView.ItemsSource = patients;
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        Preferences.Remove("CurrentUsername");
        Preferences.Remove("CurrentRole");
        Preferences.Remove("CurrentFullName");

        await Shell.Current.GoToAsync("//MainPage");
    }
}