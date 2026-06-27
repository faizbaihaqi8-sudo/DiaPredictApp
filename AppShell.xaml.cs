using DiaPredictApp.Pages;

namespace DiaPredictApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(FaceRegisterPage), typeof(FaceRegisterPage));
        Routing.RegisterRoute(nameof(FaceVerificationPage), typeof(FaceVerificationPage));
        Routing.RegisterRoute(nameof(PatientDashboardPage), typeof(PatientDashboardPage));
        Routing.RegisterRoute(nameof(NakesDashboardPage), typeof(NakesDashboardPage));
    }
}