using System;
using static SDL2.SDL;
using System.Timers;
using System.Diagnostics;
using NAudio.Wave;
using SDL2;
using System.Drawing;


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
        static IntPtr SDL_ttf;
        static bool SDL_running = true;

        //CHIP 8 REFERENCE
        static Chip8 chip8;

        //VIDEO
        const int VIDEO_SCALE = 1;
        const int VIDEO_WIDTH = 640;
        const int VIDEO_HEIGHT = 320;
        const int SETTINGS_WIDTH = VIDEO_WIDTH / 3;
        const int SETTINGS_BUFFER = 10;

        //SOUND
        static WaveOutEvent waveOut;
        static AudioFileReader audioFile;

        //CLOCK SPEED
        const int clockFrequency = 750;
        const int cycleTimeMs = 1000 / clockFrequency;
        static Stopwatch clockSpeedStopwatch;

        static void Main(string[] args)
        {
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
            SDL_drawDisplay();
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

                //redraw the display if needed
                if (chip8.drawDisplay == true)
                {
                    SDL_drawDisplay();
                }

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
            waveOut = new WaveOutEvent();
            audioFile = new AudioFileReader("Sound\\beep.wav");
            waveOut.Init(audioFile);
        }

        static private void NAudio_dispose()
        {
            audioFile.Dispose();
            waveOut.Dispose();
        }

        static private bool SDL_setup()
        {
            if (SDL_Init(SDL_INIT_VIDEO) < 0)
            {
                debugMessage($"Unable to initialise  {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

            SDL_window = SDL_CreateWindow(
                "Chip 8 Emulator",
                SDL_WINDOWPOS_UNDEFINED,
                SDL_WINDOWPOS_UNDEFINED,
                (VIDEO_WIDTH * VIDEO_SCALE) + SETTINGS_WIDTH, VIDEO_HEIGHT * VIDEO_SCALE,
                SDL_WindowFlags.SDL_WINDOW_BORDERLESS
                );
            
            if(SDL_window == IntPtr.Zero)
            {
                debugMessage($"Unable to create SDL window. {SDL_GetError()}", ConsoleColor.Red);
                return false;
            }

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
                        if(e.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
                        {
                            SDL_running = false;
                            break;
                        }

                        debugMessage("Key is down.", ConsoleColor.Green);
                        byte keyDown = getKeyPressed(e.key.keysym.sym);
                        if(keyDown != 17)
                        {
                            chip8.keys[keyDown] = 1;
                        }
                        break;

                    case SDL_EventType.SDL_KEYUP:
                        debugMessage("Key is up.", ConsoleColor.Green);
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

        static private void SDL_drawDisplay()
        {
            SDL_SetRenderDrawColor(SDL_renderer, 0, 0, 0, 255);
            SDL_RenderClear(SDL_renderer);

            for(int x = 0; x < chip8.display.GetLength(0); x++)
            {
                for (int y = 0; y < chip8.display.GetLength(1); y++)
                {
                    if(chip8.display[x, y] == 1)
                    {
                        SDL_SetRenderDrawColor(SDL_renderer, 255, 255, 255, 255);

                        SDL_Rect r = new SDL_Rect();
                        r.w = 10 * VIDEO_SCALE;
                        r.h = 10 * VIDEO_SCALE;
                        r.x = x * (10 * VIDEO_SCALE);
                        r.y = y * (10 * VIDEO_SCALE);

                        SDL_RenderFillRect(SDL_renderer, ref r);
                    }
                }
            }

            SDL_RenderDrawLine(SDL_renderer, VIDEO_WIDTH * VIDEO_SCALE + (SETTINGS_BUFFER / 2), SETTINGS_BUFFER / 2, VIDEO_WIDTH * VIDEO_SCALE + (SETTINGS_BUFFER / 2), VIDEO_HEIGHT * VIDEO_SCALE - (SETTINGS_BUFFER / 2));

            SDL_RenderPresent(SDL_renderer);

            chip8.drawDisplay = false;
        }

        #endregion

        #region helper functions

        private static void setupTimers()
        {

        }

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

        static void debugMessage(string message, ConsoleColor colour)
        {
            ConsoleColor previousColour = Console.ForegroundColor;

            Console.ForegroundColor = colour;
            Console.WriteLine($"DEBUG :: {message}\n");

            Console.ForegroundColor = previousColour;
        }

        static void writeDisplayToConsole()
        {
            for (int i = 0; i < chip8.display.GetLength(0); i++)
            {
                for (int j = 0; j < chip8.display.GetLength(1); j++)
                {
                    Console.Write($"{chip8.display[i, j]:X2}");
                }
                Console.Write("\n");
            }
        }

        #endregion


    }
}
