using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace KeyChange
{
    public static class SendInputs
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public INPUTUNION inputUnion;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            // Fields
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public int uMsg;
            public short wParamL;
            public short wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern int SendInput(int nInputs, [In] INPUT[] pInputs, int cbSize);

        public static int Send(string keys)
        {
            var nativekey = new INPUT[keys.Length / 2];
            int cc = 0; bool flag = false;
            for (int i = 0; i < keys.Length; )
            {
                if (keys[i] == ' ')
                    continue;
                if (keys[i] == '!')
                {
                    flag = true;
                    i++; continue;
                }
                nativekey[cc++] = new INPUT
                {
                    type = 1,
                    inputUnion = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (short)int.Parse(keys.Substring(i, 2), System.Globalization.NumberStyles.HexNumber),
                            dwFlags = flag ? 2 : 0
                        }
                    }
                };
                flag = false;
                i += 2;
            }
            int keysSent = SendInput(cc, nativekey, Marshal.SizeOf(typeof(INPUT)));
            return keysSent;
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        public static void Mouse(string keys)
        {

            for (int i = 0; i < keys.Length; i += 2)
            {
                var k=int.Parse(keys.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
                if (k == 0x80)
                {
                    var p = int.Parse(keys.Substring(i+2, 4), System.Globalization.NumberStyles.HexNumber);
                    i += 4;
                    if (p >= 32768) p = p-65536;
                    mouse_event(0x800, 0, 0, p, 0);
                }else
                mouse_event(k, 0, 0, 0, 0);
            }
        }

/* Original
static int SendKeys(params Keys[] keys) {
 var nativeKeys = keys.Select(
 k =new INPUT {
 type = 1, inputUnion = new INPUTUNION {
 ki = new KEYBDINPUT { wVk = (short) k }
 }
 }
 ).ToArray();
 int keysSent = NativeMethods.SendInput(nativeKeys.Length, nativeKeys,
 Marshal.SizeOf(typeof(INPUT)));
 if (keysSent == 0) throw new Win32Exception();
 return keysSent;
 }
 */
    }
}
