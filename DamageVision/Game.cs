using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DamageVision {
    public static class Game {

        public static bool Ready { get; private set; } = false;

        private static Process game;
        private static ulong[] offsets;

        public static bool CheckExit() {
            if (Ready) {
                if (game.HasExited) {
                    Ready = false;
                    return true;
                }
            }
            return false;
        }

        public static void Init() {
            Ready = false;
            // find game process
            Process[] processes = Process.GetProcessesByName("MonsterHunterWorld");
            if (processes.Count() == 0) {
                return;
            }
            if (processes.Count() > 1) {
                Utils.Log(2, "Multiple MHW processes found, will use the first one");
            }
            Utils.Log(1, "MHW process found");
            game = processes.FirstOrDefault();
            try {
                Utils.Log(0, $"MHW base address is {game.MainModule.BaseAddress}");
            } catch (Exception) {
                Utils.Log(3, "Access denied");
                return;
            }
            // locate offsets
            ulong[] uOffsets = FindPatterns();
            if (uOffsets.Any(x => x <= 0x1400FFFFF)) {
                Utils.Log(3, $"Failed to locate offsets (1): {uOffsets[0].ToString("X")} {uOffsets[1].ToString("X")} {uOffsets[2].ToString("X")} {uOffsets[3].ToString("X")}");
                return;
            }
            uOffsets[0] = uOffsets[0] + ReadUInt(uOffsets[0] + 0x02) + 0x06;
            uOffsets[1] = uOffsets[1] + 0x33 + ReadUInt(uOffsets[1] + 0x36) + 0x07;
            uOffsets[2] = uOffsets[2] + 0x0F + ReadUInt(uOffsets[2] + 0x11) + 0x06;
            uOffsets[3] = uOffsets[3] + ReadUInt(uOffsets[3] + 0x03) + 0x07;
            if (uOffsets.Any(x => x <= 0x140004000 || x >= 0x150000000)) {
                Utils.Log(3, $"Failed to locate offsets (2): {uOffsets[0].ToString("X")} {uOffsets[1].ToString("X")} {uOffsets[2].ToString("X")} {uOffsets[3].ToString("X")}");
                return;
            }
            offsets = uOffsets;
            Ready = true;
        }

        // [[[[offset3] + 0x258] + 0x10] + 0xBFEC]
        public static int GetPlayerSeatId() {
            uint ptr = ReadUInt(ReadUInt(offsets[3]) + 0x258);
            int seatId = -1;
            if (ptr > 0x1000) {
                ptr = ReadUInt(ptr + 0x10);
                if (ptr != 0) {
                    seatId = (int)ReadUInt(ptr + 0xBFEC);
                }
            }
            return seatId;
        }

        // [[offset3] + 0x54A45 + 0x21 * seatId]
        public static string[] GetPlayerNames() {
            string[] names = new string[4];
            byte[] buffer = new byte[40];
            uint ptr = ReadUInt(offsets[3]) + 0x54A45;
            for (int i = 0; i < 4; ++i) {
                Array.Resize(ref buffer, 40);
                ReadProcessMemory(game.Handle, (IntPtr)(long)(ptr + 0x21 * i), buffer);
                int size = Array.FindIndex(buffer, x => x == 0);
                if (size <= 0) {
                    names[i] = "";
                } else {
                    Array.Resize(ref buffer, size);
                    names[i] = Encoding.UTF8.GetString(buffer);
                }
            }
            return names;
        }

        // [[([[offset1] + 0x66B0 + 0x04 * seatId] & [offset0]) * 0x58 + 0x48 + [offset2] + 0x48] + 0x48]
        public static int[] GetPlayerDamages() {
            int[] damages = new int[4];
            ulong rcx = ReadULong(offsets[2]);
            ulong bPtr = ReadULong(offsets[1]) + 0x66B0;
            for (int i = 0; i < 4; ++i) {
                uint edx = ReadUInt(bPtr + 0x04 * (ulong)i);
                ulong ptr = ((ReadUInt(offsets[0]) & edx) * 0x58) + 0x48 + rcx;
                if (ptr != 0) {
                    ptr = ReadULong(ptr + 0x48);
                    if (ptr != 0) {
                        byte[] buffer = new byte[4];
                        bool flag = ReadProcessMemory(game.Handle, (IntPtr)(long)(ptr + 0x48), buffer);
                        int result = buffer[0] + (buffer[1] << 8) + (buffer[2] << 16) + (buffer[3] << 24);
                        if (flag & result >= 0 && result <= 0xFFFFF) {
                            damages[i] = result;
                        }
                    }
                }
            }
            return damages;
        }

        // BROKEN: [[[[[base + 0x8A8] + 0x3B0] + 0x18] + 0x58] + 0x64]
        private static float[] ReadMonsterHealthInfo(ulong lpAddress) {
            ulong ptr = ReadUInt(lpAddress);
            if (ptr == 0) {
                return new float[] { 0, 0 };
            }
            ptr = ptr + 0x8A8;
            ptr = ReadUInt(ptr) + 0x3B0;
            ptr = ReadUInt(ptr) + 0x18;
            ptr = ReadUInt(ptr) + 0x58;
            ptr = ReadUInt(ptr) + 0x60;
            float mhp = ReadFloat(ptr);
            float hp = ReadFloat(ptr + 0x04);
            float[] info = { mhp, hp };
            return info;
        }

        // BROKEN: [[base + 0x3B220D8] + 0x168]
        public static float[] GetTargetMonsterInfo() {
            ulong ptr = (ulong)(long)game.MainModule.BaseAddress + 0x3B220D8;
            ptr = ReadUInt(ptr) + 0x168;
            return ReadMonsterHealthInfo(ptr);
        }

        // BROKEN: [[base + 0x3B22410] + 0xD18 - i * 0x60]
        public static float[] GetIthMonsterInfo(ulong i) {
            ulong ptr = (ulong)(long)game.MainModule.BaseAddress + 0x3B22410;
            ptr = ReadUInt(ptr) + 0xD18 - i * 0x60;
            return ReadMonsterHealthInfo(ptr);
        }

        private struct MEMORY_BASIC_INFORMATION64 {
            public ulong BaseAddress;
            public ulong AllocationBase;
            public int AllocationProtect;
            public int __alignment1;
            public ulong RegionSize;
            public int State;
            public int Protect;
            public int Type;
            public int __alignment2;
        }

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength);
        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        private static bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer) {
            int lpNumberOfBytesRead = 0;
            return ReadProcessMemory(hProcess, lpBaseAddress, lpBuffer, lpBuffer.Length, ref lpNumberOfBytesRead);
        }

        private static uint ReadUInt(ulong lpAddress) {
            byte[] num = new byte[4];
            ReadProcessMemory(game.Handle, (IntPtr)(long)lpAddress, num);
            return BitConverter.ToUInt32(num, 0);
        }

        private static ulong ReadULong(ulong lpAddress) {
            byte[] num = new byte[8];
            ReadProcessMemory(game.Handle, (IntPtr)(long)lpAddress, num);
            return BitConverter.ToUInt64(num, 0);
        }

        private static float ReadFloat(ulong lpAddress) {
            byte[] num = new byte[4];
            ReadProcessMemory(game.Handle, (IntPtr)(long)lpAddress, num);
            return BitConverter.ToSingle(num, 0);
        }

        private static ulong[] FindPatterns() {
            ulong[] uOffsets = new ulong[4];
            ulong lpAddress = 0x140004000;
            bool[] found = new bool[4] { false, false, false, false };
            do {
                if (VirtualQueryEx(game.Handle, (IntPtr)(long)lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64))) > 0 && lpBuffer.RegionSize != 0) {
                    byte[] buffer = new byte[lpBuffer.RegionSize];
                    ReadProcessMemory(game.Handle, (IntPtr)(long)lpBuffer.BaseAddress, buffer);
                    for (int i = 0; i < 4; ++i) {
                        if (found[i]) {
                            continue;
                        }
                        if (uOffsets[i] == 0) {
                            int index = FindSubArrayIndex(buffer, patterns[i]);
                            if (index >= 0) {
                                uOffsets[i] = lpBuffer.BaseAddress + (ulong)index;
                                found[i] = true;
                            }
                        }
                    }
                }
            } while (lpAddress < 0x145000000 && found.Any(x => !x));
            return uOffsets;
        }

        private static int FindSubArrayIndex(byte[] buffer, byte?[] pattern) {
            if (buffer.Length < pattern.Length) {
                return -1;
            }
            for (int i = 0; i < buffer.Length - pattern.Length + 1; ++i) {
                bool found = true;
                for (int j = 0; j < pattern.Length; ++j) {
                    if (pattern[j].HasValue && pattern[j] != buffer[i + j]) {
                        found = false;
                        break;
                    }
                }
                if (found) {
                    return i;
                }
            }
            return -1;
        }

        private static readonly List<byte?[]> patterns = new List<byte?[]> {
            new byte?[] {
                0x8B, 0x0D,
                null, null, null, null,
                0x23, 0xCA, 0x81, 0xF9, 0x00, 0x01, 0x00, 0x00, 0x73, 0x2F, 0x0F, 0xB7,
                null, null, null, null, null,
                0xC1, 0xEA, 0x10
            },
            new byte?[] {
                0x48, 0x89, 0x74, 0x24, 0x38, 0x8B, 0x70, 0x18, 0x48, 0x8B,
                null, null, null, null, null,
                0x89, 0x88, 0x0C, 0x05, 0x00, 0x00, 0x48, 0x8B,
                null, null, null, null, null,
                0x89, 0x90, 0x10, 0x05, 0x00, 0x00, 0x48, 0x8B,
                null, null, null, null, null,
                0x89, 0x98, 0x14, 0x05, 0x00, 0x00, 0x85, 0xDB, 0x7E,
                null,
                0x48, 0x8B,
                null, null, null, null, null
            },
            new byte?[] {
                0xB2, 0xAC, 0x0B, 0x00, 0x00, 0x49, 0x8B, 0xD9, 0x8B, 0x51, 0x54, 0x49, 0x8B, 0xF8, 0x48, 0x8B, 0x0D,
                null, null, null, null
            },
            new byte?[] {
                0x48, 0x8B, 0x0D,
                null, null, null, null,
                0x48, 0x8D, 0x54, 0x24, 0x38, 0xC6, 0x44, 0x24, 0x20, 0x00, 0x4D, 0x8B, 0x40, 0x08, 0xE8,
                null, null, null, null,
                0x48, 0x8B, 0x5C, 0x24, 0x60, 0x48, 0x83, 0xC4, 0x50, 0x5F, 0xC3
            }
        };

    }
}
