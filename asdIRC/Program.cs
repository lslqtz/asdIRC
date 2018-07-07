using asdIRC.Helpers;
using IrcDotNet;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace asdIRC
{
    class Program
    {
        static AssemblyName CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName();
        static string ProgramName = CurrentAssemblyName.Name;
        static string ProgramVersion = GetFullVersion(CurrentAssemblyName.Version);
        static StandardIrcClient IRCClient;
        static Thread PingThread = new Thread(() => {
            while (true)
            {
                IRCClient.Ping();
                Thread.Sleep(2000);
            }
        });
        delegate bool ControlCtrlDelegate(int CtrlType);
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);
        static ControlCtrlDelegate cancelHandler = new ControlCtrlDelegate(HandlerRoutine);
        static bool HandlerRoutine(int CtrlType) {
            Console.WriteLine("Exiting...");
            switch (CtrlType)
            {
                case 0:
                case 2:
                    PingThread.Abort();
                    if (IRCClient != null)
                    {
                        IRCClient.SendRawMessage("QUIT");
                        IRCClient.Disconnect();
                    }
                    Environment.Exit(0);
                    break;
            }
            return false;
        }
        static string[] IRCChannels = { };
        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(cancelHandler, true);
            pConfigManager Config = new pConfigManager(@"IRC.cfg", true) { WriteOnChange = true };
            Config.LoadConfig(@"IRC.cfg", true);
            string IRCAddress = Config.GetValue("IRCAddress", "irc.ppy.sh");
            string IRCUsername = Config.GetValue("IRCUsername", "lslqtz");
            string IRCPassword = Config.GetValue("IRCPassword", "");
            int IRCConnectTimeout = Config.GetValue("IRCConnectTimeout", 10000);
            IRCChannels = Config.GetValue("Channels", "#osu").Split(',');
            Init();
            //Database.ConnectionString = Config.GetValue("ConnectionString", Database.ConnectionString);
            IrcUserRegistrationInfo IRCUserRegistrationInfo = new IrcUserRegistrationInfo() { NickName = IRCUsername, UserName = IRCUsername, Password = IRCPassword };
            IRCClient = new StandardIrcClient();
            IRCClient.Registered += OnRegistered;
            IRCClient.Connected += OnConnected;
            IRCClient.ProtocolError += OnProtocolError;
            IRCClient.RawMessageSent += OnSentRawMessage;
            IRCClient.RawMessageReceived += OnReceivedRawMessage;
            using (var connectedEvent = new ManualResetEventSlim(false))
            {
                IRCClient.Connected += (s, e) => connectedEvent.Set();
                IRCClient.Connect(IRCAddress, false, IRCUserRegistrationInfo);
                if (!connectedEvent.Wait(IRCConnectTimeout))
                {
                    Console.WriteLine("Connection timed out.");
                    Console.ReadKey(true);
                }
                while (true)
                {
                    string str = "";
                    ConsoleKeyInfo NewKey = Console.ReadKey(true);
                    while (NewKey.Key != ConsoleKey.Enter)
                    {
                        if (NewKey.Key == ConsoleKey.Backspace)
                        {
                            if (str.Length > 0)
                            {
                                str = str.Remove(str.Length - 1);
                            }
                        }
                        else
                        {
                            str += NewKey.KeyChar;
                        }
                        InitTitle(((!string.IsNullOrEmpty(str)) ? string.Format("Current Content: {0}", str) : null));
                        NewKey = Console.ReadKey(true);
                    }
                    InitTitle();
                    string[] NewLine = str.Split(':');
                    if (NewLine.Length > 1)
                    {
                        string target = NewLine[0];
                        string text = string.Join(" ", NewLine, 1, NewLine.Length - 1);
                        SendMessage(target, text);
                    }
                    str = "";
                }
            }
        }
        static void InitTitle(string OtherTitle = null)
        {
            Console.Title = ProgramName;
            if (OtherTitle != null)
            {
                Console.Title += "/" + OtherTitle;
            }
        }
        static void Init()
        {
            InitTitle();
            Console.WriteLine(string.Format("{0} Version: b{1}", ProgramName, ProgramVersion));
        }
        static string GetFullVersion(Version ver)
        {
            string FullVer = string.Format("{0:D4}{1:D2}{2:D2}.{3}", ver.Major, ver.Minor, ver.Build, ver.Revision);
#if DEBUG
            FullVer += "dev";
#endif
            return FullVer;
        }
        static void OnConnected(object sender, EventArgs e)
        {
        }
        static void OnRegistered(object sender, EventArgs e)
        {
            IrcClient IRCClient = (IrcClient)sender;
            IRCClient.LocalUser.MessageSent += OnSentMessage;
            IRCClient.LocalUser.NoticeSent += OnSentNotice;
            IRCClient.LocalUser.NoticeReceived += OnReceivedPrivateNotice;
            IRCClient.LocalUser.MessageReceived += OnReceivedPrivateMessage;
            PingThread.Start();
            IRCClient.Channels.Join(IRCChannels);
        }
        static void OnProtocolError(object sender, IrcProtocolErrorEventArgs e)
        {
            Console.WriteLine(string.Format("Protocol Error: {0}", e.Message));
        }
        static void OnSend(string type, string str)
        {
            Console.WriteLine(string.Format("Send/{0}: {1}", type, str));
        }
        static void OnSent(string type, string str)
        {
            Console.WriteLine(string.Format("Sent/{0}: {1}", type, str));
        }
        static void OnReceived(string type, string str)
        {
            Console.WriteLine(string.Format("Received/{0}: {1}", type, str));
        }
        static void OnSentMessage(object sender, IrcMessageEventArgs e)
        {
            OnSent("Message", e.Text);
        }
        static void OnSentNotice(object sender, IrcMessageEventArgs e)
        {
            OnSent("Notice", e.Text);
        }
        static void OnSentRawMessage(object sender, IrcRawMessageEventArgs e)
        {
#if DEBUG
            if (e.Message.Command.ToUpper() != "PASS")
            {
                OnSent("Raw Message", e.RawContent);
            }
#endif
        }
        static void OnReceivedRawMessage(object sender, IrcRawMessageEventArgs e)
        {
#if DEBUG
            if (e.Message.Command.ToUpper() != "JOIN" && e.Message.Command.ToUpper() != "QUIT" && e.Message.Command.ToUpper() != "PART" && e.Message.Command != "353")
            {
                OnReceived("Raw Message", e.RawContent);
            }
#endif
            string[] msgarr = e.Message.Parameters.ToArray();
            switch (e.Message.Command.ToUpper())
            {
                case "001":
                    OnReceived("Welcome Message", msgarr[1]);
                    break;
                case "375":
                    OnReceived("Motd Start  ", msgarr[1]);
                    break;
                case "372":
                    OnReceived("Motd Message", msgarr[1]);
                    break;
                case "376":
                    OnReceived("Motd End    ", msgarr[1]);
                    break;
                case "332":
                    OnReceived(string.Format("Channel Topic({0})", msgarr[1]), msgarr[2]);
                    break;
                case "333":
                    OnReceived(string.Format("Channel Topic Info({0})", msgarr[1]), msgarr[2]);
                    break;
                case "PRIVMSG":
                    string channel = msgarr[0];
                    string msg = msgarr[1];
                    if (channel.StartsWith("#"))
                    {
                        OnReceivedPublicMessage(channel, e.Message.Source.Name, msg);
                    }
                    break;
            }
        }
        static void OnReceivedPrivateNotice(object sender, IrcMessageEventArgs e)
        {
            OnReceived(string.Format("Private Notice({0})", e.Source.Name), e.Text);
        }
        static void OnReceivedPrivateMessage(object sender, IrcMessageEventArgs e)
        {
            OnReceived(string.Format("Private Message({0})", e.Source.Name), e.Text);
            ProcessMessage(null, e.Source.Name, e.Text);
        }
        static void OnReceivedPublicMessage(string channel, string sender, string text)
        {
            OnReceived(string.Format("Public Message({0}/{1})", channel, sender), text);
            ProcessMessage(channel, sender, text);
        }
        static bool ProcessMessage(string channel, string target, string str)
        {
            if (str.StartsWith("!"))
            {
                str = str.Substring(1);
                string[] strarr = str.Split(' ');
                string command = strarr[0].ToUpper();
                string text = "";
                if (strarr.Length > 1)
                {
                    text = string.Join(" ", strarr, 1, strarr.Length - 1);
                }
                switch (command)
                {
                    case "TEST":
                        if (!string.IsNullOrEmpty(text))
                        {
                            SendMessage(target, "Testing...");
                            return true;
                        }
                        break;
                    default:
                        break;
                }
            }
            return false;
        }
        static void SendMessage(string target, string str)
        {
            if (target != null)
            {
                OnSend("Message", str);
                IRCClient.LocalUser.SendMessage(target, str);
            }
        }
    }
}
