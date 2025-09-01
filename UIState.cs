/******************************************************************************
#                                                                             #
#   Copyright (c) 2025 Gorik Gazarian                                         #
#                                                                             #
#   This software is licensed under the PolyForm Internal Use License 1.0.0.  #
#   You may obtain a copy of the License at                                   #
#   https://polyformproject.org/licenses/internal-use/1.0.0                   #
#   and in the LICENSE file in this repository.                               #
#                                                                             #
#   You may use, copy, and modify this software for internal purposes,        #
#   including internal commercial use, but you may not redistribute it        #
#   or sell it without a separate license.                                    #
#                                                                             #
******************************************************************************/

using CommunityToolkit.Mvvm.ComponentModel;

namespace HDRPriorityGraph;

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