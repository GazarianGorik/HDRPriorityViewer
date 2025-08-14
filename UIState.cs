using CommunityToolkit.Mvvm.ComponentModel;

namespace WwiseHDRTool;

public class UIState : ObservableObject
{
    private bool _isConnectedToWwise;
    public bool IsConnectedToWwise
    {
        get => _isConnectedToWwise;
        set => SetProperty(ref _isConnectedToWwise, value);
    }

    private bool _isChartReady;
    public bool IsChartReady
    {
        get => _isChartReady;
        set => SetProperty(ref _isChartReady, value);
    }

    // Ajoute ici deux autres états globaux si nécessaire
    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }
}