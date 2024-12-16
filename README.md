# WFRP_2ed_Battle

A comprehensive simulator based on the mechanics of the role-playing game **Warhammer Fantasy Role Play 2nd Edition**, designed to automate and streamline large-scale battles.
With a focus on ease of use and flexibility, the simulator supports a wide range of customization options, enabling players to recreate diverse battle scenarios while preserving the core mechanics of the tabletop experience.

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
- 20 different races or monsters to choose,
- 33 different weapons (with quality to choose),
- 16 different spells,
- 7 different game modes (including automatic combat mode),
- Save and load system,
- Covering map areas,
- Customizable backgrounds,
- Interactive units management,
- And more...

# Tutorial

## Map Editor

When you start the game, you enter the **Map Editor**. Here, you can customize the battlefield using the following features:

- **Grid Size**: Adjust the grid size to fit your gameplay needs.
- **Background Settings**: Load a custom background and modify its size and position to align with your grid.
- **Map Elements**: Place various elements on the map and adjust their rotation:
  - **Element Rotation**: Right-click before placing an element on the map to rotate it by 90 degrees. You can also set random rotation. When enabled, each placed element will have a random rotation.
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

- **Adding Units:** Open the Unit Management Panel, click on the name of a unit, and then click on the desired location on the grid to place it. Alternatively, use the button to add a unit at a random position on the battlefield.
- **Selecting Units:** Left-click a unit to select it. To select multiple units, enable the appropriate mode in the Unit Management Panel and drag the mouse over the desired units. You can copy selected units with `Ctrl + C` and paste them using `Ctrl + V`.
- **Moving Units:** Click on an empty tile within the unit's movement range to move the selected unit. Movement range is visually highlighted.
- **Attacking:** Right-click on an enemy unit to initiate an attack.
- **Editing Units:** After selecting a unit, you can edit its attributes in the panel located on the left side of the screen.
- **Deleting Units:** Press `Delete` to remove selected units or use the "Delete" button in the Unit Management Panel.

## Covering Areas

This feature is accessible from the main menu. It allows you to hide or reveal parts of the map. To use it, activate the appropriate button, then either click on specific tiles or drag the mouse across the grid. You can toggle this mode on or off by pressing **Ctrl+W**.

Additionally you can use the **right mouse button (RMB)** to number covered tiles. The functionality works as follows:

- **RMB**: Increment the number on the selected tile, cycling from `0` to `9`. When the number reaches `9`, pressing **RMB** again will disable numbering on that tile.
- **Ctrl + RMB**: Immediately disable numbering on the selected tile, regardless of its current number.

This numbering system is helpful for organizing or annotating hidden areas of the map.

## Game Modes and Settings

Press **Esc** to open the main menu, where you can access **Settings**. By default, the settings are optimized for manual gameplay during RPG sessions. For testing purposes, it is recommended to enable the automatic modes.

1. **Automatic Combat** (Ctrl+A): Actions for all units are automated, preventing manual movement when enabled.
2. **Automatic Death** (Ctrl+K): Units with health below zero are automatically removed.
3. **Automatic Unit Selection** (Ctrl+Q): Units are selected in initiative order.
4. **Automatic Defense** (Ctrl+D): Units automatically decide to parry or dodge attacks. When disabled, players choose to block, dodge, or take the damage.
5. **Automatic Dice Rolls** (Ctrl+R): Disable to allow players to use physical dice; manual outcomes can then be entered.
6. **Include Fear Mechanics** (Ctrl+T): When enabled, the mechanics of Fear and Terror are applied.
7. **Friendly Fire** (Ctrl+F): Enables attacking allied units.
8. **Enemy Stats Hiding Mode** (Ctrl+I): Hides the statistics panel of enemy units when they are selected.
9. **Unit Name Hiding Mode** (Ctrl+N): Hides the names of units on their tokens.
10. **Health Points Hiding Mode** (Ctrl+H): Hides the health points of units on their tokens.
