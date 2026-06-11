# 🏔️ The Peak — Don't Climb It

> A turn-based tower defense game where players must stop climbers from reaching the summit of a mountain using a card-based obstacle system. Winner of the **Best Game Award** at the university end-of-year showcase.

![Engine](https://img.shields.io/badge/Engine-Unity-black?logo=unity)
![Language](https://img.shields.io/badge/Language-C%23-purple)
![Genre](https://img.shields.io/badge/Genre-Tower%20Defense-blue)
![Award](https://img.shields.io/badge/🏆-Best%20Game%20Award-gold)
![Status](https://img.shields.io/badge/Status-Completed-brightgreen)

---

## 🎮 Overview

**The Peak — Don't Climb It** is a turn-based tower defense game developed in Unity. Players must prevent a group of climbers from reaching the top of a mountain by strategically placing obstacles through a card-based system inspired by Clash Royale. Each climber has unique behaviors and counters to specific obstacles, creating a layer of strategic depth on top of the core tower defense loop.

The game was developed as a university capstone project and won the **Best Game Award** at the end-of-year showcase.

---

## 👥 Team

Developed by a team of **6**:

| Role | Name |
|------|------|
| Gameplay Programmer | Darío Calderón Tornero |
| Gameplay Programmer | [Teammate] |
| Gameplay Programmer | [Teammate] |
| VFX Artist | [Teammate] |
| 3D Artist | [Teammate] |
| Game Designer | [Teammate] |

---

## 🏆 Awards

- 🥇 **Best Game Award** — University End-of-Year Showcase

---

## 🕹️ Gameplay

- Place obstacle cards on a mountain grid to stop climbers before they reach the summit
- Each turn, climbers advance along dynamically calculated paths
- Different climber types counter specific obstacles — strategy is key
- Cinematic death sequences play when a climber is eliminated
- Mountain difficulty scales as the game progresses through levels

---

## ⚙️ Key Systems

### 💀 Death System
- Full death pipeline: hit detection, death type classification, and outcome resolution
- Dynamic cinematic camera that frames each death moment automatically
- Per-death-type VFX instantiation and animation triggering
- Decoupled architecture allowing independent control of sound, VFX, and animation timing

### 🖼️ Vignette System
- Dynamic HUD showing all active climbers ordered by urgency (proximity to summit)
- Order updates in real time as climbers move
- Interactive: clicking a vignette moves the camera to that climber
- Clicking a climber on the map highlights their vignette
- Visual indicators for counter interactions displayed directly on the vignette

### ⚔️ Counter System
- Each climber type has a counter reaction to specific obstacles
- Counter interactions visually highlighted in the vignette UI
- Some climbers blow air, swing a pickaxe, or perform unique reactions depending on the obstacle

### 🧠 Manager Architecture
- **GameManager** — Global game state and flow control
- **TurnManager** — Turn sequencing, player and climber phases, path recalculation triggered once per turn
- **SpawnManager** — Climber spawning logic tied to mountain level progression

### 🗺️ Grid & Pathfinding
- Tile-based mountain grid for obstacle placement
- Pathfinding system ensuring climbers always find an upward route
- Camp nodes define intermediate waypoints and next route selection
- Path recalculation on turn start to avoid per-climber direction artifacts

### 📷 Cinemachine Camera
- Full camera system built with Cinemachine
- WASD panning across the mountain
- Smooth rotation and zoom controls
- Automatic cinematic framing for death sequences
- Seamless transition between gameplay camera and cinematic camera

### 🔊 Audio System
- All sound effects and music implemented and integrated
- Event-driven audio reactions to gameplay moments (deaths, counters, card placement, UI)
- Centralized audio management

### 📊 Scoring & Progression
- Score system tied to climber eliminations and efficiency
- Mountain level progression increasing difficulty over time
- Visual level indicators on the mountain

---

## 🧠 Technical Highlights

- **Decoupled death callbacks** — `OnAnimationComplete` and `OnReadyForExplosion` separated to allow independent sound/VFX timing control without modifying central managers
- **Shared state dictionaries** — All climbers registered immediately on spawn to prevent stuck states; all freed on `OnDestroy`
- **Path recalculation once per turn** — Triggered in `TurnManager.StartPlayerTurn` rather than per-climber to avoid direction-change artifacts
- **Vignette urgency sorting** — Dynamic reordering based on real-time climber position on the mountain
- **Lambda closure safety** — Local references stored before nulling fields to prevent `MissingReferenceException` in coroutines

---

## 🛠️ Tech Stack

| Category | Technology |
|----------|------------|
| Engine | Unity |
| Language | C# |
| Camera | Cinemachine |
| Data | ScriptableObjects |
| Pathfinding | Custom NavMesh + waypoint system |
| UI | Unity UI + TextMeshPro |
| Audio | Unity Audio System |
| VFX | Unity Particle System + Visual Effect Graph |

---

## 📸 Screenshots

<!-- Add screenshots or GIFs here -->
> *Coming soon*

---

## 📄 License

This project is not open source. All rights reserved.
