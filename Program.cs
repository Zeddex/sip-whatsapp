using System;

namespace SipWA
{
    internal class Program
    {
        private static void Main()
        {
            var menu = new Menu();
            menu.StartInfo();
            var codecs = menu.CodecsList();

            var waApp = new WhatsAppApp();
            waApp.Init();

            // whatsapp api service
            //string waToken = "token12345";
            //string waInstance = "instance12345";
            //var whatsAppApi = new WhatsAppApi(waToken, waInstance);

            // sip credentials
            string sipUsername = "sipUsername";
            string sipPassword = "sipPassword";
            string sipDomain = "sipDomain";
            int sipPort = 5060;
            int sipExpire = 600;
            var sip = new Sip(sipUsername, sipPassword, sipDomain, sipPort, sipExpire)
            {
                Debug = false
            };
            sip.Init(waApp, codecs);

            Console.ReadKey();

            sip.StopService();
            waApp.CloseApp();
        }

    }
}