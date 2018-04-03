using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.IO.Pipes;
using Microsoft.Win32;
using System.Windows;

namespace DesktopToastsApp
{
    /// <summary>
    /// Summary description for SingleApp.
    /// </summary>
    public class SingleApplication
    {
        private const string MQ = "SIQ";
        private const string MQPath = ".\\Private$\\" + MQ;

        public SingleApplication()
        {

        }
        /// <summary>
        /// Imports 
        /// </summary>

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int IsIconic(IntPtr hWnd);

        /// <summary>
        /// GetCurrentInstanceWindowHandle
        /// </summary>
        /// <returns></returns>
        private static IntPtr GetCurrentInstanceWindowHandle()
        {
            IntPtr hWnd = IntPtr.Zero;
            Process process = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(process.ProcessName);
            foreach (Process _process in processes)
            {
                // Get the first instance that is not this instance, has the
                // same process name and was started from the same file name
                // and location. Also check that the process has a valid
                // window handle in this session to filter out other user's
                // processes.
                if (_process.Id != process.Id &&
                    _process.MainModule.FileName == process.MainModule.FileName &&
                    _process.MainWindowHandle != IntPtr.Zero)
                {
                    hWnd = _process.MainWindowHandle;
                    break;
                }
            }
            return hWnd;
        }
        /// <summary>
        /// SwitchToCurrentInstance
        /// </summary>
        public static void SwitchToCurrentInstance()
        {
            IntPtr hWnd = GetCurrentInstanceWindowHandle();
            if (hWnd != IntPtr.Zero)
            {
                // Restore window if minimised. Do not restore if already in
                // normal or maximised window state, since we don't want to
                // change the current state of the window.
                if (IsIconic(hWnd) != 0)
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }

                // Set foreground window.
                SetForegroundWindow(hWnd);
            }
        }

        /// <summary>
        /// Execute a form base application if another instance already running on
        /// the system activate previous one
        /// </summary>
        /// <param name="frmMain">main form</param>
        /// <returns>true if no previous instance is running</returns>
        public static bool Run(Action firstInstaceCode, EventHandler<string> activatedHandler, string activationData)
        {
            try
            {
                if (IsAlreadyRunning())
                {
                    using (var pipeClient = new NamedPipeClientStream("pipe"))
                    {
                        pipeClient.Connect(5000);
                        using (StreamWriter writer = new StreamWriter(pipeClient))
                        {
                            writer.AutoFlush = true;
                            writer.WriteLine(activationData);
                            pipeClient.WaitForPipeDrain();
                            Application.Current.Shutdown();
                            return false;
                        }
                    }
                }
            }
            catch { }

            //if (IsAlreadyRunning())
            //{
            //    //set focus on previously running app
            //    SwitchToCurrentInstance();
            //    return false;
            //}

            ConfigurePipeServer();

            Activated = activatedHandler;
            firstInstaceCode();
            Activated.Invoke(null, activationData);
            return true;
        }

        private static EventHandler<string> Activated;

        private static async void ConfigurePipeServer()
        {
            // https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-use-anonymous-pipes-for-local-interprocess-communication
            while (true)
            {
                using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("pipe"))
                {
                    //PipeServerHandle = pipeServer.GetClientHandleAsString();
                    //pipeServer.DisposeLocalCopyOfClientHandle();
                    await pipeServer.WaitForConnectionAsync();

                    using (StreamReader reader = new StreamReader(pipeServer))
                    {
                        string line = await reader.ReadLineAsync();
                        if (line != null)
                        {
                            Activated(null, line);
                        }
                    }

                    pipeServer.Close();
                }
            }
        }

        private async void RunMessageLoop()
        {
            while (true)
            {
                try
                {

                }
                catch { }

                await Task.Delay(50);
            }
        }

        /// <summary>
        /// for console base application
        /// </summary>
        /// <returns></returns>
        public static bool Run()
        {
            if (IsAlreadyRunning())
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// check if given exe alread running or not
        /// </summary>
        /// <returns>returns true if already running</returns>
        private static bool IsAlreadyRunning()
        {
            string strLoc = Assembly.GetExecutingAssembly().Location;
            FileSystemInfo fileInfo = new FileInfo(strLoc);
            string sExeName = fileInfo.Name;
            bool bCreatedNew;

            mutex = new Mutex(true, "Global\\" + sExeName, out bCreatedNew);
            if (bCreatedNew)
                mutex.ReleaseMutex();

            return !bCreatedNew;
        }

        static Mutex mutex;
        const int SW_RESTORE = 9;

        private static string PipeServerHandle
        {
            get
            {
                string answer = Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\DesktopToasts", "PipeServerHandle", null) as string;
                return answer;
            }
            set
            {
                var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\DesktopToasts");
                key.SetValue("PipeServerHandle", value);
            }
        }
    }
}
