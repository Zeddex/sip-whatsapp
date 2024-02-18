using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SipIntercept
{
    internal class ContactsApp
    {
        private const string AppPackage = "com.android.contacts";
        private const string AppActivity = "com.android.contacts.activities.PeopleActivity";
        //private const string AppPackage = "com.samsung.android.app.contacts";
        //private const string AppActivity = "com.samsung.android.contacts.contactslist.PeopleActivity";
        private static Appium app;

        public void Init()
        {
            app = new Appium();
            app.Init(AppPackage, AppActivity);
            //app.RunCurrentApp();
        }

        public void CloseApp()
        {
            app.CloseApp();
        }

        public void AddNumber(string number)
        {
            #region Android 11 mobile phone

            //app.Click("//android.widget.Button[@content-desc='Create contact']");
            //Thread.Sleep(1000);

            //// check popup menu where to save contacts
            //if (app.FindElement("//android.widget.TextView[contains(@resource-id, 'dropdown_title_text') and @text='Save contact to']"))
            //{
            //    app.TryClick("//android.widget.TextView[contains(@resource-id, 'text1') and @text='Phone']");
            //}

            ////app.SendText("//android.widget.EditText[@text='Name']", number);
            //app.Click("//android.widget.TextView[contains(@resource-id, 'titleText') and @text='Phone']");
            //app.SendText("//android.widget.EditText[@text='Phone']", "+" + number);

            ////// check if contact exist
            ////if (app.FindElement("//android.widget.Button[contains(@resource-id, 'button1') and @text='Update']"))
            ////{
            ////    app.Click("//android.widget.Button[contains(@resource-id, 'button2') and @text='Cancel']");
            ////    app.Click("//android.widget.Button[@content-desc='Cancel']");
            ////    app.Click("//android.widget.Button[contains(@resource-id, 'button1') and @text='Update']");
            ////}
            ////else
            ////{
            ////    app.SendText("//android.widget.EditText[@text='Phone']", "+" + number);
            ////}

            //app.Click("//android.widget.Button[@content-desc, 'Save']");
            //app.Click("//android.widget.TextView[contains(@resource-id, 'smallLabel') and @text='Save']");

            #endregion

            #region Memu 7

            app.Click("//android.widget.ImageButton[@content-desc='add new contact']");
            app.WaitElement("//android.widget.EditText[@text='Phone']");
            app.SendText("//android.widget.EditText[@text='Phone']", "+" + number);
            app.Click("//android.widget.TextView[contains(@resource-id, 'menu_save') and @content-desc='Save']");

            #endregion
        }

        public void WhatsappCall(string number)
        {
            #region Android 7 memu emulator

            app.Click("//android.widget.LinearLayout[contains(@resource-id, 'third_party_messenger_card_content_area')]");

            #endregion

            #region Android 11 mobile phone

            //app.Click("//android.widget.LinearLayout[contains(@resource-id, 'third_party_messenger_card_content_area')]");
            //app.WaitAndClick("//android.widget.TextView[contains(@resource-id, 'action') and contains(@text, 'Voice call')]");

            //// check permissions
            //if (app.FindElement("//android.widget.TextView[contains(@resource-id, 'permission_message')]"))
            //{
            //    app.TryClick("//android.widget.Button[contains(@resource-id, 'submit')]");
            //    app.TryClick("//android.widget.Button[contains(@resource-id, 'permission_allow_foreground_only_button')]");
            //}

            #endregion
        }

        public void ReopenApp()
        {
            app.RunCurrentApp();
        }
    }
}
