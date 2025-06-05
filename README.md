### GMLoader for UFO 50
Intended to be used as an alternative to DeltaPatcher for mod installation. Allows users to install mods to and from any version of UFO 50.

### How To Install Mods
Modders should provide a zip folder called `mods.zip`. Unzip that and drag the resulting `mods` folder into the GMLoader folder where a mods folder already exists. For the time being, do not install multiple mods at a time unless they are tested to be compatible with each other.

### How To Distribute Mods
For now, follow the [GMLoader guide for pizza tower](https://github.com/Senjay-id/GMLoader/wiki/Pizza-Tower-Guide).

GMLoader is a program that can recompile non-YYC GameMaker Studio games data using [UndertaleModLib](https://github.com/krzys-h/UndertaleModTool).

### Features
* Adding or replacing GML assets by loading the gml files inside GameFolder/mods/code/*.gml
* Adding or replacing textures by loading the texture files inside GameFolder/mods/textures/*.png
* Manipulate sprite properties by making sprite configuration files inside GameFolder/mods/config/textures_properties/*.yaml
* Append GML code by importing the gml files inside GameFolder/mods/code/appendgml/Any Folder Name/*.gml
* Prepend GML code by importing the gml files inside GameFolder/mods/code/prependgml/Any Folder Name/*.gml
* Existing GameObjects Manipulation by loading configuration files inside GameFolder/mods/config/existing_object/*.json
* Add and Manipulate new GameObjects by loading configuration files inside GameFolder/mods/config/new_object/*.json
* Import shaders by loading exported shader files inside GameFolder/mods/shader/*any_shader_folder
* Import rooms by loading the room files inside GameFolder/mods/room/*.json
* Load your own custom CSharp Script files inside GameFolder/csx/pre/*.csx or GameFolder/csx/post/*.csx

### [Wiki](https://github.com/Senjay-id/GMLoader/wiki)
Contains User guide and Modders guide