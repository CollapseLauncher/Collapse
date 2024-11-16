using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CollapseLauncher.ShellLinkCOM
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariant
    {
        public short variantType;
        public short Reserved1, Reserved2, Reserved3;
        public IntPtr pointerValue;

        public static PropVariant FromString(string str)
        {
            var pv = new PropVariant()
            {
                variantType = 31,  // VT_LPWSTR
                pointerValue = Marshal.StringToCoTaskMemUni(str),
            };

            return pv;
        }

        public static PropVariant FromGuid(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            var pv = new PropVariant()
            {
                variantType = 72,  // VT_CLSID
                pointerValue = Marshal.AllocCoTaskMem(bytes.Length),
            };
            Marshal.Copy(bytes, 0, pv.pointerValue, bytes.Length);

            return pv;
        }

        /// <summary>
        /// Called to properly clean up the memory referenced by a PropVariant instance.
        /// </summary>
        [DllImport("ole32.dll")]
        private extern static int PropVariantClear(ref PropVariant pvar);

        /// <summary>
        /// Called to clear the PropVariant's referenced and local memory.
        /// </summary>
        /// <remarks>
        /// You must call Clear to avoid memory leaks.
        /// </remarks>
        public void Clear()
        {
            PropVariantClear(ref this);
        }
    }
}
