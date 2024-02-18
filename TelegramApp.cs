using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SipIntercept
{
    public class TelegramApp : WinAppDriver, IApp
    {
        private const WinAppType AppType = WinAppType.ClassicApp;
        private const string AppPath = @"C:\Users\Cat\AppData\Roaming\Telegram Desktop\Telegram.exe";

        public TelegramApp() : base(AppPath, AppType)
        {}


        public void Init()
        {
            Run();
        }

        public void ReopenApp()
        {
            
        }

        public void OpenChat(string number)
        {
            
        }

        public bool CheckNuberIsValid(string number)
        {
            return true;
        }

        public void CallCurrentContact()
        {
            
        }

        public void EndCall()
        {
            
        }

        public bool IsCallingScreen()
        {
            return true;
        }

        public bool IsCallDeclined()
        {
            return false;
        }

        public bool IsCallActive()
        {
            return true;
        }

        public bool IsRinging()
        {
            return true;
        }
    }
}
