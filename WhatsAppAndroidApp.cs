using System;
using System.Text.RegularExpressions;

namespace SipIntercept
{
    public class WhatsAppAndroidApp : IApp
    {
        private const string AppPackage = "com.whatsapp";
        private const string AppActivity = "com.whatsapp.Main";
        private static Appium _app;

        public void Init()
        {
            _app = new Appium();
            _app.Init(AppPackage, AppActivity);
            _app.RunCurrentApp();
        }

        public void ReopenApp()
        {
            _app.RunCurrentApp();
        }

        public void OpenChat(string number)
        {
            string command = $"adb shell am start -a android.intent.action.VIEW -d https://wa.me/{number}";
            _app.ExecuteAdbShellCommand(command);
        }

        public bool CheckNuberIsValid(string number)
        {
            //Ext.WriteLog($"Start checking number: {Ext.GetTime()}", ConsoleColor.DarkCyan);
            OpenChat(number);

            //bool isValid = _app.FindElement("//android.widget.ImageButton[@content-desc='Voice call']");
            bool isValid = _app.TryWaitElement("//android.widget.ImageButton[@content-desc='Voice call']", 2);

            //string numberInfo = _app.GetElementText("//android.widget.TextView[contains(@resource-id, 'conversation_contact_name')]");

            //Ext.WriteLog($"End checking number: {Ext.GetTime()}", ConsoleColor.DarkCyan);
            return isValid;
        }

        public void CallCurrentContact()
        {
            _app.WaitAndClick("//android.widget.ImageButton[@content-desc='Voice call']");
            _app.TryClick("//android.widget.Button[@content-desc='Call']");
        }

        public void EndCall()
        {
            _app.TryClick("//android.widget.ImageButton[@content-desc='Leave call']");
        }

        public bool IsCallingScreen()
        {
            bool isCallingScreen = _app.FindElement("//android.widget.ImageButton[@content-desc='Leave call']");
            //bool isCallingScreen = _app.FindElement("//android.widget.ImageButton[@content-desc='Cancel']");

            return isCallingScreen;
        }

        public bool IsCallDeclined()
        {
            bool isCallDeclined = _app.FindElement("//android.widget.ImageButton[@content-desc='Cancel']");

            return isCallDeclined;
        }

        public bool IsCallActive()
        {
            bool isCallActive = ActivityDumpFilter("audio_call_status");
            return isCallActive;
        }

        public bool IsRinging()
        {
            bool isRinging = ActivityDumpFilter("voipcalling.VoipActivityV2");
            return isRinging;
        }

        /// <summary>
        /// Check if dump contains filter string
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        private bool ActivityDumpFilter(string filter)
        {
            string? dump = GetActivityDump();
            return dump != null && dump.Contains(filter);
        }

        private string? GetActivityDump()
        {
            //string command = "adb shell dumpsys activity com.whatsapp | findstr audio_call_status";
            string command = "adb shell dumpsys activity com.whatsapp";
            string? output = _app.ExecuteAdbShellCommand(command);

            return output;
        }
    }
}
