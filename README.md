# HOI4 Mod Helper
 A tool for making Hearts of Iron IV mods.
 
## Features

- Copies mod code from any folder to the HOI4 mods folder so your `descriptor.mod` doesn't get nuked
- Automatically creates the `.mod` file in the mods folder to point to the code
- Allows ignoring certain files from being copied over such as the .git folder with the `ignored_files.mod` file
  - Uses the same syntax as `.gitignore`
- Allows dev builds to avoid name conflicts when the mod is also subscribed to, making testing easier
- Automatically converts images
  - Supports the following formats:
    - `.gif`
    - `.webp`
    - `.pbm`
    - `.jpeg`
    - `.qoi`
    - `.tga`
    - `.tiff`
    - `.bmp`
    - `.png`
  - Converts all except the thumbnail to TGA or DDS
- Automatically generates different sized flag textures
