using Esri.ArcGISRuntime;
using Esri.ArcGISRuntime.Maui;
using Esri.ArcGISRuntime.Security;
using Microsoft.Extensions.Logging;

namespace UtilityTracer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            }).UseArcGISRuntime(config => config
              .UseLicense("<Your License>")
             );

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}