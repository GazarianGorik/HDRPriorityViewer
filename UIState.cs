/****************************************************************************** 
 Copyright (c) 2025 Gorik Gazarian
 
 This file is part of WwiseHDRTool.
 
 Licensed under the PolyForm Noncommercial License 1.0.0.

 You may not use this file except in compliance with the License.
 You may obtain a copy of the License at
 https://polyformproject.org/licenses/noncommercial/1.0.0
 and in the LICENSE file in this repository.
 
 Unless required by applicable law or agreed to in writing,
 software distributed under the License is distributed on
 an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 either express or implied. See the License for the specific
 language governing permissions and limitations under the License.
******************************************************************************/

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