# BeastSorter

A Path of Exile plugin for ExileCore that automatically processes Bestiary Orbs on captured beasts to create Imprinted Bestiary Orbs.

## Description

BeastSorter is an automation plugin for Path of Exile that streamlines the process of using Bestiary Orbs on captured beasts. Instead of manually right-clicking each orb and selecting beasts one by one, this plugin automates the entire process:

1. **Automatically opens** the Bestiary panel and navigates to the Captured Beasts tab
2. **Detects Bestiary Orbs** in your inventory
3. **Right-clicks orbs** and selects captured beasts
4. **Places resulting Imprinted Bestiary Orbs** in available inventory space
5. **Continues processing** until all orbs are used or no free space remains

## Features

- ✅ **Fully Automated**: One key press starts the entire process
- ✅ **Smart Inventory Management**: Automatically finds free inventory space
- ✅ **Cancellable Operations**: Stop the process at any time with a cancel key
- ✅ **Debug Information**: Optional on-screen debug info for troubleshooting
- ✅ **Configurable Delays**: Adjustable timing for different system performance
- ✅ **Safe Operation**: Built-in checks to prevent errors and handle edge cases

## Usage

### Quick Start

1. **Prepare your inventory**:
   - Place Bestiary Orbs in your inventory
   - Ensure you have free space for the resulting Imprinted Bestiary Orbs

2. **Open required panels**:
   - Open your inventory (default: `I` key)
   - Open the Bestiary panel (default: `N` key)
   - Navigate to the "Captured Beasts" tab

3. **Start the automation**:
   - Press the **Activation Key** (default: `F7`)
   - The plugin will automatically process all Bestiary Orbs

4. **Cancel if needed**:
   - Press the **Cancel Key** (default: `F8`) to stop the process

### Default Key Bindings

| Action | Default Key | Description |
|--------|-------------|-------------|
| **Activate** | `F7` | Start the Bestiary Orb processing |
| **Cancel** | `F8` | Stop the current operation |
| **Open Inventory** | `I` | Open inventory panel |
| **Open Bestiary** | `N` | Open bestiary panel |

### Configuration

Access the plugin settings through ExileCore's plugin menu to customize:

- **Key Bindings**: Change default hotkeys
- **Timing Settings**: Adjust delays for your system performance
- **Debug Options**: Enable/disable debug information
- **Target Settings**: Configure inventory space usage

## How It Works

The plugin performs these steps automatically:

1. **Inventory Check**: Scans your inventory for Bestiary Orbs
2. **Panel Management**: Opens inventory and bestiary panels if needed
3. **Beast Selection**: Finds available captured beasts to use orbs on
4. **Orb Processing**: Right-clicks orbs and selects beasts
5. **Item Placement**: Places resulting Imprinted Bestiary Orbs in free inventory space
6. **Loop Continuation**: Repeats until all orbs are processed or no space remains