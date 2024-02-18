using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SipIntercept
{
    internal class WhatsAppWinApp : WinAppDriver, IApp
    {
        private const WinAppType AppType = WinAppType.UwpApp;
        private const string AppName = "5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App";

        public WhatsAppWinApp() : base(AppName, AppType)
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
            string waUrl = $"whatsapp://send/?phone={number}";
            OpenWaProtocolLink(waUrl);
        }

        public bool CheckNuberIsValid(string number)
        {
            OpenChat(number);

            var isNoNumberPopup = FindElement(By.Name("Popup"));

            return !isNoNumberPopup;
        }

        public void CallCurrentContact()
        {
            GetElement(By.XPath("//Button[@Name='Voice call']")).Click();
        }

        public void EndCall()
        {
            
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

        private void OpenWaProtocolLink(string url)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }
    }
}
