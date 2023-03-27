#define AD_HOC

using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Security;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.Tasks.Offline;
using Esri.ArcGISRuntime.UI;
using Esri.ArcGISRuntime.UtilityNetworks;

namespace OfflineMapWithUN;

public partial class MainPage : ContentPage
{
    private Uri _portalUri = new Uri("<Your Portal>");
    private Uri _portalItemUri = new Uri("<Your Webmap Item>");
    private bool _hasInitializedCredentials = false;

    private object _generateJob;
    private Esri.ArcGISRuntime.Mapping.Map _onlineMap;
    private Esri.ArcGISRuntime.Mapping.Map _offlineMap;
    private GraphicsOverlay _associationOverlay = new GraphicsOverlay();

    private String _downloadPath;
    private bool _associationsVisible = false;

    public MainPage()
    {
        // Make sure we have a clean location to download offline data to
        _downloadPath = Path.Combine(FileSystem.Current.CacheDirectory, "offline_map");
        if (Directory.Exists(_downloadPath))
        {
            Directory.Delete(_downloadPath, true);
        }

        InitializeComponent();

        // Provide a default map view
        MapView.Map = new Esri.ArcGISRuntime.Mapping.Map(new Basemap(BasemapStyle.ArcGISLightGray));

        // Add our GraphicOverlays
        MapView.GraphicsOverlays.Add(_associationOverlay);
    }

    private async void LoadOnlineMap(object sender, EventArgs e)
    {
        LoadingIndicator.IsVisible = true;

        if (!_hasInitializedCredentials)
        {
            // TODO: Supply our credentials for loading the Webmap and backing services

            _hasInitializedCredentials = true;
        }

        try
        {
            // Load the online map we will be taking offline
            _onlineMap = new Esri.ArcGISRuntime.Mapping.Map(_portalItemUri);
            await _onlineMap.LoadAsync();

            // Update UI
            MapView.Map = _onlineMap;
            LoadingIndicator.IsVisible = false;
            LoadOnlineMapButton.IsVisible = false;
            TakeOfflineGroup.IsVisible = true;
        }
        catch (Exception)
        {
            ShowError("Error loading online map");
        }
    }

    private async void TakeMapOffline(object sender, EventArgs e)
    {
        // Show loading indicator
        CancelButton.IsVisible = true;
        LoadingIndicatorLabel.Text = "Taking the map offline...";
        LoadingIndicator.IsVisible = true;

#if AD_HOC
        #region Ad-Hoc Offline Map
        var offlineMapTask = await OfflineMapTask.CreateAsync(_onlineMap);
        var defaultParameters = await offlineMapTask.CreateDefaultGenerateOfflineMapParametersAsync(MapView.VisibleArea);

        var generateJob = offlineMapTask.GenerateOfflineMap(defaultParameters, _downloadPath);
        generateJob.ProgressChanged += GenerateJob_ProgressChanged;
        generateJob.Start();

        _generateJob = generateJob;
        #endregion
#else
        #region Offline Map Areas
        var offlineMapTask = await OfflineMapTask.CreateAsync(_onlineMap);
        var mapAreas = await offlineMapTask.GetPreplannedMapAreasAsync();
        var mapArea = mapAreas.FirstOrDefault();

        if (mapArea is null)
        {
            ShowError("No map areas were found for the online map");
            return;
        }

        var defaultParameters = await offlineMapTask.CreateDefaultDownloadPreplannedOfflineMapParametersAsync(mapArea);

        var generateJob = offlineMapTask.DownloadPreplannedOfflineMap(defaultParameters, _downloadPath);
        generateJob.ProgressChanged += GenerateJob_ProgressChanged;
        generateJob.Start();

        _generateJob = generateJob;
        #endregion
#endif

        try
        {
            var result = await generateJob.GetResultAsync();
            _offlineMap = result.OfflineMap;

            // Load the offline map
            LoadingIndicatorLabel.Text = "Loading offline map...";
            await _offlineMap.LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Error taking map offline: {ex.Message}.\nPlease try again");
            return;
        }

        // Display the offline version of the map in the MapView in
        // place of the original online version
        MapView.Map = _offlineMap;

        try
        {
            // Load the utility network from the offline map
            LoadingIndicatorLabel.Text = "Loading utility network...";

            var utilityNetwork = _offlineMap.UtilityNetworks.FirstOrDefault();
            await utilityNetwork.LoadAsync();

            // Update UI and hide loading screen
            UseOfflineGroup.IsVisible = true;
            BadgeLabel.Text = _offlineMap.Bookmarks.Count.ToString();
            TakeOfflineButton.IsVisible = false;
            LoadingIndicator.IsVisible = false;
        }
        catch (Exception ex)
        {
            ShowError($"Error loading offline map: {ex.Message}.\nPlease try again");
        }
    }

    private void ViewWorkOrder(object sender, EventArgs e)
    {
        // Get a random Bookmark and zoom to it, pretending it's the location
        // of a work order
        var workOrder = _offlineMap.Bookmarks[Random.Shared.Next() % _offlineMap.Bookmarks.Count];
        MapView.SetViewpointAsync(workOrder.Viewpoint);

        // Reset the UI
        ViewAssociationsButton.Text = "View Associations";
        _associationOverlay.Graphics.Clear();
        _associationsVisible = false;
    }

    private async void ViewAssociations(object sender, EventArgs e)
    {
        // Only show associations in the offline map
        if (_offlineMap == null)
        {
            return;
        }

        // Remove any existing association graphics
        _associationOverlay.Graphics.Clear();

        if (!_associationsVisible)
        {
            var utilityNetwork = _offlineMap.UtilityNetworks.FirstOrDefault();

            // Query associations within the visible extent.  Querying the
            // associations spatially will return geometry that can be displayed
            var associations = await utilityNetwork.GetAssociationsAsync(MapView.VisibleArea.Extent);
            foreach (var association in associations)
            {
                // Add graphics for each association geometry
                if (association.AssociationType == UtilityAssociationType.Connectivity)
                {
                    _associationOverlay.Graphics.Add(new Graphic(
                        association.Geometry,
                        new SimpleLineSymbol(
                            SimpleLineSymbolStyle.Dash,
                            System.Drawing.Color.Blue,
                            2.0)));
                }
                else if (association.AssociationType == UtilityAssociationType.Attachment)
                {
                    _associationOverlay.Graphics.Add(new Graphic(
                        association.Geometry,
                        new SimpleLineSymbol(
                            SimpleLineSymbolStyle.Dash,
                            System.Drawing.Color.Green,
                            2.0)));
                }
            }

            ViewAssociationsButton.Text = "Hide Associations";
            _associationsVisible = true;
        }
        else
        {
            ViewAssociationsButton.Text = "View Associations";
            _associationsVisible = false;
        }
    }

    #region UI Events and Helpers
    private void GenerateJob_ProgressChanged(object sender, EventArgs e)
    {
        if (sender is GenerateOfflineMapJob adHocJob)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicatorProgressBar.Progress = ((float)adHocJob.Progress / 100d);
            });
        }
        else if (sender is DownloadPreplannedOfflineMapJob mapAreaJob)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicatorProgressBar.Progress = ((float)mapAreaJob.Progress / 100d);
            });
        }
    }

    private void Cancel(object sender, EventArgs e)
    {
        if (_generateJob is GenerateOfflineMapJob adHocJob)
        {
            adHocJob.CancelAsync();
        }
        else if (_generateJob is DownloadPreplannedOfflineMapJob mapAreaJob)
        {
            mapAreaJob.CancelAsync();
        }
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            CancelButton.IsVisible = false;
            LoadingIndicatorLabel.Text = message;

            await Task.Delay(3000);
            LoadingIndicator.IsVisible = false;
        });
    }

    protected override void LayoutChildren(double x, double y, double width, double height)
    {
        base.LayoutChildren(x, y, width, height);
        MapView.ViewInsets = new Thickness(Panel.Width + 2, 0, 0, 0);
    }
    #endregion
}
