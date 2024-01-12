using System;
using System.Linq;

namespace SipWA
{
    class Program
    {
        static void Main()
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
            string sipUsername = "xxxx";
            string sipPassword = "xxxx";
            string sipDomain = "8.8.8.8";
            int sipPort = 5060;
            int sipExpire = 600;
            var sip = new Sip(sipUsername, sipPassword, sipDomain, sipPort, sipExpire);
            sip.Init(waApp, codecs);

            Console.ReadKey();

            sip.StopService();
            waApp.CloseApp();
        }

    }
}