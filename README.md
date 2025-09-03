# HDR Priority Viewer
HDR Priority Viewer is a visual helper tool for Wwise projects that allows you to visualize your HDR setup on an interactive graph.
It provides a clear overview of your project and lets you open elements directly in Wwise for editing, ensuring consistency across your project.



<br>

## ⌨️ How to use

**WAAPI Connection & Project Analyze**
You first need to open your Wwise project and enable WAAPI connection if not already done. Then you may need to change the settings connection according to your project WAAPI setup. 
<p align="left"><img src="https://github.com/user-attachments/assets/fbdcf96e-8ed9-48bd-91a8-f13931f734cf" width="800" /></p>

**You can then hide or highlight specific elements**:
<p align="left"><img src="https://github.com/user-attachments/assets/1a5c63ce-2ca7-4797-a83b-50bf3effc8f3" width="800" /></p>
  - *Left pannel filters & search bars*

**Zoom and pan through your HDR setup**:
  - *Right mouse button selection or Scrollwheel + optionnal Ctrl or Alt keys for Vertical / Horizontal zoom)*
    
**Open audio objects in Wwise directly from the graph**:
  - *Ctrl + Click on a point*

<br>

## 🔎 How it works

1. **WAAPI Data Retrieval**
   - Connects to your Wwise session to get basic project data: busses, events, and their target audio objects rooted to an HDR bus, along with `.wwu` file locations.

2. **Project Scanning via `.wwu` files**
   - Retrieves HDR priority data, including:
     - Voice volume
     - Voice volume random ranges
     - Voice volume affected by States
     - RTPCs on both audio objects and their busses

3. **Chart Display**
   - Elements are displayed based on the **“fixed” volume setup**, combining:
     - Audio object voice volume
     - Parent object volume
     - Bus volume
     - Parent bus volume

4. **Display Logic**
   - If a target audio object has children with specific volume setups, they will be displayed instead of the parent (event target audio). (Recursive logic).
   - Otherwise, the event target audio object itself is displayed.


> [!TIP]
> Depending on the Wwise project complexity, the graph may contain many elements which could cause performance issues. Using filters or search bars is recommended to hide unecessary elements.  
> It's recommanded to apply the default filter (first one in the list) if this popup shows-up.
> <p align="left">
> <img width="400" alt="HDR Priority Viewer Screenshot" src="https://github.com/user-attachments/assets/443fb10e-b3ab-4be9-81c7-ced9d38d72bd" />
</p>

<br>

## Is this app safe to use❓
- As the tool is "read only", the short answer is yes. It **does not modify `.wwu` files** or send any WAAPI commands that write to your project.
- **BUT**, the tool is still in pre-release version, so always save your work before using it, as large graphs may freeze the tool, and maybe your system (less likely but we never know).

<br>

## 🪲 Known issues
- **Zooming with Ctrl or Alt is a bit buggy.** The zoom axis updates with a slight delay due to the way LiveCharts2 handles input. For now, the most reliable way to zoom in is by using right-click selection.
- If you un-maximaze the tool window and open an audio object in Wwise with a left click on the graph point, Wwise window will resize to the tool's window size. This looks like a bug on AudioKinetic's side  , I’ll investigate further.

<br>

## 🛟 Troubleshoot

<br>

## 🤝 License
✅ Allowed
- Use the **compiled tool** as an internal tool to develop commercial projects.
- Use, copy, and modify the **code** for personal or internal purposes.

❌ Not Allowed
- Sell, distribute, or commercialize the **code**.
- Sell the **compiled tool** or redistribute it commercially for profit.

<br>
Copyright © 2025 Gorik Gazarian.
<br>

This project is licensed under the [PolyForm Internal Use License 1.0.0](https://polyformproject.org/licenses/internal-use/1.0.0/). For full details, see the [LICENSE](./LICENSE) file in this repository.
