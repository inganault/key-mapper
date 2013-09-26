using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using SocketIOClient;
using System.Threading;
using System.Diagnostics;

namespace KeyChange
{
    public partial class Form1 : Form
    {
        /*[DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();*/
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        static extern bool PostMessage(int hWnd, uint Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        static extern int LoadKeyboardLayout(string pwszKLID, uint Flags);
        [DllImport("user32.dll")]
        static extern bool GetKeyboardLayoutName([Out] StringBuilder pwszKLID);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int flag);

        /*public const int NOMOD = 0x0000;
        public const int ALT = 0x0001;
        public const int CTRL = 0x0002;
        public const int SHIFT = 0x0004;
        public const int WIN = 0x0008;*/

        public const int WM_HOTKEY_MSG_ID = 0x0312;
        public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        public const int HWND_BROADCAST = 0xffff;
        public const uint KLF_ACTIVATE = 1;

        string fn = "config.txt";
        private bool AutoHide = false;

        public int HashCode(int modifier, int key)
        {
            return modifier ^ (int)key ^ this.Handle.ToInt32();
        }

        public void SetHotkey(int modifier, int key)
        {
            if (modifier == 255)
                return;
            RegisterHotKey(this.Handle, HashCode(modifier, key), modifier, (int)key);
        }
        public void UnSetHotkey(int modifier, int key)
        {
            if (modifier == 255)
                return;
            UnregisterHotKey(this.Handle, HashCode(modifier, key));
        }

        public Form1(string[] args)
        {
            InitializeComponent();
            if (args.Length > 0 && args[0] == "hide")
            {
                AutoHide = true;
                if (args.Length > 1)
                    fn = args[1].Trim('\"');
            }
            else
            {
                if (args.Length > 0)
                    fn = args[0].Trim('\"');
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY_MSG_ID)
            {
                if (!Config.id.ContainsKey(m.LParam.ToInt32()))
                    return; // Ignore unbounded websocket event

                // Hotkey
                Keys key = (Keys)((m.LParam.ToInt32()) >> 16);
                //MessageBox.Show(key.ToString());

                int lp = Config.id[m.LParam.ToInt32()];
                textBox1.Text = "Trying...";
                switch (Config.type[lp])
                {
                    case 0:
                        PostMessage(HWND_BROADCAST,
                            WM_INPUTLANGCHANGEREQUEST,
                            0,
                            LoadKeyboardLayout(Config.action[lp], KLF_ACTIVATE));
                        textBox1.Text = "Changed to " + Config.action[lp];
                        break;
                    case 1:
                        SendKeys.Send(Config.action[lp]);
                        textBox1.Text = "SendKeys " + Config.action[lp];
                        break;
                    case 2:
                        System.Diagnostics.Process.Start(Config.action[lp]);
                        textBox1.Text = "Executing " + Config.action[lp];
                        break;
                    case 3:
                        textBox1.Text = "SendInput " + Config.action[lp] + (SendInputs.Send(Config.action[lp]) != 0 ? "" : " Failed!");
                        break;
                    case 4:
                        SendInputs.Mouse(Config.action[lp]);
                        textBox1.Text = "SendMouse " + Config.action[lp];
                        break;
                }
                textBox1.Text += " (" + key.ToString() + ")";
            }
            base.WndProc(ref m);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Process current = Process.GetCurrentProcess();
            Process[] process = Process.GetProcessesByName(current.ProcessName);
            foreach (Process p in process)
                if (p.Id != current.Id)
                {
                    p.Kill();
                }
            button3_Click(null, null);
            sock = new MySock(s =>
            {
                button4.Text = s;
                return false;
            });
            sock.Pedal += (s, ee) =>
            {
                this.BeginInvoke(new Action(()=>{
                    Message m = new Message() { Msg = WM_HOTKEY_MSG_ID, LParam = new IntPtr((ee.value << 16) + 255) };
                    WndProc(ref m);
                }));
            };
            foreach (int i in Config.hotkey)
                SetHotkey(i & 0xFFFF, i >> 16);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            StringBuilder name = new StringBuilder();
            GetKeyboardLayoutName(name);
            textBox1.Text = name.ToString();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            if (AutoHide)
                ShowWindow(this.Handle, 0);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            int mod = (e.Shift?4:0) + (e.Alt?1:0) + (e.Control?2:0);
            textBox1.Text = (mod).ToString("X2") + (e.KeyValue).ToString("X2") + " (" + e.KeyData.ToString() + ")";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ShowWindow(this.Handle, 0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (int i in Config.hotkey)
                UnSetHotkey(i & 0xFFFF, i >> 16);
            Config.Clear();
            if (!Config.LoadConfig(fn))
            {
                textBox1.Text = "Config load failed.";
            }
            else
            {
                textBox1.Text = "Ready!";
            }
        }

        MySock sock;
        private void button4_Click(object sender, EventArgs e)
        {
            sock.Connect();
        }
    }
    public class IntEventArgs : EventArgs
    {
        public int value;
        public IntEventArgs(int value)
        {
            this.value = value;
        }
    }

    public class MySock
    {
        Client socket = new Client("http://127.0.0.1/");
        Func<string,bool> callback;

        public event EventHandler<IntEventArgs> Pedal;

        public MySock(Func<string, bool> cb)
        {
            callback = cb;
            if (Config.conf["NodeJS_autostart"].ToLower() == "true")
            {
                new Thread(() =>
                {
                    Thread.Sleep(1000);
                    this.Connect();
                }).Start();
            }
        }

        public void Connect()
        {
            if (Retrying)
                return;
            if (socket.IsConnected)
            {
                socket.Close();
                return;
            }
            try
            {
                Retrying = true;
                socket = new Client(Config.conf["NodeJS_URL"]);
                socket.Opened += SocketEV;
                //socket.Message += SocketMessage;
                socket.SocketConnectionClosed += SocketEV;
                socket.Error += SocketEV;
                socket.On("pressure", (data) =>
                {
                    Pedal(this, new IntEventArgs(data.Json.GetFirstArgAs<int>()));
                });

                socket.Connect();
            }
            catch (Exception)
            {
                SocketEV(this, new EventArgs());
            }
        }
        bool Retrying = false;
        private void SocketEV(object sender, EventArgs e)
        {
            Retrying = false;
            callback(socket.IsConnected ? "WS" : "X");
            if (!socket.IsConnected && Config.conf["NodeJS_retry"] != "0")
            {
                new Thread(() =>
                {
                    Thread.Sleep(int.Parse(Config.conf["NodeJS_retry"])*1000);
                    this.Connect();
                }).Start();
            }
        }
    }

    public static class Config
    {
        public static Dictionary<string, string> conf = new Dictionary<string, string>();

        public static Dictionary<int, int> id = new Dictionary<int, int>();
        public static List<string> action = new List<string>();
        public static List<int> type = new List<int>();
        public static List<int> hotkey = new List<int>();
        public static void Clear()
        {
            id.Clear();
            action.Clear();
            type.Clear();
            hotkey.Clear();
            conf.Clear();
        }
        public static bool LoadConfig(string fn)
        {
            StreamReader fp=null;
            try
            {
                fp = new StreamReader(fn);
            }
            catch (Exception)
            {
                MessageBox.Show("Cannot read "+fn);
                return false;
            }
            while (!fp.EndOfStream)
            {
                string line = fp.ReadLine().Trim();
                if (line.StartsWith("#") || line.StartsWith("[") || line=="")
                    continue;

                if (line.StartsWith("!"))
                {
                    var cmdx = line.Substring(1).Split(new char[] { '=' }, 2);
                    conf.Add(cmdx[0].Trim(),cmdx[1].Trim());
                    continue;
                }
                string[] cmd = line.Split(new char[] { '=' }, 3);
                if (cmd.Length != 3)
                {
                    MessageBox.Show("Error in \"" + line + "\"");
                    return false;
                }
                string typ = cmd[0].Trim();
                string keys = cmd[1].Trim();
                string dest = cmd[2].Trim();
                int ty = -1,mod,key;
                try
                {
                    mod=int.Parse(keys.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                    key=int.Parse(keys.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                }
                catch (Exception)
                {
                    MessageBox.Show("Error in \"" + line + "\"\nParse error.");
                    return false;
                }
                //CMD
                switch (typ.ToUpper())
                {
                    case "LANG":
                        ty = 0;
                        break;
                    case "STRMAP":
                        ty = 1;
                        break;
                    case "EXEC":
                        ty = 2;
                        break;
                    case "KEYMAP":
                        ty = 3;
                        /*try
                        {
                            int.Parse(dest, System.Globalization.NumberStyles.HexNumber);
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Error in \"" + line + "\"\nParse error.");
                            return false;
                        }*/
                        break;
                    case "MOUSE":
                        ty = 4;
                        break;
                    default:
                        MessageBox.Show("Error in \"" + line + "\"\nUnknown type.");
                        return false;
                }
                key = (mod << 16) + key;
                action.Add(dest);
                type.Add(ty);
                id.Add(key,action.Count()-1);
                hotkey.Add(key);
            }
            try
            {
                fp.Close();
            }
            catch (Exception)
            {
                MessageBox.Show("Cannot close " + fn);
                return false;
            }
            return true;
        }
    }
}
