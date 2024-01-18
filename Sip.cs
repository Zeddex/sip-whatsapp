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

namespace SipWA
{
    public class Sip
    {
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public int Expire { get; set; }
        public WhatsAppApp WhatsAppApp { get; set; }
        public bool IsCallCancelled { get; set; }
        public List<AudioCodecsEnum> Codecs { get; set; }

        private StunClient _stunClient;
        private readonly WindowsAudioEndPoint _audioEndPoint;
        private readonly SIPTransport _sipTransport;
        private readonly ConcurrentDictionary<string, SIPUserAgent> _calls;

        public Sip(string user, string password, string domain, int port = 5060, int expire = 120)
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

        public void Init(WhatsAppApp wa, List<AudioCodecsEnum> codecs, string stunServer)
        {
            WhatsAppApp = wa;

            Codecs = codecs;

            InitializeStunClient(stunServer);

            _sipTransport.SIPTransportRequestReceived += OnRequest;

            var userAgent = StartRegistrations(_sipTransport, User, Password, Domain, Expire);
            userAgent.Start();
        }

        public void Init(WhatsAppApp wa)
        {
            WhatsAppApp = wa;

            _sipTransport.SIPTransportRequestReceived += OnRequest;

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

        private async Task OnRequest(SIPEndPoint localSipEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                IsCallCancelled = false;

                if (sipRequest.Method == SIPMethodsEnum.INVITE)
                {
                    Console.WriteLine($"Incoming call request: {localSipEndPoint}<-{remoteEndPoint} {sipRequest.URI}.");

                    var tryingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Trying, null);
                    await _sipTransport.SendResponseAsync(tryingResponse);

                    string callerNumber = Ext.ParseCallerNumber(sipRequest.URI.ToString());
                    //Ext.WriteLog($"Caller number: {callerNumber}", ConsoleColor.Green);

                    //bool isWAValid = Ext.IsWANumberValid(callerNumber);
                    bool isWaValid = WhatsAppApp.CheckNuberIsValid(callerNumber);

                    if (!isWaValid)
                    {
                        Ext.WriteLog($"{callerNumber} is not registered in WhatsApp", ConsoleColor.Gray);

                        //decline the call
                        var unavailResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.ServiceUnavailable, null);
                        await _sipTransport.SendResponseAsync(unavailResponse);

                        Console.WriteLine($"Sent {SIPResponseStatusCodesEnum.ServiceUnavailable} to {sipRequest.URI}");
                    }

                    else
                    {
                        Ext.WriteLog($"{callerNumber} is registered in WhatsApp", ConsoleColor.Green);

                        string host = Ext.ParseHost(sipRequest.URI.ToString());
                        _sipTransport.ContactHost = host;

                        var ua = new SIPUserAgent(_sipTransport, null);

                        ua.OnCallHungup += OnHangup;

                        ua.ServerCallCancelled += (_) =>
                        {
                            Console.WriteLine("Incoming call cancelled by remote party.");
                            WhatsAppApp.EndCall();

                            IsCallCancelled = true;
                        };

                        ua.ServerCallRingTimeout += (uas) =>
                        {
                            Console.WriteLine($"Incoming call timed out in {uas.ClientTransaction.TransactionState} state waiting for client ACK, terminating.");
                            WhatsAppApp.EndCall();
                            ua.Hangup();

                            IsCallCancelled = true;
                        };

                        if (!IsCallCancelled)
                        {
                            var uas = ua.AcceptCall(sipRequest);

                            var rtpSession = CreateRtpSession(ua, sipRequest.URI.User);
                            //var rtpSession = CreateRtpSessionTestSound(ua, sipRequest.URI.User);

                            var ringingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ringing, null);
                            await _sipTransport.SendResponseAsync(ringingResponse);

                            Ext.WriteLog("Calling to whatsapp...", ConsoleColor.Blue);
                            WhatsAppApp.OpenChat(callerNumber);
                            WhatsAppApp.CallCurrentContact();

                            while (!WhatsAppApp.IsRinging())
                            {
                                await Task.Delay(100);
                            }

                            while (!WhatsAppApp.IsCallActive())
                            {
                                await Task.Delay(100);

                                if (WhatsAppApp.IsCallDeclined())
                                {
                                    Ext.WriteLog("WA call unanswered or declined", ConsoleColor.Red);
                                    IsCallCancelled = true;

                                    //decline the call
                                    var busyResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.BusyHere, null);
                                    await _sipTransport.SendResponseAsync(busyResponse);

                                    break;
                                }     
                            }

                            if (!IsCallCancelled)
                            {
                                Ext.WriteLog("WA answered", ConsoleColor.Green);

                                await ua.Answer(uas, rtpSession);
                                Ext.WriteLog("SIP answered", ConsoleColor.Blue);

                                if (ua.IsCallActive)
                                {
                                    await rtpSession.Start();
                                    _calls.TryAdd(ua.Dialogue.CallId, ua);
                                    //Ext.WriteLog($"Caller id = {ua.Dialogue.CallId}", ConsoleColor.Gray);
                                }

                                while (WhatsAppApp.IsCallingScreen())
                                {
                                    await Task.Delay(100);
                                }
                            }

                            // closing RTP session
                            ua.Hangup();
                            rtpSession.Close("End call");

                            if (ua.Dialogue != null)
                            {
                                EndCall(ua.Dialogue.CallId);
                            }

                            WhatsAppApp.ReopenApp();
                        }
                    }
                }
                else if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    if (_calls.TryRemove(sipRequest.Header.CallId, out var ua))
                    {
                        var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                        await _sipTransport.SendResponseAsync(okResponse);
                        Ext.WriteLog($"Call ended with 200 OK sent to {sipRequest.URI}", ConsoleColor.Green);

                        ua.Close();
                    }
                    else
                    {
                        // The call does not exist or is already terminated.
                        var notFoundResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                        await _sipTransport.SendResponseAsync(notFoundResponse);
                    }

                    if (WhatsAppApp.IsCallingScreen())
                    {
                        WhatsAppApp.EndCall();
                    }
                }
                else if (sipRequest.Method == SIPMethodsEnum.SUBSCRIBE)
                {
                    var notAllowededResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                    await _sipTransport.SendResponseAsync(notAllowededResponse);
                }
                else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
                {
                    var optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await _sipTransport.SendResponseAsync(optionsResponse);
                }
                else if (sipRequest.Method == SIPMethodsEnum.ACK)
                {
                    var optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await _sipTransport.SendResponseAsync(optionsResponse);
                    Ext.WriteLog($"SIP Status: {sipRequest.Method}", ConsoleColor.Red);
                }
            }
            catch (Exception reqExcp)
            {
                Console.WriteLine($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
            }
        }

        private void EndCall(string callId)
        {
            if (_calls.TryGetValue(callId, out var userAgent))
            {
                userAgent.Hangup();
                _calls.TryRemove(callId, out _);  // Optionally remove the call from the dictionary
                Console.WriteLine("Call ended with BYE request.");
            }
            else
            {
                Console.WriteLine("Call not found.");
            }
        }

        private void OnHangup(SIPDialogue dialogue)
        {
            if (dialogue != null)
            {
                string callID = dialogue.CallId;

                if (_calls.ContainsKey(callID))
                {
                    if (_calls.TryRemove(callID, out var ua))
                    {
                        ua.Close();
                    }
                }
            }

            WhatsAppApp.EndCall();
        }

        private VoIPMediaSession CreateRtpSession(SIPUserAgent ua, string dst)
        {
            var rtpAudioSession = new VoIPMediaSession(new MediaEndPoints
            {
                AudioSource = _audioEndPoint,
                AudioSink = _audioEndPoint
            });
            _audioEndPoint.RestrictFormats(format => Codecs.Contains(format.Codec));

            rtpAudioSession.AcceptRtpFromAny = true;

            rtpAudioSession.OnRtpPacketReceived += (ep, type, rtp) =>
            {
                // Handle incoming RTP packets
            };

            rtpAudioSession.OnTimeout += (_) =>
            {
                Console.WriteLine(ua.Dialogue != null
                    ? $"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up."
                    : "RTP timeout on incomplete call, closing RTP session.");

                ua.Hangup();
            };

            return rtpAudioSession;
        }

        private VoIPMediaSession CreateRtpSessionTestSound(SIPUserAgent ua, string dst)
        {
            if (string.IsNullOrEmpty(dst) || !Enum.TryParse(dst, out AudioSourcesEnum audioSource))
            {
                audioSource = AudioSourcesEnum.Music;
            }

            var audioExtrasSource = new AudioExtrasSource(new AudioEncoder(), new AudioSourceOptions { AudioSource = audioSource });

            //var codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMU, AudioCodecsEnum.PCMA, AudioCodecsEnum.G729 };
            //audioExtrasSource.RestrictFormats(formats => codecs.Contains(formats.Codec));

            audioExtrasSource.RestrictFormats(formats => Codecs.Contains(formats.Codec));

            // ver 1
            var rtpAudioSession = new VoIPMediaSession(new MediaEndPoints { AudioSource = audioExtrasSource });

            // ver 2
            //var rtpAudioSession = new VoIPMediaSession(_audioEndPoint.ToMediaEndPoints());

            rtpAudioSession.AcceptRtpFromAny = true;

            // Wire up the event handler for RTP packets received from the remote party.
            rtpAudioSession.OnRtpPacketReceived += (ep, type, rtp) =>
            {
                // The raw audio data is available in rtpPacket.Payload
            };

            rtpAudioSession.OnTimeout += (_) =>
            {
                Console.WriteLine(ua.Dialogue != null
                    ? $"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up."
                    : "RTP timeout on incomplete call, closing RTP session.");

                ua.Hangup();
            };

            return rtpAudioSession;
        }
    }
}
