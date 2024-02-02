using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SIPSorcery.SIP.App;
using SIPSorcery.SIP;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Threading;
using System.Net.Sockets;
using NAudio.Wave;
using NAudio.MediaFoundation;

namespace SipIntercept
{
    public class SipHandler
    {
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public int Expire { get; set; }
        public List<AudioCodecsEnum> Codecs { get; set; }
        public WhatsAppApp WhatsAppApp { get; set; }

        private StunClient _stunClient;
        private readonly WindowsAudioEndPoint _audioEndPoint;
        private readonly SIPTransport _sipTransport;
        private readonly ConcurrentDictionary<string, SIPUserAgent> _calls;

        public SipHandler(string user, string password, string domain, int port = 5060, int expire = 120)
        {
            _audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder());

            User = user;
            Password = password;
            Domain = domain;
            Port = port;
            Expire = expire;

            _sipTransport = new SIPTransport();
            _calls = new ConcurrentDictionary<string, SIPUserAgent>();

            _sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, Port)));
            _sipTransport.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(IPAddress.Any, Port)));
            _sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.IPv6Any, Port)));
            _sipTransport.EnableTraceLogs();
        }

        public void Init(List<AudioCodecsEnum> codecs, string stunServer, WhatsAppApp wa = null)
        {
            WhatsAppApp = wa;

            Codecs = codecs;

            InitializeStunClient(stunServer);

            var userAgent = StartRegistrations(_sipTransport, User, Password, Domain, Expire);
            userAgent.Start();
        }

        public void Init(WhatsAppApp wa)
        {
            WhatsAppApp = wa;

            var userAgent = StartRegistrations(_sipTransport, User, Password, Domain, Expire);
            userAgent.Start();
        }

        public void InitializeStunClient(string stunServerAddress)
        {
            _stunClient = new StunClient(stunServerAddress);
            _stunClient.Run();
        }

        private SIPRegistrationUserAgent StartRegistrations(SIPTransport sipTransport, string username, string password, string domain, int expiry)
        {
            var regUserAgent = new SIPRegistrationUserAgent(sipTransport, username, password, domain, expiry);

            regUserAgent.RegistrationFailed += RegUserAgentRegistrationFailed;
            regUserAgent.RegistrationTemporaryFailure += RegUserAgentRegistrationTemporaryFailure;
            regUserAgent.RegistrationRemoved += RegUserAgentRegistrationRemoved;
            regUserAgent.RegistrationSuccessful += RegUserAgentRegistrationSuccessful;

            return regUserAgent;

            void RegUserAgentRegistrationFailed(SIPURI uri, SIPResponse resp, string arg)
            {
                Console.WriteLine($"Registration Failed - {uri}: {resp}, {arg}");
            }

            void RegUserAgentRegistrationTemporaryFailure(SIPURI uri, SIPResponse resp, string arg)
            {
                Console.WriteLine($"Registration Temporary Failure - {uri}: {resp}, {arg}");
            }

            void RegUserAgentRegistrationRemoved(SIPURI uri, SIPResponse resp)
            {
                Console.WriteLine($"Registration Removed - {uri}: {resp}");
            }

            void RegUserAgentRegistrationSuccessful(SIPURI uri, SIPResponse resp)
            {
                Console.WriteLine($"Registration Successful - {uri}: {resp}\n\n");
            }
        }

        public void StopService()
        {
            _sipTransport.Shutdown();
            _stunClient.Stop();
        }
    }
}
