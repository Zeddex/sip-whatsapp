using System;
using System.Text.RegularExpressions;

namespace SipIntercept
{
    public class WhatsAppApp
    {
        private const string AppPackage = "com.whatsapp";
        private const string AppActivity = "com.whatsapp.Main";
        private static AppiumApp app;

        public void Init()
        {
            app = new AppiumApp();
            app.Init(AppPackage, AppActivity);
            app.RunCurrentApp();
        }

        public void CloseApp()
        {
            app.CloseApp();
        }

        public void ReopenApp()
        {
            app.RunCurrentApp();
        }

        public void OpenChat(string number)
        {
            string command = $"adb shell am start -a android.intent.action.VIEW -d https://wa.me/{number}";
            app.ExecuteAdbShellCommand(command);
        }

        public bool CheckNuberIsValid(string number)
        {
            //Ext.WriteLog($"Start checking number: {Ext.GetTime()}", ConsoleColor.DarkCyan);
            OpenChat(number);

            //bool isValid = app.FindElement("//android.widget.ImageButton[@content-desc='Voice call']");
            bool isValid = app.TryWaitElement("//android.widget.ImageButton[@content-desc='Voice call']", 2);

            //string numberInfo = app.GetElementText("//android.widget.TextView[contains(@resource-id, 'conversation_contact_name')]");

            //Ext.WriteLog($"End checking number: {Ext.GetTime()}", ConsoleColor.DarkCyan);
            return isValid;
        }

        public void CallCurrentContact()
        {
            app.WaitAndClick("//android.widget.ImageButton[@content-desc='Voice call']");
            app.TryClick("//android.widget.Button[@content-desc='Call']");
        }

        public void EndCall()
        {
            app.TryClick("//android.widget.ImageButton[@content-desc='Leave call']");
        }

        public bool IsCallingScreen()
        {
            bool isCallingScreen = app.FindElement("//android.widget.ImageButton[@content-desc='Leave call']");
            //bool isCallingScreen = app.FindElement("//android.widget.ImageButton[@content-desc='Cancel']");

            return isCallingScreen;
        }

        public bool IsCallDeclined()
        {
            bool isCallDeclined = app.FindElement("//android.widget.ImageButton[@content-desc='Cancel']");

            return isCallDeclined;
        }

        public string? GetActivityDump()
        {
            //string command = "adb shell dumpsys activity com.whatsapp | findstr audio_call_status";
            string command = "adb shell dumpsys activity com.whatsapp";
            string? output = app.ExecuteAdbShellCommand(command);

            return output;
        }

        /// <summary>
        /// Check if dump contains filter string
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public bool ActivityDumpFilter(string filter)
        {
            string? dump = GetActivityDump();
            return dump != null && dump.Contains(filter);
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

        public void CallAgain()
        {
            app.TryClick("//android.widget.ImageButton[@content-desc='Call again']");
        }

        public bool IsNoAnswer()
        {
            bool isNoAnswer = app.FindElement("//android.widget.ImageButton[@content-desc='Call again']");

            return isNoAnswer;
        }

        public bool IsContactScreen()
        {
            bool isContactScreen = app.FindElement("//android.widget.ImageButton[@content-desc='New call']");

            return isContactScreen;
        }

        public void CancellCall()
        {
            app.TryClick("//android.widget.ImageButton[@content-desc='Cancel']");
        }
    }
}
