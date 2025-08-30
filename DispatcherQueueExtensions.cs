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

using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace WwiseHDRTool;
public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this DispatcherQueue dispatcherQueue, Action action)
    {
        var tcs = new TaskCompletionSource();
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("Unable to enqueue on DispatcherQueue."));
        }
        return tcs.Task;
    }
}
