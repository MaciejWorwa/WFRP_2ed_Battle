# WFRP_2ed_Battle

A simulator based on the mechanics of the role-playing game Warhammer Fantasy Role Play 2 ed., used to automate larger battles, making them easier to play.

# Setup

You can download the game from the Releases section. Both Windows and Linux versions are available.
Execute the StandaloneWindows64.exe or StandaloneLinux64.exe executable files, located in the downloaded .zip file, and enjoy!

# Environment

The game was developed in Unity Engine. List of supported OS:
- [x] Windows
- [x] Linux

# Language Pack

At the moment only Polish language is supported.

# Features

- Battlefield editor with 34 unique map elements,
- 20 different races/monsters to choose,
- 20 different weapons (with quality to choose),
- 16 different spells,
- 6 different game modes (including automatic combat mode),
- Save and load system,
- And more...

# Tutorial

## Map Editor
When you start the game, you enter the **Map Editor**. Here, you can customize the battlefield using the following features:

- **Grid Size**: Adjust the grid size to fit your gameplay needs.
- **Background Settings**: Load a custom background and modify its size and position to align with your grid.
- **Map Elements**: Place various elements on the map and adjust their rotation:
  - **Element Rotation**: Right-click on an element to rotate it by 90 degrees. You can also set random rotation. When enabled, each placed element will have a random rotation.
  - **Element Types**:
    - **High Obstacle**: Fully covers units behind it.
    - **Low Obstacle**: Partially covers units behind it.
  - **Blocking Tiles**: Set elements to block the tile they occupy. The last item in the element list is specifically designed for blocking; check the box to enable this, making it invisible in Battle Mode.
- When your battlefield setup is complete, click **Play** in the top right corner to start the battle.

## Camera Controls
- **Panning**: Hold the middle mouse button and drag to pan the camera.
- **Zooming**: Use the scroll wheel to zoom in and out for a closer or broader view.

## Starting Battle Mode
- **Click "Play"**: When your battlefield setup is complete, click **Play** in the top right corner to start the battle.
- **Game Modes and Settings**: Press **Esc** to open the main menu, where you can access **Settings**. Here, you can adjust the game modes; by default, recommended settings are enabled.

## Battle Mode

In Battle Mode, manage and control units with these actions:

- **Adding Units**: Open the **Unit Management Panel** to add new units to the battlefield.
- **Selecting Units**: Left-click a unit to select it.
- **Moving Units**: Click on an empty tile to move the selected unit.
- **Attacking**: Right-click on an enemy unit to initiate an attack.

## Game Modes and Settings
Press **Esc** to open the main menu, where you can access **Settings**. By default, recommended settings are enabled.

1. **Automatic Parrying**: Units automatically decide to parry or dodge attacks. When disabled, players choose to block, dodge, or take the damage.
2. **Automatic Death**: Units with health below zero are automatically removed.
3. **Automatic Unit Selection**: Units are selected in initiative order.
4. **Friendly Fire**: Enables attacking allied units.
5. **Automatic Dice Rolls**: Disable to allow players to use physical dice; manual outcomes can then be entered.
6. **Automatic Combat**: Actions for all units are automated, preventing manual movement when enabled.
