using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium.Service;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Net.Http;
using System.Threading;
using OpenQA.Selenium.Appium.MultiTouch;

namespace SipIntercept
{
    public class WinAppDriver
    {
        private readonly WinAppType _appType;
        private readonly string _appName;
        private readonly string? _appArguments;
        private readonly string? _appWorkingDir;

        protected static WindowsDriver<WindowsElement> session;
        protected static WebDriverWait wait;

        public WinAppDriver(string appNamePath, WinAppType appType, string? appArguments = null, string? appWorkingDir = null)
        {
            _appName = appNamePath;
            _appType = appType;
            _appWorkingDir = appWorkingDir;
            _appArguments = appArguments;
        }

        public void Run()
        {
            StartWinAppDriver();
            InitializeAppDriver();
        }

        private void StartWinAppDriver()
        {
            string winAppDriverPath = @"C:\Program Files\Windows Application Driver\WinAppDriver.exe";

            var process = new Process
            {
                StartInfo =
                {
                    FileName = winAppDriverPath,
                }
            };
            process.Start();
        }

        private void InitializeAppDriver()
        {
            var options = new AppiumOptions
            {
                PlatformName = "Windows"
            };
            options.AddAdditionalCapability("app", _appName);
            options.AddAdditionalCapability("aautomationName", "Windows");
            options.AddAdditionalCapability("deviceName", "WindowsPC");
            options.AddAdditionalCapability("appArguments", _appArguments);
            options.AddAdditionalCapability("ms:waitForAppLaunch", 7);

            if (_appType == WinAppType.ClassicApp)
            {
                options.AddAdditionalCapability("appWorkingDir", _appWorkingDir);
            }

            string serverUrl = "http://127.0.0.1:4723";
            session = new WindowsDriver<WindowsElement>(new Uri(serverUrl), options);

            session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1.5);
        }

        #region App Navigation

        public WindowsElement GetElement(By selector)
        {
            var element = session.FindElement(selector);

            return element;
        }

        public string GetElementText(By selector)
        {
            try
            {
                var el = session.FindElement(selector);
                string text = el.Text;
                return text;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void Click(By selector)
        {
            session.FindElement(selector).Click();
        }

        public void TryClick(By selector)
        {
            try
            {
                session.FindElement(selector).Click();
            }
            catch { }
        }

        public void WaitAndClick(By selector, int seconds = 10)
        {
            WaitElement(selector, seconds);

            try
            {
                session.FindElement(selector).Click();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public bool FindElement(By selector)
        {
            bool found = false;
            WindowsElement? element = null;

            try
            {
                element = session.FindElement(selector);
            }
            catch { }


            if (element != null)
            {
                found = true;
            }

            return found;
        }

        public void WaitElement(By selector, int seconds = 10)
        {
            if (wait == null)
            {
                wait = new WebDriverWait(session, TimeSpan.FromSeconds(seconds));
            }

            wait.Timeout = TimeSpan.FromSeconds(seconds);
            wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(selector));
        }

        public bool TryWaitElement(By selector, int seconds = 10)
        {
            if (wait == null)
            {
                wait = new WebDriverWait(session, TimeSpan.FromSeconds(seconds));
            }

            wait.Timeout = TimeSpan.FromSeconds(seconds);

            try
            {
                wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(selector));
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void SendText(By selector, string text)
        {
            session.FindElement(selector).SendKeys(text);
        }

        public void ClearField(string xpath)
        {
            session.FindElementByXPath(xpath).Clear();
        }

        public void CloseApp()
        {
            if (session != null)
            {
                session.Quit();
                session = null;
            }
        }

        #endregion
    }
}
