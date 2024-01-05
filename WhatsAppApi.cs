using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SipWA
{
    public class WhatsAppApi
    {
        public static string Token { get; set; }
        public static string InstanceId { get; set; }

        public WhatsAppApi(string token, string instanceId)
        {
            Token = token;
            InstanceId = instanceId;
        }

        public (bool isValid, string status) IsNumberValid(string number)
        {
            string apiUrl = $"https://api.ultramsg.com/{InstanceId}/contacts/check?token={Token}&chatId={number}@c.us";

            string response = Get(apiUrl);

            if (response == null)
            {
                return (false, "api error");
            }

            var parseResponse = Ext.ExtractDataValue(response, "status");

            //Console.WriteLine($"Response from whatsapp api: {response}");

            if (parseResponse.status == "invalid")
            {
                return (false, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(parseResponse.status)) 
            { 
                return (false, parseResponse.error); 
            }

            return (true, string.Empty);

        }

        private string Get(string uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            string result = null;

            try
            {
                using var response = (HttpWebResponse)request.GetResponse();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                result = reader.ReadToEnd();
            }

            catch {}
            
            return result;
        }

        private async Task<string> GetAsync(string uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            string result = null;

            try
            {
                using var response = (HttpWebResponse)request.GetResponse();
                using var stream = response.GetResponseStream();
                using var reader = new StreamReader(stream);
                result = await reader.ReadToEndAsync();
            }

            catch { }

            return result;
        }

        private string Post(string uri, string data, string contentType, string method = "POST")
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.ContentLength = dataBytes.Length;
            request.ContentType = contentType;
            request.Method = method;

            using (Stream requestBody = request.GetRequestStream())
            {
                requestBody.Write(dataBytes, 0, dataBytes.Length);
            }

            using var response = (HttpWebResponse)request.GetResponse();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private async Task<string> PostAsync(string uri, string data, string contentType, string method = "POST")
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.ContentLength = dataBytes.Length;
            request.ContentType = contentType;
            request.Method = method;

            using (Stream requestBody = request.GetRequestStream())
            {
                await requestBody.WriteAsync(dataBytes, 0, dataBytes.Length);
            }

            using var response = (HttpWebResponse)await request.GetResponseAsync();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }
}
