# HDR Priority Viewer
HDR Priority Viewer is a visual helper tool for Wwise projects that allows you to visualize your HDR setup on an interactive graph.
It provides a clear overview of your project and lets you open elements directly in Wwise for editing, ensuring consistency across your project.

> [!IMPORTANT]
> The gif and screenshots doesn't reflect a normal use of the tool since I'm using using the Wwise Limbo sample project **which is not an HDR project** with the HDR option enabled on of the main bus.💥  
> I just needed a large session to work with and be able to see the limits of the tool and improve it.

<br>

## 🛠️ Setup & Connection
You first need to open your Wwise project and make sure that Waapi is enabled.  
Then make sure the connection settings of your Wwise project and the tool matches and click on "Connect and Analyze Wwise project". 
<p align="left"><img src="https://github.com/user-attachments/assets/99a30778-7079-47a6-b263-88e6b1fba398" width="500" /></p>

<br>

## ⌨️ Chart controls
### Search & Filters:  
You can **hide or highlight specific elements** using the left pannel filters & search bars.
<p align="left"><img src="https://github.com/user-attachments/assets/1a5c63ce-2ca7-4797-a83b-50bf3effc8f3" width="500" /></p>

> [!TIP]
> Depending on the Wwise project complexity, the graph may contain many elements which could cause performance issues. Using filters or search bars is recommended to hide unecessary elements.  
> It's recommanded to apply the default filter (first one in the list) if this popup shows-up after the analyze.
> <p align="left">
> <img width="400" alt="HDR Priority Viewer Screenshot" src="https://github.com/user-attachments/assets/443fb10e-b3ab-4be9-81c7-ced9d38d72bd" />
</p>

### Zoom & Pan:  
Using the right mouse button you can select an area of the chart to zoom-in, or use the scroll-wheel to zoom / de-zoom.
<p align="left"><img src="https://github.com/user-attachments/assets/810f1185-3afe-4731-a129-9b9aee254a46" width="500" /></p>

### Chart points:  
Hover a point to show its priority properties.  
Ctrl + Left Click to open an audio object in Wwise directly from the graph.
<p align="left"><img src="https://github.com/user-attachments/assets/43d9249b-026a-4188-bb1c-b3ba8659fc7e" width="500" /></p>

> [!IMPORTANT]
> Each time you modify your Wwise project, you need to save it and re-analyse it (Left pannel -> "Re-analyze" button) to update the chart data.

<br>

## 📈 Chart Display  
### Display logic:  
The logic to display an element is following:
   - If the target audio object of the event has children with specific volume setups, they will be displayed instead of the parent. (Recursive logic).
   - Otherwise, the event target audio object itself is displayed.

### Priority value:  
Audio elements are positionned on the Y axis based on there **“fixed” volume setup** (priority), which combines:
   - The audio object and its parents voice volume
   - The audio object's bus and parents bus voice volume

### Priority Range:
Then, the minimum and maximum priority is displayed with a vertical lign which combines:  
   - Voice volumes random ranges
   - States
   - RTPCs min/max

> [!IMPORTANT]
> The min and max value is a theorical value, since the state may never been used, or some RTPCs may cancel each others depending on how the game code / Wwise project works.
> It's just a here to show how much a priority can change and it's better to use Wwise's profiler for live data.
> (And I'm not even sure if a real HDR project will use

<br>

## 🔎 Project Analyze

### WAAPI Data Retrieval  
Connects to your Wwise session to get basic project data: busses, events, and their target audio objects rooted to an HDR bus, along with `.wwu` file locations.

### Project Scanning via `.wwu` files  
Retrieves HDR priority data, including:
   - Voice volume
   - Voice volume random ranges
   - Voice volume affected by States
   - RTPCs on both audio objects and their busses

<br>

## ❓Is this tool safe to use
- As the tool is "read only", the short answer is yes. It **does not modify `.wwu` files** or send any WAAPI commands that write to your project.
- **BUT**, the tool is still in pre-release version, so always save your work before using it, as large graphs may freeze the tool, and maybe your system (less likely but we never know).

<br>

## ⬇️ Download
Go to [Releases page](https://github.com/GazarianGorik/HDRPriorityViewer/releases) => Assets => Download and un-zip *HDRPriorityViewer.zip* => Run *HDRPriorityViewer.exe*

<br>

## 🪲 Known issues
- **Zooming with Ctrl or Alt is a bit buggy.** The zoom axis updates with a slight delay due to the way LiveCharts2 handles input. For now, the most reliable way to zoom in is by using right-click selection.
- If you un-maximaze the tool window and open an audio object in Wwise with a left click on the graph point, Wwise window will resize to the tool's window size. This looks like a bug on AudioKinetic's side  , I’ll investigate further.

<br>

## 🤝 License
Allowed
- Use the **compiled tool** as an internal tool to develop commercial projects.
- Use, copy, and modify the **code** for personal or internal purposes.

Not Allowed
- Sell, distribute, or commercialize the **code**.
- Sell the **compiled tool** or redistribute it commercially for profit.

<br>
This project incorporates third-party code:

1. LiveCharts2
   Copyright (c) 2021
   <a href="https://github.com/beto-rodriguez/LiveCharts2">LiveCharts</a>
   Contributors
   Licensed under the 
   <a href="https://opensource.org/license/mit">MIT License</a>

3. Audiokinetic Wwise SDK (WaapiClientCore & WaapiClientJson)
   Copyright (c) 2025 Audiokinetic Inc.
   Licensed under the 
   <a href="http://www.apache.org/licenses/LICENSE-2.0">Apache License, Version 2.0</a>

##

<sub>
   <p  align="center">
      <br>
         Copyright © 2025 Gorik Gazarian - 
         This project is licensed under the 
         <a href="https://polyformproject.org/licenses/internal-use/1.0.0/">PolyForm Internal Use License 1.0.0</a> - 
         For full details, see the 
         <a href="./LICENSE">LICENSE</a> file in this repository
      <br>
   </p>
</sub>
