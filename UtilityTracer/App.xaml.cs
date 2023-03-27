using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Mapping;

namespace UtilityTracer;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new OutagePage();
    }
}

