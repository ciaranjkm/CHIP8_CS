using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ch8_SDL2CS
{
    internal class Chip8
    {        
        public bool drawDisplay = false;

        //Constants for memory addresses and sizes of arrays

        const int MEMORY_SIZE = 4096;
        const int REGISTERS_SIZE = 16;
        const int STACK_SIZE = 16;
        const int MEMORY_START = 0x200;
        const int FONT_START = 0x50;
        const int DISPLAY_HEIGHT = 32;
        const int DISPLAY_WIDTH = 64;

        const int KEYS_SIZE = 16;

        //16 bit opcodes, index pointer, and program counter
        public ushort opcode { private set; get; }
        public ushort I { private set; get; }
        public ushort pc { private set; get; }

        //stack for memory addresses (minimum 12, 16 recommended) and a stack pointer
        Stack<ushort> stack;

        //4KB of memory
        byte[] memory;
        //16, 8 bit registers
        public byte[] registers { private set; get; }

        public byte[] keys;

        //64x32 array to store display, monochrome 0 or 1
        public byte[,] display { get; private set; }

        //8 bit delay and sound timer 
        public byte soundTimer { private set; get; }
        public byte delayTimer { private set; get; }
        public bool beep;

        System.Timers.Timer clock60Hz;
        System.Timers.Timer debugInfo;

        //default font used by the chip8
        byte[] fontset = new byte[]
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
	        0x20, 0x60, 0x20, 0x20, 0x70, // 1
	        0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
	        0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
	        0x90, 0x90, 0xF0, 0x10, 0x10, // 4
	        0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
	        0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
	        0xF0, 0x10, 0x20, 0x40, 0x40, // 7
	        0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
	        0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
	        0xF0, 0x90, 0xF0, 0x90, 0x90, // A
	        0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
	        0xF0, 0x80, 0x80, 0x80, 0xF0, // C
	        0xE0, 0x90, 0x90, 0x90, 0xE0, // D
	        0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
	        0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };

        public Chip8()
        {
            
        }

        public void initialise(byte[] program)
        {
            //starting memory location
            pc = MEMORY_START;

            //initalise stack, memory, register, display
            stack = new Stack<ushort>();
            memory = new byte[MEMORY_SIZE];
            registers = new byte[REGISTERS_SIZE];
            display = new byte[DISPLAY_WIDTH, DISPLAY_HEIGHT];
            keys = new byte[KEYS_SIZE];

            loadFontIntoMemory(fontset);
            loadProgramIntoMemory(program);

            clock60Hz = new System.Timers.Timer(1000 / 60);
            clock60Hz.Start();
            clock60Hz.Elapsed += Clock60Hz_Elapsed;

            debugInfo = new System.Timers.Timer(400);
            debugInfo.Start();
            debugInfo.Elapsed += DebugInfo_Elapsed;
        }

        public void close()
        {
            clock60Hz.Stop();
            debugInfo.Stop();
        }

        private void DebugInfo_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            displayDebugInfoInConsole();
        }

        private void Clock60Hz_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (delayTimer > 0)
            {
                delayTimer--;
            }

            if (soundTimer > 0)
            {
                soundTimer--;
                if (soundTimer == 0)
                {
                    beep = false;
                }
                else
                {
                    beep = true;
                }
            }
        }

        public void step()
        {
            opcode = (ushort)((memory[pc] << 8) | memory[pc + 1]);
            pc += 2;

            byte identifier = (byte)(opcode >> 12);
            byte X = (byte)((opcode >> 8) & 0x0f);
            byte Y = (byte)((opcode & 0x00f0) >> 4);
            byte N = (byte)(opcode & 0x000f);

            byte NN = (byte)(opcode & 0x00ff);
            ushort NNN = (ushort)(opcode & 0x0fff);

            executeOpcode(identifier, X, Y, N, NN, NNN);
        }

        #region opcode functions
        //execute opcodes
        private void executeOpcode(byte identifier, byte X, byte Y, byte N, byte NN, ushort NNN)
        {
            switch (identifier)
            {
                case 0x0:
                    switch (N)
                    {
                        case 0x0:
                            op_0x00E0();
                            break;
                        case 0xe:
                            op_0x00EE();
                            break;
                    }
                    break;

                case 0x1:
                    op_1NNN(NNN);
                    break;

                case 0x2:
                    op_2NNN(NNN);
                    break;

                case 0x3:
                    op_3XNN(X, NN);
                    break;

                case 0x4:
                    op_4XNN(X, NN);
                    break;

                case 0x5:
                    op_5XY0(X, Y);
                    break;

                case 0x6:
                    op_6XNN(X, NN);
                    break;

                case 0x7:
                    op_7XNN(X, NN);
                    break;

                case 0x8:
                    switch (N)
                    {
                        case 0x0:
                            op_8XY0(X, Y);
                            break;

                        case 0x1:
                            op_8XY1(X, Y);
                            break;

                        case 0x2:
                            op_8XY2(X, Y);
                            break;

                        case 0x3:
                            op_8XY3(X, Y);
                            break;

                        case 0x4:
                            op_8XY4(X, Y);
                            break;

                        case 0x5:
                            op_8XY5(X, Y);
                            break;

                        case 0x6:
                            op_8XY6(X);
                            break;

                        case 0x7:
                            op_8XY7(X, Y);
                            break;

                        case 0xe:
                            op_8XYE(X);
                            break;
                    }
                    break;

                case 0x9:
                    op_9XY0(X, Y);
                    break;

                case 0xa:
                    op_ANNN(NNN);
                    break;

                case 0xb:
                    op_BNNN(X, NNN);
                    break;

                case 0xc:
                    op_CXNN(X, NN);
                    break;

                case 0xd:
                    op_DXYN(X, Y, N);
                    break;

                case 0xe:
                    switch (N)
                    {
                        case 0xe:
                            op_EX9E(X);
                            break;

                        case 0x1:
                            op_EXA1(X);
                            break;
                    }
                    break;

                case 0xf:
                    switch (Y)
                    {
                        case 0x0:
                            switch (N)
                            {
                                case 0x7:
                                    op_FX07(X);
                                    break;

                                case 0xa:
                                    op_FX0A(X);
                                    break;
                            }
                            break;

                        case 0x1:
                            switch (N)
                            {
                                case 0x5:
                                    op_FX15(X);
                                    break;

                                case 0x8:
                                    op_FX18(X);
                                    break;

                                case 0xe:
                                    op_FX1E(X);
                                    break;

                            }
                            break;

                        case 0x2:
                            op_FX29(X);
                            break;

                        case 0x3:
                            op_FX33(X);
                            break;

                        case 0x5:
                            op_FX55(X);
                            break;

                        case 0x6:
                            op_FX65(X);
                            break;
                    }
                    break;
            }

        }

        //clear the screen
        private void op_0x00E0()
        {
            for(int w = 0; w < DISPLAY_WIDTH; w++)
            {
                for(int h = 0; h < DISPLAY_HEIGHT; h++)
                {
                    display[w, h] = 0;
                }
            }
        }

        //return from a subroutine
        private void op_0x00EE()
        {
            //address at top -1 is previous subroutine, return program counter to that address and reduce the stack pointer
            ushort popped = stack.Pop();
            pc = popped;
        }

        //jump to subroutine
        private void op_1NNN(ushort NNN)
        {
            pc = NNN;
        }

        //Call the subroutine at NNN
        private void op_2NNN(ushort NNN)
        {
            stack.Push(pc);
            pc = NNN;
        }

        //Skip instruction if Vx == NN
        private void op_3XNN(byte X, byte NN)
        {
            if (registers[X] == NN)
            {
                pc += 2;
            }
        }

        //Skip instruction if Vx != NN
        private void op_4XNN(byte X, byte NN)
        {
            if(registers[X] != NN)
            {
                pc += 2;
            }
        }

        //Skip instruction if Vx == Vy
        private void op_5XY0(byte X, byte Y)
        {
            if (registers[X] == registers[Y])
            {
                pc += 2;
            }
        }

        //Set Vx = NN
        private void op_6XNN(byte X, byte NN)
        {
            registers[X] = NN;
        }

        //Add Vx and NN and store in Vx
        private void op_7XNN(byte X, byte NN)
        {
            registers[X] += NN;
        }

        //Set Vx = Vy
        private void op_8XY0(byte X, byte Y)
        {
            registers[X] = registers[Y];
        }

        //Set Vx = Vx | Vy
        private void op_8XY1(byte X, byte Y)
        {
            registers[X] = (byte)(registers[X] | registers[Y]);
        }

        //Set Vx = Vx & Vy
        private void op_8XY2(byte X, byte Y)
        {
            registers[X] = (byte)(registers[X] & registers[Y]);
        }

        //Set Vx = Vx ^ Vy
        private void op_8XY3(byte X, byte Y)
        {
            registers[X] = (byte)(registers[X] ^  registers[Y]);
        }

        //Add Vx to Vy and store in Vx, if result is greater than 255 set Vf to 1
        private void op_8XY4(byte X, byte Y)
        {
            ushort result = (ushort)(registers[X] + registers[Y]);
            if(result > 255)
            {
                result = 1;
            }
            else
            {
                result = 0;
            }

            registers[X] += registers[Y];
            registers[0xf] = (byte)result;
        }

        //Set Vx = Vx - Vy, set VF to 1 if Vx > Vy
        private void op_8XY5(byte X, byte Y)
        {
            byte result = 1;

            if (registers[Y] > registers[X])
            {
                result = 0;
            }

            registers[X] -= registers[Y];
            registers[0xf] = result;
        }

        //If least significant bit of Vx is 1 set Vf to 1, else 0. Then divide Vx by 2
        private void op_8XY6(byte X)
        {
            byte result = 0;

            if (registers[X] % 2 == 1)
            {
                result = 1;
            }

            registers[X] >>= 1;
            registers[0xf] = result;
        }

        //If Vy > Vx, then VF is set to 1, otherwise 0. Then Vx is subtracted from Vy, and the results stored in Vx.
        private void op_8XY7(byte X, byte Y)
        {
            byte result = 0;

            if (registers[Y] >= registers[X])
            {
                result = 1;
            }

            registers[X] = (byte)(registers[Y] - registers[X]);
            registers[0xf] = result;
        }

        //If the most-significant bit of Vx is 1, then VF is set to 1, otherwise to 0. Then Vx is multiplied by 2.
        private void op_8XYE(byte X)
        {
            byte result = (byte)((registers[X] & 0x80) >> 7);

            registers[X] <<= 1;
            registers[0xf] = result;

        }

        //Skip next instruction if Vx != Vy
        private void op_9XY0(byte X, byte Y)
        {
            if (registers[X] != registers[Y])
            {
                pc += 2;
            }
        }

        //Set I = nnn
        private void op_ANNN(ushort NNN)
        {
            I = NNN;
        }

        //Jump to location NNN + V0
        private void op_BNNN(byte X, ushort NNN)
        {
            pc = (ushort)(NNN + registers[X]);
        }

        //Set Vx = random byte AND kk
        private void op_CXNN(byte X, byte NN)
        {
            Random r = new Random();
            byte randomByte = (byte)(r.Next(0, 256));

            registers[X] = (byte)(randomByte & NN);
        }

        //Display n-byte sprite starting at memory location I at (Vx, Vy), set VF = collision
        private void op_DXYN(byte X, byte Y, byte N)
        {
            registers[0xf] = 0;
            byte collision = 0;

            for(int i = 0; i < N; i++)
            {
                int y = (registers[Y] + i) % DISPLAY_HEIGHT;
                byte spriteByte = memory[I + i];

                for(int bit = 0; bit < 8; bit++)
                {
                    if ((spriteByte & 0x80) != 0)
                    {
                        int x = (registers[X] + bit) % DISPLAY_WIDTH;

                        if (display[x, y] == 1)
                        {
                            collision = 1;
                        }

                        display[x, y] ^= 0x1;
                    }

                    spriteByte <<= 0x1;
                }
            }

            registers[0xf] = collision;

            drawDisplay = true;
        }

        //If key in Vx is currently down skip instruction
        private void op_EX9E(byte X)
        {
            if (keys[registers[X]] == 1)
            {
                pc += 2;
            }
        }

        //If key in Vx is currently not down skip instruction
        private void op_EXA1(byte X)
        {
            if (keys[registers[X]] != 1)
            {
                pc += 2;
            }
        }

        //Set Vx as the value of delay timer
        private void op_FX07(byte X)
        {
            registers[X] = delayTimer;
        }

        //Wait for key press, store key pressed in Vx
        private void op_FX0A(byte X)
        {
            for(int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == 1)
                {
                    registers[X] = (byte)i;
                }
            }

            pc -= 2;
        }

        //Set the value of the delay timer
        private void op_FX15(byte X)
        {
            delayTimer = registers[X];
        }

        //Set the value of the sound timer
        private void op_FX18(byte X)
        {
            soundTimer = registers[X];
        }

        //Add Vx and I and store in Vx
        private void op_FX1E(byte X)
        {
            I += registers[X];
        }

        //Set I to location in memory for digit in Vx
        private void op_FX29(byte X)
        {
            I = (ushort)(FONT_START + (5 * registers[X]));
        }

        //Put HTU of Vx and put into memory[I, I+1, I+2]
        private void op_FX33(byte X)
        {
            int Vx = registers[X];

            memory[I + 2] = (byte)(Vx % 10);
            Vx /= 10;

            memory[I + 1] = (byte)(Vx % 10);
            Vx /= 10;

            memory[I] = (byte)(Vx % 10);
        }

        //Store V0 - Vx in memory starting at I
        private void op_FX55(byte X)
        {
            for(int i = 0; i <= X; i++)
            {
                memory[I + i] = registers[i];
            }
        }

        //Read memory into registers V0 to Vx starting at I
        private void op_FX65(byte X)
        {
            for(int i = 0; i <= X; i++)
            {
                registers[i] = memory[I + i];
            }
        }

        #endregion

        #region helper functions

        private void loadProgramIntoMemory(byte[] program)
        {
            for (int i = 0; i < program.Length; i++)
            {
                memory[MEMORY_START + i] = program[i];
            }
        }

        private void loadFontIntoMemory(byte[] font)
        {
            for (int i = 0; i < font.Length; i++)
            {
                memory[FONT_START + i] = font[i];
            }
        }

        #endregion

        #region debug functions

        private void displayDebugInfoInConsole()
        {
            Console.Clear();
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine("== CHIP-8 Debug Info ==\n");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"PC: 0x{pc:X3}\nI:  0x{I:X3}\n");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("== Registers ==");

            for (int i = 0; i < 16; i++)
                Console.WriteLine($"V{i:X}: {registers[i]:X2}");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n== Timers ==\nDelay Timer: {delayTimer}\nSound Timer: {soundTimer}");

            Console.ForegroundColor = ConsoleColor.White;
        }

        public void displayMemoryInConsole()
        {
            int count = 0;
            foreach(byte b in memory)
            {
                Console.Write($"{b:X2} ");
                count++;

                if(count == 16)
                {
                    Console.Write("\n");
                    count = 0;
                }
            }
        }

        void debugMessage(string message, ConsoleColor colour)
        {
            ConsoleColor previousColour = Console.ForegroundColor;

            Console.ForegroundColor = colour;
            Console.WriteLine($"DEBUG :: {message}\n");

            Console.ForegroundColor = previousColour;
        }

        #endregion
    }
}
