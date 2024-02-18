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
            throw new NotImplementedException();
        }

        public void ReopenApp()
        {
            throw new NotImplementedException();
        }

        public void OpenChat(string number)
        {
            throw new NotImplementedException();
        }

        public bool CheckNuberIsValid(string number)
        {
            throw new NotImplementedException();
        }

        public void CallCurrentContact()
        {
            throw new NotImplementedException();
        }

        public void EndCall()
        {
            throw new NotImplementedException();
        }

        public bool IsCallingScreen()
        {
            throw new NotImplementedException();
        }

        public bool IsCallDeclined()
        {
            throw new NotImplementedException();
        }

        public bool IsCallActive()
        {
            throw new NotImplementedException();
        }

        public bool IsRinging()
        {
            throw new NotImplementedException();
        }

        public void CallAgain()
        {
            throw new NotImplementedException();
        }

        public bool IsNoAnswer()
        {
            throw new NotImplementedException();
        }

        public bool IsContactScreen()
        {
            throw new NotImplementedException();
        }

        public void CancellCall()
        {
            throw new NotImplementedException();
        }
    }
}
