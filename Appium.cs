using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.MultiTouch;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SipIntercept
{
    public class Appium
    {
        private AppiumOptions? _options;
        private AndroidDriver<IWebElement> _driver;
        private WebDriverWait _wait;

        public void Init(string appPackage, string appActivity, bool noReset = true, string deviceName = "Samsung A50")
        {
            Console.WriteLine("Starting Appium...\n");

            var p = new Process
            {
                StartInfo =
                {
                    FileName = "cmd.exe",
                    Arguments = "/C appium --address 127.0.0.1 --port 4723 --relaxed-security --log-level error"
                }
            };
            p.Start();

            // check appium server is started
            var request = WebRequest.Create("http://127.0.0.1:4723/sessions");
            request.Method = "HEAD";
            WebResponse response = null;

            while (response == null)
            {
                try
                {
                    response = request.GetResponse();
                }
                catch { }

                Thread.Sleep(1000);
            }

            _options = new AppiumOptions();
            _options.PlatformName = "Android";
            _options.AddAdditionalCapability("appium:automationName", "UiAutomator2");
            _options.AddAdditionalCapability("appium:noReset", noReset);
            _options.AddAdditionalCapability("appium:deviceName", deviceName);
            _options.AddAdditionalCapability("appium:appPackage", appPackage);
            _options.AddAdditionalCapability("appium:appActivity", appActivity);
            _options.AddAdditionalCapability("appium:newCommandTimeout", 3000);

            _driver = new AndroidDriver<IWebElement>(new Uri("http://127.0.0.1:4723"), _options);
        }

        public void CloseApp()
        {
            _driver.CloseApp();

            foreach (var process in Process.GetProcessesByName("node"))
            {
                process.Kill();
            }
        }

        public void RunCurrentApp()
        {
            _driver.LaunchApp();
        }

        #region App Navigation

        public string GetElementText(string xpath)
        {
            try
            {
                var el = _driver.FindElementByXPath(xpath);
                string text = el.Text;
                return text;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void Click(string xpath)
        {
            _driver.FindElementByXPath(xpath).Click();
        }

        public void TryClick(string xpath)
        {
            try
            {
                _driver.FindElementByXPath(xpath).Click();
            }
            catch { }
        }

        public void WaitAndClick(string xpath, int seconds = 10)
        {
            WaitElement(xpath, seconds);

            try
            {
                _driver.FindElementByXPath(xpath).Click();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public bool FindElement(string xpath)
        {
            IWebElement element = null;

            bool found = false;

            try
            {
                element = _driver.FindElementByXPath(xpath);
            }
            catch { }


            if (element != null)
            {
                found = true;
            }

            return found;
        }

        public void WaitElement(string xpath, int seconds = 10)
        {
            if (_wait == null)
            {
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(seconds));
            }

            _wait.Timeout = TimeSpan.FromSeconds(seconds);
            _wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(xpath)));
        }

        public bool TryWaitElement(string xpath, int seconds = 10)
        {
            if (_wait == null)
            {
                _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(seconds));
            }

            _wait.Timeout = TimeSpan.FromSeconds(seconds);

            try
            {
                _wait.Until(ExpectedConditions.PresenceOfAllElementsLocatedBy(By.XPath(xpath)));
            }
            catch
            {
                return false;
            }

            return true;
        }

        public void Scroll(int fromX, int fromY, int toX, int toY)
        {
            var action = new TouchAction(_driver);

            action.LongPress(fromX, fromY)
                .MoveTo(toX, toY)
                .Release()
                .Perform();
        }

        public void Swipe(int fromX, int fromY, int toX, int toY)
        {
            var action = new TouchAction(_driver);

            action.Press(fromX, fromY)
                .Wait(500)
                .MoveTo(toX, toY)
                .Release()
                .Perform();
        }

        public void SwipeCenterLeft(int x = 540, int y = 900)
        {
            var action = new TouchAction(_driver);

            action.Press(x, y)
                .Wait(500)
                .MoveTo(x - 200, y)
                .Release()
                .Perform();
        }

        public void SwipeCenterRight(int x = 540, int y = 900)
        {
            var action = new TouchAction(_driver);

            action.Press(x, y)
                .Wait(500)
                .MoveTo(x + 200, y)
                .Release()
                .Perform();
        }

        public void SendText(string xpath, string text)
        {
            _driver.FindElementByXPath(xpath).SendKeys(text);
        }

        public void ClearField(string xpath)
        {
            _driver.FindElementByXPath(xpath).Clear();
        }

        #endregion


        #region AdbCommands

        public string? ExecuteAdbShellCommand(string adbShellCommand)
        {
            var args = AdbConvertToAppium(adbShellCommand);

            var output = _driver.ExecuteScript("mobile: shell", args);

            return output.ToString();
        }

        public void ToggleAirplaneMode()
        {
            _driver.ToggleAirplaneMode();

            Thread.Sleep(3000);
        }

        private Dictionary<string, object> AdbConvertToAppium(string adbShellCommand)
        {
            var map = new Dictionary<string, object>();
            var parameters = new List<string>();

            var args = adbShellCommand.Split(' ');

            map.Add("command", args[2]);

            if (args.Length > 3)
            {
                for (int i = 3; i < args.Length; i++)
                {
                    parameters.Add(args[i]);
                }
            }

            map.Add("args", parameters);

            return map;
        }

        #endregion
    }
}
