using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium;
using SIPSorcery.SIP;
using Spectre.Console;

namespace SipIntercept
{
    internal class ConsoleTestApp : IApp
    {
        public void Init()
        {}

        public void ReopenApp()
        {}

        public void OpenChat(string number)
        {}

        public bool CheckNuberIsValid(string number)
        {
            return true;
        }

        public void CallCurrentContact()
        {}

        public void EndCall()
        {}

        public bool IsCallingScreen()
        {
            bool isCallScreen = true;

            AnsiConsole.MarkupLine("Press Esc to end the call");

            while (Console.ReadKey(true).Key != ConsoleKey.Escape)
            {
            }

            return isCallScreen;
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
