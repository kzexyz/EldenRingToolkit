# Elden Ring Toolkit

A small Elden Ring utility I originally made for myself.

It started as a way to keep a few things I use often in one place — save editing, inventory stuff, presets, and skin mods. After a while I figured there was no real reason to keep it private, so here it is.

## Features

### Save
- Load `.sl2` save files
- Auto-detect saves
- Manual save selection
- Backup and restore
- Save checksum handling

### Character
- Edit character name
- Edit level and runes
- Edit stats
- Select character slots

### Inventory
- Browse inventory
- Search and filter items
- Edit quantity
- Edit weapon upgrade level
- Edit Ash of War and affinity
- Add items to Inventory or Storage
- Remove items
- Item icons
- Built-in item database

Current item categories include:

- Weapons
- Armor
- Talismans
- Consumables
- Sorceries
- Incantations
- Spirit Ashes
- Ashes of War
- Key Items

### Presets
- Import and export appearance presets
- Apply presets to supported character slots

### Skin Mods
- Install and replace `.partsbnd.dcx` skin mods
- Mod Engine 2 folder support
- Detect `mod\parts`
- View installed replacements
- Replace or restore skins

### Item Database
- Search items
- Browse by category
- View item IDs and icons

## Installation

Windows x64 only for now.

You can use either the installer or the portable build from the Releases page.

### Installer

Run:

`Elden-Ring-Toolkit-Setup-v1.0a.exe`

The installer checks for .NET 10 Desktop Runtime and will offer the Microsoft download if it is missing.

### Portable

Extract:

`Elden-Ring-Toolkit-v1.0a-win-x64-portable.zip`

and run:

`Elden Ring Toolkit.exe`

## Requirements

- Windows 10 / 11 x64
- .NET 10 Desktop Runtime x64
- Elden Ring

## Save Safety

The toolkit creates backups, but keeping your own backup is still a good idea.

Do not edit a save while the game is running.

## Building

Built with:

- C#
- .NET 10
- WPF
- Visual Studio

Open:

`EldenRingToolkit.slnx`

and build normally in Visual Studio.

## Project Status

This is an **alpha** release.

There will probably be bugs, missing data, and things that still need cleaning up.

If you find something broken, feel free to open an issue.

## Contributing

Pull requests and bug reports are welcome.

Things that would help:

- Testing
- Missing item data
- Item icons
- Documentation fixes
- Bug fixes
- Feature ideas

## Credits

Credits for libraries, tools, and research used by the project will be listed here.

## Disclaimer

This is an unofficial fan-made project and is not affiliated with FromSoftware or Bandai Namco Entertainment.

Elden Ring and related assets belong to their respective owners.

## License

License details will be added soon.
