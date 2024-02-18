using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SipIntercept
{
    public class SkypeApp : WinAppDriver, IApp
    {
        private const WinAppType AppType = WinAppType.ClassicApp;
        private const string AppPath = @"C:\Program Files (x86)\Microsoft\Skype for Desktop\Skype.exe";

        public SkypeApp() : base(AppPath, AppType)
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
        {throw new NotImplementedException();
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
