using System.Runtime.InteropServices;

namespace Hi3Helper.Screen
{
    public class ScreenInterop
    {
        // Reference:
        // http://www.codeproject.com/KB/dotnet/changing-display-settings.aspx
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            // You can define the following constant
            // but OUTSIDE the structure because you know
            // that size and layout of the structure is very important
            // CCHDEVICENAME = 32 = 0x50
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            // In addition you can define the last character array
            // as following:
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            //public Char[] dmDeviceName;

            // After the 32-bytes array
            [MarshalAs(UnmanagedType.U2)]
            public ushort dmSpecVersion;

            [MarshalAs(UnmanagedType.U2)]
            public ushort dmDriverVersion;

            [MarshalAs(UnmanagedType.U2)]
            public ushort dmSize;

            [MarshalAs(UnmanagedType.U2)]
            public ushort dmDriverExtra;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmFields;

            public POINTL dmPosition;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmDisplayOrientation;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmDisplayFixedOutput;

            [MarshalAs(UnmanagedType.I2)]
            public short dmColor;

            [MarshalAs(UnmanagedType.I2)]
            public short dmDuplex;

            [MarshalAs(UnmanagedType.I2)]
            public short dmYResolution;

            [MarshalAs(UnmanagedType.I2)]
            public short dmTTOption;

            [MarshalAs(UnmanagedType.I2)]
            public short dmCollate;

            // CCHDEVICENAME = 32 = 0x50
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            // Also can be defined as
            //[MarshalAs(UnmanagedType.ByValArray, 
            //    SizeConst = 32, ArraySubType = UnmanagedType.U1)]
            //public Byte[] dmFormName;

            [MarshalAs(UnmanagedType.U2)]
            public ushort dmLogPixels;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmBitsPerPel;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmPelsWidth;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmPelsHeight;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmDisplayFlags;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmDisplayFrequency;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmICMMethod;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmICMIntent;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmMediaType;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmDitherType;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmReserved1;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmReserved2;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmPanningWidth;

            [MarshalAs(UnmanagedType.U4)]
            public uint dmPanningHeight;

            /// <summary>
            /// Initializes the structure variables.
            /// </summary>
            public void Initialize()
            {
                dmDeviceName = new string(new char[32]);
                dmFormName = new string(new char[32]);
                dmSize = (ushort)Marshal.SizeOf(this);
            }
        }

        // 8-bytes structure
        [StructLayout(LayoutKind.Sequential)]
        public struct POINTL
        {
            public int x;
            public int y;
        }

        [DllImport("User32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplaySettings(
            [param: MarshalAs(UnmanagedType.LPTStr)]
            string lpszDeviceName,  // display device
            [param: MarshalAs(UnmanagedType.U4)]
            int iModeNum,         // graphics mode
            [In, Out]
            ref DEVMODE lpDevMode       // graphics mode settings
            );

        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int DMDO_DEFAULT = 0;
        public const int DMDO_90 = 1;
        public const int DMDO_180 = 2;
        public const int DMDO_270 = 3;


        [DllImport("User32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern int ChangeDisplaySettings(
            [In, Out]
            ref DEVMODE lpDevMode,
            [param: MarshalAs(UnmanagedType.U4)]
            uint dwflags);



        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern uint FormatMessage(
            [param: MarshalAs(UnmanagedType.U4)]
            uint dwFlags,
            [param: MarshalAs(UnmanagedType.U4)]
            uint lpSource,
            [param: MarshalAs(UnmanagedType.U4)]
            uint dwMessageId,
            [param: MarshalAs(UnmanagedType.U4)]
            uint dwLanguageId,
            [param: MarshalAs(UnmanagedType.LPTStr)]
            out string lpBuffer,
            [param: MarshalAs(UnmanagedType.U4)]
            uint nSize,
            [param: MarshalAs(UnmanagedType.U4)]
            uint Arguments);

        public const uint FORMAT_MESSAGE_FROM_HMODULE = 0x800;

        public const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100;
        public const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x200;
        public const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x1000;
        public const uint FORMAT_MESSAGE_FLAGS = FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_IGNORE_INSERTS | FORMAT_MESSAGE_FROM_SYSTEM;
    }
}