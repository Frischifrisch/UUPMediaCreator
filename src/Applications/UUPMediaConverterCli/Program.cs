﻿/*
 * Copyright (c) Gustave Monce and Contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using UUPMediaCreator.InterCommunication;

namespace UUPMediaConverterCli
{
    internal static class Program
    {
        public static string GetExecutableDirectory()
        {
            string fileName = Process.GetCurrentProcess().MainModule.FileName;
            return fileName.Contains(Path.DirectorySeparatorChar) ? string.Join(Path.DirectorySeparatorChar, fileName.Split(Path.DirectorySeparatorChar).Reverse().Skip(1).Reverse()) : "";
        }

        public static string GetParentExecutableDirectory()
        {
            string runningDirectory = GetExecutableDirectory();
            return runningDirectory.Contains(Path.DirectorySeparatorChar) ? string.Join(Path.DirectorySeparatorChar, runningDirectory.Split(Path.DirectorySeparatorChar).Reverse().Skip(1).Reverse()) : "";
        }

        private static void Main(string[] args)
        {
            Log($"UUPMediaConverterCli {Assembly.GetExecutingAssembly().GetName().Version} - Converts an UUP file set to an usable ISO file");
            Log("Copyright (c) Gustave Monce and Contributors");
            Log("https://github.com/gus33000/UUPMediaCreator");
            Log("");
            Log("This program comes with ABSOLUTELY NO WARRANTY.");
            Log("This is free software, and you are welcome to redistribute it under certain conditions.");
            Log("");
            Log("This software contains work derived from libmspack licensed under the LGPL-2.1 license.");
            Log("(C) 2003-2004 Stuart Caie.");
            Log("(C) 2011 Ali Scissons.");
            Log("");

            if (args.Length < 3)
            {
                Log("Usage: UUPMediaConverterCli.exe <UUP File set path> <Destination ISO file> <Language Code> [Edition]");
                return;
            }

            string UUPPath = Path.GetFullPath(args[0]);
            string DestinationISO = args[1];
            string LanguageCode = args[2];
            string Edition = null;
            if (args.Length > 3)
            {
                Edition = args[3];
            }

            if (GetOperatingSystem() == OSPlatform.OSX)
            {
                Log("WARNING: For successful ISO creation, please install cdrtools via brew", LoggingLevel.Warning);
            }
            else if (GetOperatingSystem() == OSPlatform.Linux)
            {
                Log("WARNING: For successful ISO creation, please install genisoimage", LoggingLevel.Warning);
            }

            Log("WARNING: This tool does NOT currently integrate updates into the finished media file. Any UUP set with updates (KBXXXXX).MSU/.CAB will not have the update integrated.", severity: LoggingLevel.Warning);
            if (!IsAdministrator())
            {
                Log("WARNING: This tool is NOT currently running under Windows as administrator. The resulting image will be less clean/proper compared to Microsoft original.", severity: LoggingLevel.Warning);

                if (string.IsNullOrEmpty(Edition))
                {
                    Log("WARNING: You are attempting to create an ISO media with potentially all editions available. Due to the tool not running under Windows as administrator, this request might not be fullfilled.", severity: LoggingLevel.Warning);
                }
            }
            else
            {
                string parentDirectory = GetParentExecutableDirectory();
                string toolpath = Path.Combine(parentDirectory, "UUPMediaConverterDismBroker", "UUPMediaConverterDismBroker.exe");

                if (!File.Exists(toolpath))
                {
                    parentDirectory = GetExecutableDirectory();
                    toolpath = Path.Combine(parentDirectory, "UUPMediaConverterDismBroker", "UUPMediaConverterDismBroker.exe");
                }

                if (!File.Exists(toolpath))
                {
                    parentDirectory = GetExecutableDirectory();
                    toolpath = Path.Combine(parentDirectory, "UUPMediaConverterDismBroker.exe");
                }

                if (!File.Exists(toolpath))
                {
                    Log("ERROR: Could not find: " + toolpath, severity: LoggingLevel.Error);
                    return;
                }
            }

            int prevperc = -1;
            Common.ProcessPhase prevphase = Common.ProcessPhase.ReadingMetadata;
            string prevop = "";

            void callback(Common.ProcessPhase phase, bool IsIndeterminate, int ProgressInPercentage, string SubOperation)
            {
                if (phase == prevphase && prevperc == ProgressInPercentage && SubOperation == prevop)
                {
                    return;
                }

                prevphase = phase;
                prevop = SubOperation;
                prevperc = ProgressInPercentage;

                if (phase == Common.ProcessPhase.Error)
                {
                    Log("An error occured!", severity: LoggingLevel.Error);
                    Log(SubOperation, severity: LoggingLevel.Error);
                    if (Debugger.IsAttached)
                    {
                        Console.ReadLine();
                    }

                    return;
                }
                string progress = IsIndeterminate ? "" : $" [Progress: {ProgressInPercentage}%]";
                Log($"[{phase}]{progress} {SubOperation}");
            }

            try
            {
                MediaCreationLib.MediaCreator.CreateISOMedia(
                    DestinationISO,
                    UUPPath,
                    Edition,
                    LanguageCode,
                    false,
                    Common.CompressionType.LZX,
                    callback);
            }
            catch (Exception ex)
            {
                Log("An error occured!", severity: LoggingLevel.Error);
                while (ex != null)
                {
                    Log(ex.Message, LoggingLevel.Error);
                    Log(ex.StackTrace, LoggingLevel.Error);
                    ex = ex.InnerException;
                }
                if (Debugger.IsAttached)
                {
                    Console.ReadLine();
                }
            }
        }

        public enum LoggingLevel
        {
            Information,
            Warning,
            Error
        }

        private static readonly object lockObj = new();

        public static void Log(string message, LoggingLevel severity = LoggingLevel.Information, bool returnline = true)
        {
            lock (lockObj)
            {
                if (message?.Length == 0)
                {
                    Console.WriteLine();
                    return;
                }

                string msg = "";

                switch (severity)
                {
                    case LoggingLevel.Warning:
                        msg = "  Warning  ";
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;

                    case LoggingLevel.Error:
                        msg = "   Error   ";
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case LoggingLevel.Information:
                        msg = "Information";
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }

                if (returnline)
                {
                    Console.WriteLine(DateTime.Now.ToString("'['HH':'mm':'ss']'") + "[" + msg + "] " + message);
                }
                else
                {
                    Console.Write("\r" + DateTime.Now.ToString("'['HH':'mm':'ss']'") + "[" + msg + "] " + message);
                }

                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        private static bool IsAdministrator()
        {
            if (GetOperatingSystem() == OSPlatform.Windows)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
#pragma warning restore CA1416 // Validate platform compatibility
            }
            else
            {
                return false;
            }
        }

        public static OSPlatform GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OSPlatform.OSX;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OSPlatform.Linux;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OSPlatform.Windows;
            }

            return RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)
                ? OSPlatform.FreeBSD
                : throw new Exception("Cannot determine operating system!");
        }
    }
}