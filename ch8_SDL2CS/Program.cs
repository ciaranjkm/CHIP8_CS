using System;
using static SDL2.SDL;
using System.Timers;
using System.Diagnostics;
using NAudio.Wave;
using SDL2;
using System.Drawing;
using System.Runtime.InteropServices;
using static System.Formats.Asn1.AsnWriter;


namespace ch8_SDL2CS
{
    internal class Program
    {
        //LOADING ROMS
        static string filePath = "test-roms\\";

        //SDL
        static IntPtr SDL_window;
        static IntPtr SDL_renderer;
        static IntPtr SDL_texture;

        static IntPtr font;
        static bool SDL_running = true;

        //CHIP 8 REFERENCE
        static Chip8 chip8;

        //VIDEO
        const int VIDEO_SCALE = 2;
        const int VIDEO_WIDTH = 640;
        const int VIDEO_HEIGHT = 320;

        //VIDEO :: DEBUG WINDOW + CONTROLS WINDOW
        const int SETTINGS_WIDTH = VIDEO_WIDTH / 3;
        const int SETTINGS_BUFFER = 10;

        static bool debugWindowShown;
        static bool controlsWindowShown;

        //SOUND
        static WaveOutEvent waveOut;
        static AudioFileReader audioFile;

        //CLOCK SPEED
        const int clockFrequency = 750;
        const int cycleTimeMs = 1000 / clockFrequency;
        static Stopwatch clockSpeedStopwatch;

        static void Main(string[] args)
        {
            //run the emulator
            emulator();
        }

        static void emulator()
        {
            //load the ROM
            byte[] program = loadRom();       
            
            //initialise chip8
            chip8 = new Chip8();
            chip8.initialise(program);

            //initialise SDL
            bool setup = SDL_setup();
            
            //did SDL initialise correctly
            if(setup == false)
            {
                //if it failed restart
                emulator();
            }

            //set to true if sdl initialised correctly
            SDL_running = true;

            //clear the screen with black 
            SDL_updateGameTexture();
            SDL_SetRenderDrawColor(SDL_renderer, 0, 0, 0, 255);
            SDL_RenderClear(SDL_renderer);

            //initialise NAudio to beep for soundTimer
            NAudio_setup();

            //start a new timer for the clock
            clockSpeedStopwatch = Stopwatch.StartNew();
            //set the previous time
            long previousElapsedTime = clockSpeedStopwatch.ElapsedMilliseconds;
            //set the timeDeltaTotal to 0
            double timeDeltaTotal = 0;

            int totalVideoWidth = (VIDEO_WIDTH * VIDEO_SCALE);
            int totalVideoHeight = (VIDEO_HEIGHT * VIDEO_SCALE);

            while (SDL_running)
            {
                //get the current time of the clock
                long currentTime = clockSpeedStopwatch.ElapsedMilliseconds;
                //find the delta to the previous cycle time
                long delta = currentTime - previousElapsedTime;
                //set the last time as the current time
                previousElapsedTime = currentTime;

                //add the delta to the timeDeltaTotal
                timeDeltaTotal += delta;

                //if the cpu needs to catch up
                while(timeDeltaTotal >= cycleTimeMs)
                {
                    //take one step in cpu
                    chip8.step();

                    //reduce the time catchup by one cycle
                    timeDeltaTotal -= cycleTimeMs;
                }

                //read events from SDL
                SDL_pollEvents();

                //update the texture for the display if needed
                if (chip8.drawDisplay == true)
                {
                    SDL_updateGameTexture();
                }

                //Create a destination texture for the chip8 texture
                SDL_Rect destinationRect = new SDL_Rect()
                {
                    x = 0,
                    y = 0,
                    w = VIDEO_WIDTH * VIDEO_SCALE,
                    h = VIDEO_HEIGHT * VIDEO_SCALE
                };

                //Clear the background with black
                SDL_SetRenderDrawColor(SDL_renderer, 0, 0, 0, 255);
                SDL_RenderClear(SDL_renderer);
                
                //Copy chip8 texture onto window
                SDL_RenderCopy(SDL_renderer, SDL_texture, IntPtr.Zero, ref destinationRect);


                if(debugWindowShown == true)
                {
                    //Draw border line
                    SDL_SetRenderDrawColor(SDL_renderer, 255, 0, 0, 255);
                    SDL_RenderDrawLine(SDL_renderer, totalVideoWidth + (SETTINGS_BUFFER / 2), 0 + (SETTINGS_BUFFER / 2), totalVideoWidth + (SETTINGS_BUFFER / 2), totalVideoHeight - SETTINGS_BUFFER);

                    SDL_drawDebugWindowText();
                }
                else if(controlsWindowShown == true)
                {
                    //Draw border line
                    SDL_SetRenderDrawColor(SDL_renderer, 255, 255, 255, 255);
                    SDL_RenderDrawLine(SDL_renderer, totalVideoWidth + (SETTINGS_BUFFER / 2), 0 + (SETTINGS_BUFFER / 2), totalVideoWidth + (SETTINGS_BUFFER / 2), totalVideoHeight - SETTINGS_BUFFER);

                }

                SDL_RenderPresent(SDL_renderer);

                //if the sound timer needs to beep
                if (chip8.beep == true)
                {
                    //is there a sound file currently playing
                    if (waveOut.PlaybackState != PlaybackState.Playing)
                    {
                        //if there isnt start playing new beep
                        audioFile.Position = 0;
                        waveOut.Play();
                    }
                }
                //is the sound timer not beeping
                else
                {
                    //is the sound playing
                    if (waveOut.PlaybackState != PlaybackState.Stopped)
                    {
                        //stop it if it is
                        waveOut.Stop();
                    }
                }
            }

            //when the sdl window is closed cleanup sdl, naudio and dispose of chip8
            SDL_close();
            NAudio_dispose();
            chip8.close();
            chip8 = null;

            //recall back to this method
            emulator();
        }

        #region SDL / NAudio

        static private void NAudio_setup()
        {
            //create a new waveoutevent and read the beep.wav into the audiofilereader
            waveOut = new WaveOutEvent();
            audioFile = new AudioFileReader("Sound\\beep.wav");

            //initialise the waveoutevent
            waveOut.Init(audioFile);
        }

        static private void NAudio_dispose()
        {
            audioFile.Dispose();
            waveOut.Dispose();
        }

        static private bool SDL_setup()
        {

            //initialise SDL
            if (SDL_Init(SDL_INIT_VIDEO) < 0)
            {
                debugMessage($"Unable to initialise  {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            //create and initialise SDL_window
            SDL_window = SDL_CreateWindow(
                "Chip 8 Emulator",
                SDL_WINDOWPOS_UNDEFINED,
                SDL_WINDOWPOS_UNDEFINED,
                (VIDEO_WIDTH * VIDEO_SCALE), VIDEO_HEIGHT * VIDEO_SCALE,
                SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL_WindowFlags.SDL_WINDOW_BORDERLESS
                );
            
            if(SDL_window == IntPtr.Zero)
            {
                debugMessage($"Unable to create SDL window. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            //create and initialise SDL_renderer
            SDL_renderer = SDL_CreateRenderer(
                SDL_window,
                -1,
                0
                );

            if(SDL_renderer == IntPtr.Zero)
            {
                debugMessage($"Unable to create SDL renderer. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            //create and initialise SDL_texture
            SDL_texture = SDL_CreateTexture(
                SDL_renderer,
                SDL.SDL_PIXELFORMAT_ABGR8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                VIDEO_WIDTH / 10,
                VIDEO_HEIGHT / 10
                );

            if(SDL_texture == IntPtr.Zero)
            {
                debugMessage($"Unable to create SDL texture. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            //create and intitiate SDL_ttf and load it with the font
            if (SDL_ttf.TTF_Init() < 0)
            {
                debugMessage($"Unable to create SDL ttf. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            font = SDL_ttf.TTF_OpenFont("Font\\Roboto-Medium.ttf", 20);
            if(font == IntPtr.Zero)
            {
                debugMessage($"Unable to create SDL font. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            return true;
        }

        static private void SDL_pollEvents()
        {
            while(SDL_PollEvent(out SDL_Event e) == 1)
            {
                switch (e.type)
                {
                    case SDL_EventType.SDL_QUIT:
                        SDL_running = false;
                        break;

                    case SDL_EventType.SDL_KEYDOWN:
                        //on escape down exit the window
                        if(e.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
                        {
                            SDL_running = false;
                            break;
                        }
                        //on l down toggle the debug extension
                        else if (e.key.keysym.sym == SDL_Keycode.SDLK_l)
                        {
                            if(controlsWindowShown != true)
                            {
                                debugWindowShown = SDL_extendWindow(debugWindowShown);
                                break;
                            }
                        }
                        //on m down toggle controls extension
                        else if(e.key.keysym.sym == SDL_Keycode.SDLK_m)
                        {
                            if(debugWindowShown != true)
                            {
                                controlsWindowShown = SDL_extendWindow(controlsWindowShown);
                                break;
                            }
                        }

                        //check if key pressed is a chip8 key
                        byte keyDown = getKeyPressed(e.key.keysym.sym);
                        if(keyDown != 17)
                        {
                            chip8.keys[keyDown] = 1;
                        }
                        break;

                    case SDL_EventType.SDL_KEYUP:


                        byte keyUp = getKeyPressed(e.key.keysym.sym);
                        if (keyUp != 17)
                        {
                            chip8.keys[keyUp] = 0;
                        }
                        break;
                }
            }
        }

        static private void SDL_close()
        {
            SDL_DestroyRenderer(SDL_renderer);
            SDL_DestroyWindow(SDL_window);
            SDL_Quit();
        }

        static private void SDL_updateGameTexture()
        {
            //read the display into a variable
            byte[,] display = chip8.display;

            int index = 0;

            //create a new pixelbuffer for the game texture
            int[] pixelBuffer = new int[64 * 32];

            //loop through all bits in the display
            for (int y = 0; y < display.GetLength(1); y++)
            {
                for (int x = 0; x < display.GetLength(0); x++)
                {
                    //get the pixel 
                    byte pixel = display[x, y];
                    //set pixel buffer to black or white if pixel is 1 or 0
                    pixelBuffer[y * 64 + x] = pixel == 1 ? unchecked((int)0xFFFFFFFF) : unchecked((int)0xFF000000);
                }
            }

            IntPtr pixels;
            int pitch;

            //copy texture to render on screen
            SDL_LockTexture(SDL_texture, IntPtr.Zero, out pixels, out pitch);
            Marshal.Copy(pixelBuffer, 0, pixels, pixelBuffer.Length);
            SDL_UnlockTexture(SDL_texture);

            //toggle drawDisplay
            chip8.drawDisplay = false;
        }

        static private bool SDL_extendWindow(bool extensionType)
        {
            //if the window is not extended
            if (extensionType != true)
            {
                //toggle to render the debug window in emulate loop
                extensionType = !extensionType;

                //calculate the new window size
                int newWindowWidth = (VIDEO_WIDTH * VIDEO_SCALE) + (extensionType ? SETTINGS_WIDTH : 0);
                int newWindowHeight = VIDEO_HEIGHT * VIDEO_SCALE;

                //set the new window size
                SDL_SetWindowSize(SDL_window, newWindowWidth, newWindowHeight);

                //create a new viewport for the window and set it to the window
                SDL_GetWindowSize(SDL_window, out var w, out var h);
                SDL_Rect viewport = new SDL.SDL_Rect { x = 0, y = 0, w = w, h = h };
                SDL_RenderSetViewport(SDL_renderer, ref viewport);
            }
            else if (extensionType != false)
            {
                //toggle to render the debug window in emulate loop
                extensionType = !extensionType;

                //calculate the new window size
                int newWindowWidth = (VIDEO_WIDTH * VIDEO_SCALE);
                int newWindowHeight = VIDEO_HEIGHT * VIDEO_SCALE;

                //set the new window size
                SDL_SetWindowSize(SDL_window, newWindowWidth, newWindowHeight);

                //create a new viewport for the window and set it to the window
                SDL_GetWindowSize(SDL_window, out var w, out var h);
                SDL_Rect viewport = new SDL.SDL_Rect { x = 0, y = 0, w = w, h = h };
                SDL_RenderSetViewport(SDL_renderer, ref viewport);
            }

            return extensionType;
        }

        static private bool SDL_renderText(string text, int x, int y, SDL_Color colour)
        {
            //create a new surface of text
            IntPtr textSurface = SDL_ttf.TTF_RenderText_Solid(font, text, colour);
            if(textSurface == IntPtr.Zero)
            {
                debugMessage($"DEBUG :: Couldn't create textSurface. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            //create a texture from the surface
            IntPtr textTexture = SDL_CreateTextureFromSurface(SDL_renderer, textSurface);
            SDL_FreeSurface(textSurface);

            //get dimension of text to make a rect
            SDL_QueryTexture(textTexture, out _, out _, out int tW, out int tH);
            SDL_Rect textRect = new SDL.SDL_Rect()
            {
                x = x,
                y = y,
                w = tW,
                h = tH
            };

            //copy texture to window at the textRect location
            SDL_RenderCopy(SDL_renderer, textTexture, IntPtr.Zero, ref textRect);

            //destroy the texture
            SDL_DestroyTexture(textTexture);

            return true;
        }

        static private SDL_Color rgbaToSDLColour(int r, int g, int b, int a)
        {
            return new SDL_Color() { r = (byte)r, b = (byte)b, g = (byte)g, a = (byte)a };
        }

        #endregion

        #region helper functions

        private static byte getKeyPressed(SDL_Keycode keyPressed)
        {
            Dictionary<SDL_Keycode, byte> keymap = new Dictionary<SDL_Keycode, byte>()
            {
                /*
                  1	2 3 C
                  4	5 6 D
                  7	8 9 E
                  A	0 B F
                */

                {SDL_Keycode.SDLK_1, 0x1},
                {SDL_Keycode.SDLK_2, 0x2},
                {SDL_Keycode.SDLK_3, 0x3},
                {SDL_Keycode.SDLK_4, 0xc},
                {SDL_Keycode.SDLK_q, 0x4},
                {SDL_Keycode.SDLK_w, 0x5},
                {SDL_Keycode.SDLK_e, 0x6},
                {SDL_Keycode.SDLK_r, 0xd},
                {SDL_Keycode.SDLK_a, 0x7},
                {SDL_Keycode.SDLK_s, 0x8},
                {SDL_Keycode.SDLK_d, 0x9},
                {SDL_Keycode.SDLK_f, 0xe},
                {SDL_Keycode.SDLK_z, 0xa},
                {SDL_Keycode.SDLK_x, 0x0},
                {SDL_Keycode.SDLK_c, 0xb},
                {SDL_Keycode.SDLK_v, 0xf},
            };

            if (keymap.TryGetValue(keyPressed, out byte keyValue))
            {
                return keyValue;
            }

            return 17;
        }

        static byte[] loadRom()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"==== ROM FILES @ {filePath} ====\n");

            string[] fileNames = Directory.GetFiles("test-roms\\");

            for (int i = 0; i < fileNames.Length; i++)
            {
                Console.WriteLine($"{i.ToString()} :: {fileNames[i].Replace("test-roms\\", "")}");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\nEnter your choice to load ROM :: ");

            int choice = Convert.ToInt32(Console.ReadLine());

            if(choice > fileNames.Length - 1 || choice < 0)
            {
                Console.Clear();
                loadRom();
            }

            try
            {
                byte[] fileDataBytes = File.ReadAllBytes($"{fileNames[choice]}");
                debugMessage("Loaded ROM file successfully.", ConsoleColor.Green);
                return fileDataBytes;
            }
            catch
            {
                debugMessage("Couldn't open ROM file.", ConsoleColor.Red);
                return null;
            }
        }

        #endregion

        #region debug functions

        static private void debugMessage(string message, ConsoleColor colour)
        {
            ConsoleColor previousColour = Console.ForegroundColor;

            Console.ForegroundColor = colour;
            Console.WriteLine($"DEBUG :: {message}\n");

            Console.ForegroundColor = previousColour;
        }

        static private void SDL_drawDebugWindowText()
        {
            int startX = (VIDEO_WIDTH * VIDEO_SCALE) + (SETTINGS_BUFFER * 2);
            int startY = 0;

            int lineHeight = 30;

            SDL_renderText("DEBUG INFO", startX, startY, rgbaToSDLColour(255, 0, 0, 255));
            startY += lineHeight;

            SDL_renderText($"PC :: 0x{chip8.pc:X4}", startX, startY, rgbaToSDLColour(60, 200, 60, 255));
            startY += lineHeight;

            SDL_renderText($"I :: 0x{chip8.I:X4}", startX, startY, rgbaToSDLColour(60, 200, 60, 255));
            startY += lineHeight;

            SDL_renderText($"DT :: 0x{chip8.delayTimer:X2}", startX, startY, rgbaToSDLColour(240, 60, 60, 255));
            startY += lineHeight;

            SDL_renderText($"ST :: 0x{chip8.soundTimer:X2}", startX, startY, rgbaToSDLColour(240, 60, 60, 255));
            startY += lineHeight;

            SDL_SetRenderDrawColor(SDL_renderer, 60, 60, 255, 0);
            for (int i = 0; i < chip8.registers.Length; i++)
            {
                SDL_renderText($"V{i:X1} :: {chip8.registers[i]:X2}", startX, startY, rgbaToSDLColour(60, 255, 60, 255));
                startY += lineHeight;
            }
        }

        #endregion
    }
}
