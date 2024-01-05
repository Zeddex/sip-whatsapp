using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using SIPSorcery.SIP;

namespace SipWA
{
    internal class Ext
    {
        public static (string status, string? error) ExtractDataValue(string json, string key)
        {
            string status = string.Empty;
            object error;

            dynamic data = JObject.Parse(json);

            try
            {
                status = data[key];
                error = data["error"];
            }

            catch 
            {
                error = data["error"];
            }

            error ??= string.Empty;

            return (status, error.ToString());
        }

        public static string ParseCallerNumber(string callerData)
        {
            //var regex = new Regex("(?<=sip:\\+).*(?=@)");
            var regex = new Regex("(?<=sip:).*(?=@)");
            string phoneNumber = regex.Match(callerData).Value;

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                Ext.WriteLog($"Parsing number error", ConsoleColor.Red);
                return "";
            }

            return phoneNumber;
        }

        public static bool IsWANumberValid(string number)
        {
            var waApi = new WhatsAppApi(WhatsAppApi.Token, WhatsAppApi.InstanceId);

            var result = waApi.IsNumberValid(number);

            if (!result.isValid && result.status != "")
            {
                WriteLog($"WhatsApp API error!", ConsoleColor.Red);
            }

            return result.isValid;
        }

        public static void WriteLog(string text, ConsoleColor textColor)
        {
            Console.ForegroundColor = textColor;
            Console.WriteLine(text);
            Console.ResetColor();
        }
       
        public static TimeSpan GetTime()
        {
            return DateTime.Now.TimeOfDay;
        }
    }
}
