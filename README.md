# A CHIP8 interpreter written in C# using SDL2 and NAudio.

## Table of contents

1. [Basic Usage](#basic-usage)
2. [Controls](#controls)
3. [Examples](#examples)
4. [Credits](#credits)

## Basic Usage
1. Start the .exe in from the most recent release or build an .exe from the source files provided.
2. Opens to the default ROM folder and displays all the .ch8 ROMs in that folder. Test roms are included, plus space invaders!
3. Type your selection into the console, the choices are displayed as follows, with the index as the first character,

  ```
  0 :: ROM1.ch8
  1 :: ROM2.ch8
  ```

  To choose `ROM1.ch8` you would type `0` into the console.
   
5. Emulator opens in a seperate window. See below for controls.

## Controls:
1. The CHIP8 uses a hexidecimal keypad to manage user input, with the standard keypad being arranged as follows, this interpreter maps the keys accordingly.

    ```
     1 | 2 | 3 | C       1 | 2 | 3 | 4 
     4 | 5 | 6 | D  ==>  Q | W | E | R   
     7 | 8 | 9 | E       A | S | D | F
     A | 0 | B | F       Z | X | C | V
    ```


2. There is also an optional debug menu in the SDL window togglable by pressing `L`.[^1] This shows:

- Program counter `pc`
- Index pointer `I`
- Delay timer `DT`
- Sound timer `ST`
- All 16 registers `V0 - VF`

[^1]: This is also always shown in the console window when a ROM is running. The console window can be minimised to stop it showing.

3. To close a ROM and return to the console press `ESCAPE` or simply close the window (unable to in borderless).

## Examples

### Space Invaders
<img src="https://github.com/user-attachments/assets/04accad7-f4e0-4931-a732-fae169e1fbcc" width="640">

### IBM Logo
<img src="https://github.com/user-attachments/assets/341fa88e-2cc5-4959-8e3f-0646fc395338" width="640">

### Flags Test ROM
<img src="https://github.com/user-attachments/assets/99d965e1-7755-49b1-ba63-52a19cc56b25" width="640">


## Credits

Test ROMs were gathered from:
`https://github.com/dmatlack/chip8/tree/master`
`https://github.com/Timendus/chip8-test-suite`


