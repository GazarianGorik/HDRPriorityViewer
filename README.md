# HDR Priority Viewer (W10, W11)
HDR Priority Viewer is a visual helper tool for Wwise projects that displays your HDR setup on an interactive graph.  
It provides a clear overview of your project and lets you open elements directly in Wwise for editing, helping you maintain consistency throughout your project.

> [!IMPORTANT]
> The following GIFs and screenshots do not represent typical usage of the tool. They were captured using the Wwise Limbo sample project — which is not an HDR project — with the HDR option enabled on one of the main busses. 💥  
> I chose this project simply because it provided a large session to test with, allowing me to explore the tool’s limits and refine it.

<br>

## Table of Contents
Open and navigate GitHub’s built-in table of contents using the header filters in the top-right corner.

<!--I - [Getting Started](#i---getting-started)  
 A) [Downloads](#a-downloads)  
 B) [Setup & Connection](#b-setup--connection)  
 C) [Chart controls](#c-chart-controls)  
  1) [Search & Filters](#1-search--filters)  
  2) [Zoom & Pan](#2-zoom--pan)  
  3) [Chart points](#3-chart-points)  
 D) [Chart Display](#d-chart-display)  
  1) [Display logic](#1-display-logic)  
  2) [Priority value](#2-priority-value)  
  3) [Priority range](#3-priority-range)  

II - [How it works](#ii---how-it-works)  
 A) [WAAPI Data Retrieval](#a-waapi-data-retrieval)  
 B) [Project Scanning](#b-project-scanning)  

III - [Is it safe to use?](#iii---is-it-safe-to-use)  

IV - [Known issues](#iv---known-issues)  

V - [Credits](#v---credits)  

VI - [License](#vi---license) -->

<br>

## I - Getting Started
### **Downloads**
#### [Pre-Release](https://github.com/GazarianGorik/HDRPriorityViewer/releases/download/v0.1/HDRPriorityViewer.zip)
[![Releases](https://img.shields.io/github/v/release/GazarianGorik/HDRPriorityViewer?include_prereleases&sort=semver)](https://github.com/GazarianGorik/HDRPriorityViewer/releases/download/v0.1/HDRPriorityViewer.zip) [![PayPal](https://img.shields.io/badge/paypal-donate-yellow.svg)](https://www.paypal.com/donate/?hosted_button_id=FPWWD2DV58BF4)  

### **Setup & Connection**
First, open your Wwise project and ensure that WAAPI is enabled.  
Next, verify that the connection settings in your Wwise project match those in the tool, then click "Connect and Analyze Wwise Project."

> [!IMPORTANT]
> Make sure that the Wwise's *user preferences* window is closed before trying to connect the tool.

> [!NOTE]
> More info about WAAPI setup & connection [here](https://www.audiokinetic.com/fr/public-library/2024.1.7_8863/?source=SDK&id=waapi_prepare.html).
<!-- <p align="left"><img src="https://github.com/user-attachments/assets/99a30778-7079-47a6-b263-88e6b1fba398" width="500" /></p> --> 

### **Chart controls**
#### <ins>Search & Filters</ins>  
You can **hide or highlight specific elements** using the left pannel filters & search bars.
<p align="left"><img src="https://github.com/user-attachments/assets/1a5c63ce-2ca7-4797-a83b-50bf3effc8f3" width="500" /></p>

> [!TIP]
> Depending on the Wwise project complexity, the graph may contain many elements which could cause performance issues. Using filters or search bars is recommended to hide unnecessary elements.  
> It's recommended to apply the default filter (first one in the list) if this popup shows-up after the analyze.
> <p align="left">
> <img width="400" alt="HDR Priority Viewer Screenshot" src="https://github.com/user-attachments/assets/443fb10e-b3ab-4be9-81c7-ced9d38d72bd" />
</p>

#### <ins>Zoom & Pan</ins>  
Using the right mouse button you can select an area of the chart to zoom-in, or use the scroll-wheel to zoom / de-zoom.
<p align="left"><img src="https://github.com/user-attachments/assets/810f1185-3afe-4731-a129-9b9aee254a46" width="500" /></p>

#### <ins>Chart points</ins>  
Hover a point to show its priority properties.  
Ctrl + Left Click to open an audio object in Wwise directly from the graph.
<p align="left"><img src="https://github.com/user-attachments/assets/43d9249b-026a-4188-bb1c-b3ba8659fc7e" width="500" /></p>

> [!IMPORTANT]
> Each time you modify your Wwise project, you need to save it and re-analyze it (Left pannel -> "Re-analyze" button) to update the chart data.

### **Chart Display**
#### <ins>Display logic</ins>  
The logic to display an element is following:
   - If the target audio object of the event has children with specific volume setups, they will be displayed instead of the parent. (Recursive logic).
   - Otherwise, the event target audio object itself is displayed.

#### <ins>Priority value</ins>  
Audio elements are positionned on the Y axis based on their **“fixed” volume setup** (priority), which combines:
   - The audio object and its parents voice volume
   - The audio object's bus and parents bus voice volume

#### <ins>Priority range</ins>
Then, the minimum and maximum priority is displayed with a vertical lign which combines:  
   - Voice volumes random ranges
   - States
   - RTPCs min/max

>[!IMPORTANT]
>The displayed min and max values are theoretical, since a given state may never be used, or RTPCs may cancel each other out depending on how the game code or Wwise project is set up.
>These values are only meant to illustrate how much a priority can vary. For accurate, real-time data, use Wwise’s profiler.

<br>

## II - How it works

#### <ins>WAAPI Data Retrieval</ins>  
The tool first onnects to your Wwise session to get basic project data: busses, events, and their target audio objects rooted to an HDR bus, along with `.wwu` file locations.

#### <ins>Project Scanning</ins>  
Then, it scans the `.wwu` files to retrieve HDR priority data, including:
   - Voice volume
   - Voice volume random ranges
   - Voice volume affected by States
   - Voice volume RTPCs
This is done for both audio objects and their busses.

<br>

## III - Is it safe to use?
- As the tool is "read only", the short answer is yes. It **does not modify `.wwu` files** or send any WAAPI commands that write to your project.
- **BUT**, the tool is still in pre-release version, so always save your work before using it, as large graphs may freeze the tool, and maybe your system (less likely but we never know).

<br>

## IV - Known issues
- **Zooming with Ctrl or Alt is a bit buggy.** The zoom axis updates with a slight delay due to the way LiveCharts2 handles input. For now, the most reliable way to zoom in is by using right-click selection.
- If you unmaximize the tool window and open an audio object in Wwise with a left click on the graph point, Wwise window will resize to the tool's window size. This looks like a bug on AudioKinetic's side  , I’ll investigate further.

<br>

## V - Credits
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

<br>

## VI - License
Allowed
- Use the **compiled tool** as an internal tool to develop commercial projects.
- Use, copy, and modify the **code** for personal or internal purposes.

Not Allowed
- Sell, distribute, or commercialize the **code**.
- Sell the **compiled tool** or redistribute it commercially for profit.

<br>

##

<br>

<sub>
   <p  align="center">
      <br>
         Copyright © 2025 Gorik Gazarian - 
         This project is licensed under the 
         <a href="https://polyformproject.org/licenses/internal-use/1.0.0/">PolyForm Internal Use License 1.0.0</a> - 
         For full details, see the 
         <a href="./LICENSE">LICENSE file in this repository</a>
      <br>
   </p>
</sub>
