namespace AnimeReserve.App;
public partial class App:Application {
 public App(){InitializeComponent();UserAppTheme=AppTheme.Light;}
 protected override Window CreateWindow(IActivationState? state)=>new(new NavigationPage(new MainPage())){Title="Anime Reserve Flow",Width=1200,Height=860,MinimumWidth=850,MinimumHeight=650};
}
