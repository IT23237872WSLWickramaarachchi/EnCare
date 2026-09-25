# 🌿 EnCare VR

[![Unity](https://img.shields.io/badge/Unity-6000.3.11f1%20(Unity%206)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP%2017.3.0-lightgrey.svg)](https://unity.com/srp/Universal-Render-Pipeline)
[![XR Stack](https://img.shields.io/badge/XR-OpenXR%20%7C%20XRI%203.3.1-brightgreen.svg)](https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/index.html)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**EnCare VR** is an immersive Virtual Reality environmental cleaning and waste management simulation developed with **Unity 6** and the **XR Interaction Toolkit (XRI 3.3)**. Designed for modern VR platforms via **OpenXR**, players step into interactive environments to locate, grab, and sort scattered litter and electronic waste (e-waste) into designated collection containers against a countdown timer.

---

## 🎮 Key Features

- **Physically-Based VR Interactions**:
  - Full grab, toss, and manipulation of environmental items using `XRGrabInteractable` and Unity's Input System.
  - Custom outline glow and visual cues on interactable waste objects.
- **Dynamic Cleanup Missions (`CleanupMission`)**:
  - Timed cleanup objectives with mission phases: `Setup` ➔ `Ready` ➔ `Running` ➔ `Completed` / `Failed`.
  - Configurable collection target count and time limits.
- **Garbage Collection Zone (`GarbageCollector`)**:
  - Trigger volume with spatial bounds verification (`OverlapBoxNonAlloc`) preventing false deposits.
  - Layer-masked filtering, audio cues upon deposit, and auto-cleanup.
- **Randomized Trash Spawning (`RandomTrashSpawner`)**:
  - Procedural placement across configurable spawn anchors, ensuring high replayability.
- **In-Game VR HUD (`CleanupHUD`)**:
  - Floating VR user interface displaying real-time target progress, collected counter, active status text, and remaining time.
- **Automated Editor Tooling (`CleanupSceneBuilder`)**:
  - Includes a one-click scene generator (`EnCare > ⚙ Build Cleanup Scene`) to assemble the environment, rigs, interactables, and managers instantly.

---

## 🛠️ Technology Stack & Dependencies

| Component | Specification |
| :--- | :--- |
| **Engine** | Unity `6000.3.11f1` (Unity 6) |
| **Render Pipeline** | Universal Render Pipeline (URP `17.3.0`) |
| **VR Runtime** | OpenXR Plugin (`1.16.1`) with AndroidXR support |
| **Interaction Framework** | XR Interaction Toolkit (`3.3.1`) & XR Hands (`1.7.3`) |
| **Input System** | Unity Input System (`1.19.0`) |
| **UI** | TextMeshPro (`com.unity.modules.ui`) |

---

## 📂 Project Structure

```text
EnCare VR/
├── Assets/
│   ├── EnCare/
│   │   ├── Editor/            # Editor scripts (FixTMPFontAsset, CleanupSceneBuilder)
│   │   ├── Models/            # 3D assets (Bins, collectors, environment props)
│   │   ├── Prefabs/           # Prefabs for interactables, HUD, and containers
│   │   ├── Materials/         # URP materials and outline shaders
│   │   └── Scripts/           # Core gameplay logic
│   │       ├── CleanupMission.cs       # Mission state & timer management
│   │       ├── CleanupHUD.cs           # VR World-space UI / HUD
│   │       ├── GarbageCollector.cs     # Deposit volume detection & validation
│   │       ├── TrashItem.cs            # Interactable waste component
│   │       ├── RandomTrashSpawner.cs   # Procedural item spawn manager
│   │       ├── TrashGlow.cs            # Visual hover & grab effects
│   │       └── HandoverCutscene.cs     # Mission transition sequences
│   ├── Scenes/
│   │   ├── Lvl_Backyard.unity          # Backyard cleanup level
│   │   ├── CleanupScene.unity          # Procedurally built office cleanup scene
│   │   └── BasicScene.unity            # Testing & prototyping sandbox
│   └── Samples/                       # XR Interaction Toolkit starter assets & rigs
├── Packages/                          # Package manager manifests
└── ProjectSettings/                   # Engine & OpenXR configurations
```

---

## 🚀 Getting Started

### Prerequisites

1. **Unity Hub** installed.
2. **Unity Editor `6000.3.11f1`** (or newer Unity 6 LTS) with:
   - **Android Build Support** (if building for Meta Quest or standalone headsets).
   - **Windows / macOS Build Support** (for PCVR / SteamVR testing).
3. A supported VR Headset (Meta Quest 2/3/Pro, Valve Index, HTC Vive, or OpenXR-compatible headset) or Unity XR Device Simulator.

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/IT23237872WSLWickramaarachchi/EnCare.git
   cd EnCare
   ```

2. **Open the project in Unity:**
   - Launch **Unity Hub**.
   - Click **Add** ➔ **Add project from disk**.
   - Select the `EnCare VR` root directory.
   - Wait for Unity to import assets and compile packages.

3. **Verify OpenXR Settings:**
   - Go to `Edit > Project Settings > XR Plug-in Management`.
   - Ensure **OpenXR** is enabled under your target platform tab (Standalone or Android).
   - Confirm your controllers / interaction profiles (e.g. Meta Quest Touch Controller Profile) are enabled under the OpenXR features.

---

## 🕹️ How to Play

1. Open any game scene (e.g., `Assets/Scenes/Lvl_Backyard.unity` or `Assets/Scenes/CleanupScene.unity`).
2. Connect your VR headset (or use the XR Device Simulator) and enter **Play Mode** in the Unity Editor.
3. **Controls:**
   - **Grip / Trigger:** Pick up pieces of trash scattered across the environment.
   - **Locomotion:** Use thumbsticks for continuous movement or teleportation (depending on the active rig setup).
   - **Deposit:** Bring collected trash items to the designated collection bin / basket. Releasing the item inside will register it on the HUD counter.
   - **Goal:** Clear the target number of items before the mission timer reaches zero!

---

## ⚙️ Editor Utilities

### One-Click Cleanup Scene Generator
To quickly scaffold a complete testing scene with an XR Origin, office furniture, spawn points, trash prefabs, and the mission manager:
1. In the Unity menu, click **EnCare** ➔ **⚙ Build Cleanup Scene**.
2. Confirm the prompt to build `Assets/Scenes/CleanupScene.unity`.

---

## 👥 Contributors

- **IT23237872WSLWickramaarachchi** - Lead Developer & Maintainer

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) (or applicable institution license). See the repository settings for further details.
