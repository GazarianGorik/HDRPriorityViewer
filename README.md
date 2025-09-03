WIP

# HDR Priority Graph
HDR Priority Graph is a visual helper tool for Wwise projects that allows you to visualize your HDR setup on an interactive graph.
It provides a clear overview of your project and lets you open elements directly in Wwise for editing, ensuring consistency across your project.

<br>

## ⌨️ Features & Commands

**Zoom and pan through your HDR setup**:
  - *Right mouse button selection or Scrollwheel + optionnal Ctrl or Alt keys for Vertical / Horizontal zoom)*
    
**Quickly hide or highlight specific elements**:
  - *Left pannel filters & search bars*
    
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

<br>

## ❓ Safety

- Thechnically it is ! As the tool is "read only". It **does not modify `.wwu` files** or send any WAAPI commands that write to your project.
- **BUT**, the tool is still in pre-release version, so always save your work before using it, as large graphs may freeze the tool, and less likely your system.
- A performance warning popup is shown when too many elements are about to be displayed, just apply the default filter, we never now!

<br>

<img width="537" height="269" alt="HDR Priority Graph Screenshot" src="https://github.com/user-attachments/assets/443fb10e-b3ab-4be9-81c7-ced9d38d72bd" />

> Depending on project complexity, the graph may contain many elements. Using filters or search bars is recommended.  
> **Tip:** Apply the default filter for large projects, especially on low-spec PCs.

<br>

## 🪲 Known issues
- Ctrl or Alt zoom are a bit buggy (zoom axis is updated a bit too late, comes from how LiveCharts2 handle it, I was not able to fix this issue. For now right clic selection is the best option to zoom-in).
- If you un-maximaze the tool window and open an audio object in Wwise with a left click on the graph point, Wwise window will resize to the tool window size, seems to be an AudioKinetic's side bug.

<br>

## 🛟 Troubleshoot

<br>

## 🤝 License
This project is licensed under the [PolyForm Internal Use License 1.0.0](https://polyformproject.org/licenses/internal-use/1.0.0/).
Copyright (c) 2025 Gorik Gazarian

#### ✅ Allowed
- Use the **compiled tool** as an internal tool to develop commercial projects.
- Use, copy, and modify the **code** for personal or internal purposes.

#### ❌ Not Allowed
- Sell, distribute, or commercialize the **code**.
- Sell the **compiled tool** or redistribute it commercially for profit.

<br>

For full details, see the [LICENSE](./LICENSE) file in this repository.
