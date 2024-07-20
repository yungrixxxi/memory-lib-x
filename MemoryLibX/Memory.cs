﻿using System;
using System.Diagnostics;
using System.Text;

/*
 * Copyright (c) Pavel Kalaš, [20/07/2024]
 * 
 * This software is provided "as-is" for personal use only.
 * Redistribution or commercial use is strictly prohibited.
 * 
 * You are free to use, modify, and experiment with this software for personal purposes.
 * However, you may not distribute, share, or sell the software or any derivative works.
 * 
 * This notice must be included in all copies or substantial portions of the software.
 */

namespace MemoryLibX
{
    public class Memory
    {
        /// <summary>
        /// Grants read access to the process's virtual memory.
        /// </summary>
        const int PROCESS_VM_READ = 0x0010;

        /// <summary>
        /// Grants access to query information about the process, such as its exit code and priority class.
        /// </summary>
        const int PROCESS_QUERY_INFORMATION = 0x0400;

        /// <summary>
        /// Grants all possible access rights to the process.
        /// </summary>
        const int PROCESS_ALL_ACCESS = 0x001F0FFF;

        /// <summary>
        /// Enables execute, read, and write access to a region of the process's virtual memory.
        /// </summary>
        const int PAGE_EXECUTE_READWRITE = 0x40;

        /// <summary>
        /// Handle of founded process.
        /// </summary>
        private Process processHandle;

        /// <summary>
        /// Process ID
        /// </summary>
        private int processId;

        public Memory(int processId)
        {
            this.processId = processId;
        }

        /// <summary>
        /// Bind and find process by process ID.
        /// </summary>
        public void BindProcess()
        {
            // checks if is already binded.
            if (processHandle != null)
            {
                return;
            }

            // get list of all processes
            foreach (var processInstance in Process.GetProcesses())
            {
                // check if the taken process ID using constructor has exists.
                if (processInstance.Id == processId)
                {
                    // set handle (bind)
                    processHandle = processInstance;

                    // break to speed up processing (it's just unecessarry iteration..)
                    break;
                }
            }
        }

        /// <summary>
        /// Checks if current process is running.
        /// </summary>
        /// <returns>Boolean, result if is process running.</returns>
        public bool ProcessIsRunning()
        {
            try
            {
                // checks if the process "has exited" (was closed..)
                return !Process.GetProcessById(processId).HasExited;
            }
            catch
            {
                // when error occurs, return as FALSE (not found)
                return false;
            }
        }

        /// <summary>
        /// Returns a module base from EXE or DLL.
        /// </summary>
        /// <param name="moduleName">name of EXE or DLL</param>
        /// <returns>Pointer of DLL or EXE module</returns>
        private IntPtr GetModuleBaseAddress(string moduleName)
        {
            foreach (ProcessModule module in processHandle.Modules)
            {
                if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return module.BaseAddress;
                }
            }

            return IntPtr.Zero;
        }


        /// <summary>
        /// Writes a string to specified address and module in memory.
        /// </summary>
        /// <param name="moduleName">DLL or EXE module of base pointing to</param>
        /// <param name="address">Address in memory</param>
        /// <param name="text">String you want to write</param>
        /// <param name="size">Size to allocate</param>
        /// <returns>Returns boolean if was writing successfull</returns>
        public bool WriteStringToAddress(string moduleName, int address, string text, int size)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_ALL_ACCESS, false, processId);

            if (hProcess == IntPtr.Zero)
            {
                return false;
            }

            IntPtr moduleBase = IntPtr.Zero;

            foreach (ProcessModule module in processHandle.Modules)
            {
                if (module.ModuleName == moduleName)
                {
                    moduleBase = module.BaseAddress;
                    break;
                }
            }

            if (moduleBase == IntPtr.Zero)
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            IntPtr finalAddress = IntPtr.Add(moduleBase, address);

            byte[] buffer = new byte[size];
            byte[] textBytes = Encoding.ASCII.GetBytes(text);

            Array.Copy(textBytes, buffer, textBytes.Length);

            if (!DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            bool result = DllImports.WriteProcessMemory(hProcess, finalAddress, buffer, (uint)buffer.Length, out _);

            DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, oldProtect, out _);

            DllImports.CloseHandle(hProcess);

            return result;
        }

        /// <summary>
        /// Writes a string to specified address in memory.
        /// </summary>
        /// <param name="address">Address in memory</param>
        /// <param name="text">String you want to write</param>
        /// <param name="size">Size to allocate</param>
        /// <returns>Returns boolean if was writing successfull</returns>
        public bool WriteStringToAddress(int address, string text, int size)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            IntPtr finalAddress = new IntPtr(address);

            if (hProcess == IntPtr.Zero)
            {
                return false;
            }

            byte[] buffer = new byte[size];
            byte[] textBytes = Encoding.ASCII.GetBytes(text);

            Array.Copy(textBytes, buffer, textBytes.Length);

            if (!DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            bool result = DllImports.WriteProcessMemory(hProcess, finalAddress, buffer, (uint)buffer.Length, out _);

            DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, oldProtect, out _);
            DllImports.CloseHandle(hProcess);

            return result;
        }

        /// <summary>
        /// Reads a string from specified address and module in memory.
        /// </summary>
        /// <param name="address">Address in memory</param>
        /// <param name="module">DLL or EXE module of base pointing to</param>
        /// <returns>Readed string from memory</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string ReadStringFromMemory(int address, string module)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processHandle.Id);

            if (hProcess == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                IntPtr moduleBaseAddress = GetModuleBaseAddress(module);
                IntPtr finalAddress = IntPtr.Add(moduleBaseAddress, address);

                const int stringLength = 256;
                byte[] buffer = new byte[stringLength];

                if (!DllImports.ReadProcessMemory(hProcess, finalAddress, buffer, buffer.Length, out int bytesRead) || bytesRead == 0)
                {
                    throw new InvalidOperationException("Failed to read memory.");
                }

                return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
            }
            finally
            {
                DllImports.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Reads a string from specified address in memory.
        /// </summary>
        /// <param name="address">Address in memory</param>
        /// <returns>Readed string from memory</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public string ReadStringFromMemory(int address)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processHandle.Id);

            if (hProcess == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                const int stringLength = 256;
                byte[] buffer = new byte[stringLength];

                if (!DllImports.ReadProcessMemory(hProcess, new IntPtr(address), buffer, buffer.Length, out int bytesRead) || bytesRead == 0)
                {
                    return null;
                }

                return Encoding.UTF8.GetString(buffer, 0, bytesRead).TrimEnd('\0');
            }
            finally
            {
                DllImports.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Reads the integer from the memory address and module in memory.
        /// </summary>
        /// <param name="address">Address in memory</param>
        /// <param name="module">DLL or EXE module of base pointing</param>
        /// <returns>Readed integer from memory</returns>
        public int ReadIntegerFromMemory(int address, string module)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processHandle.Id);

            if (hProcess == IntPtr.Zero)
            {
                return -1;
            }

            try
            {
                IntPtr moduleBaseAddress = GetModuleBaseAddress(module);
                IntPtr finalAddress = IntPtr.Add(moduleBaseAddress, address);

                byte[] buffer = new byte[sizeof(int)];

                if (!DllImports.ReadProcessMemory(hProcess, finalAddress, buffer, buffer.Length, out int bytesRead) || bytesRead == 0)
                {
                    return -1;
                }

                return BitConverter.ToInt32(buffer, 0);
            }
            finally
            {
                DllImports.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Reads the integer from the memory address in memory.
        /// </summary>
        /// <param name="address">Address in memory</param>
        /// <returns>Readed integer from memory</returns>
        public int ReadIntegerFromMemory(int address)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processHandle.Id);

            if (hProcess == IntPtr.Zero)
            {
                return -1;
            }

            try
            {
                byte[] buffer = new byte[sizeof(int)];

                if (!DllImports.ReadProcessMemory(hProcess, new IntPtr(address), buffer, buffer.Length, out int bytesRead) || bytesRead == 0)
                {
                    return -1;
                }

                return BitConverter.ToInt32(buffer, 0);
            }
            finally
            {
                DllImports.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Writes a integer to address and module in memory.
        /// </summary>
        /// <param name="moduleName">EXE or DLL module base of pointing</param>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool WriteIntegerToMemory(string moduleName, int address, int value)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_ALL_ACCESS, false, processId);

            if (hProcess == IntPtr.Zero)
            {
                return false;
            }

            Process process = Process.GetProcessById(processId);
            IntPtr moduleBase = IntPtr.Zero;
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName == moduleName)
                {
                    moduleBase = module.BaseAddress;
                    break;
                }
            }

            if (moduleBase == IntPtr.Zero)
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            IntPtr finalAddress = IntPtr.Add(moduleBase, address);

            byte[] buffer = BitConverter.GetBytes(value);

            if (!DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            bool result = DllImports.WriteProcessMemory(hProcess, finalAddress, buffer, (uint)buffer.Length, out _);

            DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, oldProtect, out _);

            DllImports.CloseHandle(hProcess);

            return result;
        }

        /// <summary>
        /// Writes a integer to address in memory.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool WriteIntegerToMemory(int address, int value)
        {
            IntPtr hProcess = DllImports.OpenProcess(PROCESS_ALL_ACCESS, false, processId);

            if (hProcess == IntPtr.Zero)
            {
                return false;
            }

            IntPtr finalAddress = new IntPtr(address);

            byte[] buffer = BitConverter.GetBytes(value);

            if (!DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                DllImports.CloseHandle(hProcess);
                return false;
            }

            bool result = DllImports.WriteProcessMemory(hProcess, finalAddress, buffer, (uint)buffer.Length, out _);

            DllImports.VirtualProtectEx(hProcess, finalAddress, (uint)buffer.Length, oldProtect, out _);

            DllImports.CloseHandle(hProcess);

            return result;
        }
    }
}