using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PortalNes.UnityBridge
{
    internal static class WindowsRomFilePicker
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential)]
        private struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public IntPtr filter;
            public IntPtr customFilter;
            public int maxCustomFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public IntPtr initialDirectory;
            public IntPtr title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public IntPtr defaultExtension;
            public IntPtr customData;
            public IntPtr hook;
            public IntPtr templateName;
            public IntPtr reserved;
            public int reserved2;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName fileName);

        public static string Open(string currentPath)
            => OpenFile(currentPath, "Choose iNES ROM",
                "NES ROM (*.nes)\0*.nes\0All Files (*.*)\0*.*\0\0", "nes");

        private static string OpenFile(string currentPath, string dialogTitle, string fileFilter,
            string defaultExtension)
        {
            const int fileMustExist = 0x00001000;
            const int pathMustExist = 0x00000800;
            const int noChangeDirectory = 0x00000008;
            const int bufferCharacters = 4096;
            IntPtr file = Marshal.AllocHGlobal(bufferCharacters * sizeof(char));
            IntPtr filter = Marshal.StringToHGlobalUni(fileFilter);
            IntPtr title = Marshal.StringToHGlobalUni(dialogTitle);
            IntPtr extension = Marshal.StringToHGlobalUni(defaultExtension);
            string directory = string.IsNullOrWhiteSpace(currentPath) ? null : Path.GetDirectoryName(currentPath);
            IntPtr initialDirectory = string.IsNullOrWhiteSpace(directory)
                ? IntPtr.Zero : Marshal.StringToHGlobalUni(directory);
            try
            {
                // The first UTF-16 character must be NUL for an initially empty filename.
                Marshal.WriteInt16(file, 0);
                var data = new OpenFileName
                {
                    structSize = Marshal.SizeOf<OpenFileName>(),
                    filter = filter,
                    filterIndex = 1,
                    file = file,
                    maxFile = bufferCharacters,
                    title = title,
                    defaultExtension = extension,
                    initialDirectory = initialDirectory,
                    flags = fileMustExist | pathMustExist | noChangeDirectory
                };
                return GetOpenFileName(ref data) ? Marshal.PtrToStringUni(file) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(file);
                Marshal.FreeHGlobal(filter);
                Marshal.FreeHGlobal(title);
                Marshal.FreeHGlobal(extension);
                if (initialDirectory != IntPtr.Zero) Marshal.FreeHGlobal(initialDirectory);
            }
        }
#else
        public static string Open(string currentPath) => null;
#endif
    }
}
