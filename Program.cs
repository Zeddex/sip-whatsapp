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
            string sipUsername = "u38-m13";
            string sipPassword = "Pe3SUVagWsrYO4e";
            string sipDomain = "185.12.237.56";
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