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
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using Org.BouncyCastle.Asn1.Ocsp;
using Spectre.Console;

namespace SipIntercept
{
    public class SipHandler
    {
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public int Expire { get; set; }
        public bool IsCallCancelled { get; set; }
        public bool IsCallEnded { get; set; }
        public bool IsInvite { get; set; }
        public VoIPMediaSession? RtpSession { get; set; }
        public IApp App { get; set; }

        private StunClient _stunClient;
        private readonly WindowsAudioEndPoint _audioEndPoint;
        private readonly SIPTransport _sipTransport;
        private readonly ConcurrentDictionary<string, SIPUserAgent> _calls;

        public SipHandler(string user, string password, string domain, int port = 5060, int expire = 120)
        {
            _audioEndPoint = new WindowsAudioEndPoint(new AudioEncoder());

            var codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMA };
            _audioEndPoint.RestrictFormats(format => codecs.Contains(format.Codec));

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

        public void Init(IApp app, string? stunServer = null)
        {
            App = app;

            if (stunServer != null)
                InitializeStunClient(stunServer);

            _sipTransport.SIPTransportRequestReceived += OnRequest;

            var userAgent = StartRegistrations(_sipTransport, User, Password, Domain, Expire);
            userAgent.Start();
        }

        private void InitializeStunClient(string stunServerAddress)
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

        private async Task OnRequest(SIPEndPoint localSipEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                if (sipRequest.Method == SIPMethodsEnum.INVITE && !IsInvite)
                {
                    IsCallCancelled = false;
                    IsInvite = true;

                    Console.WriteLine($"Incoming call request: {localSipEndPoint}<-{remoteEndPoint} {sipRequest.URI}.");

                    var tryingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Trying, null);
                    await _sipTransport.SendResponseAsync(tryingResponse);

                    string callerNumber = Ext.ParseCallerNumber(sipRequest.URI.ToString());
                    //Ext.WriteLog($"Caller number: {callerNumber}", ConsoleColor.Green);

                    //bool isNumberValid = Ext.CheckNuberIsValid(callerNumber);
                    bool isNumberValid = App.CheckNuberIsValid(callerNumber);

                    if (!isNumberValid)
                    {
                        Ext.WriteLog($"{callerNumber} is not registered in App", ConsoleColor.Gray);

                        //decline the call
                        var unavailResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.ServiceUnavailable, null);
                        await _sipTransport.SendResponseAsync(unavailResponse);

                        Console.WriteLine($"Sent {SIPResponseStatusCodesEnum.ServiceUnavailable} to {sipRequest.URI}");
                    }

                    else
                    {
                        Ext.WriteLog($"{callerNumber} is registered in App", ConsoleColor.Green);

                        string host = Ext.ParseHost(sipRequest.URI.ToString());
                        _sipTransport.ContactHost = host;

                        var ua = new SIPUserAgent(_sipTransport, null);

                        ua.OnCallHungup += OnHangup;

                        ua.ServerCallCancelled += (_) =>
                        {
                            Console.WriteLine("Incoming call cancelled by remote party.");
                            App.EndCall();

                            IsCallCancelled = true;
                        };

                        ua.ServerCallRingTimeout += (uas) =>
                        {
                            Console.WriteLine($"Incoming call timed out in {uas.ClientTransaction.TransactionState} state waiting for client ACK, terminating.");
                            App.EndCall();
                            ua.Hangup();

                            IsCallCancelled = true;
                        };

                        if (!IsCallCancelled)
                        {
                            var uas = ua.AcceptCall(sipRequest);

                            RtpSession = CreateRtpSession(ua, sipRequest.URI.User);

                            var ringingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ringing, null);
                            await _sipTransport.SendResponseAsync(ringingResponse);

                            Ext.WriteLog("Calling to whatsapp...", ConsoleColor.Blue);
                            App.OpenChat(callerNumber);
                            App.CallCurrentContact();

                            while (!App.IsRinging())
                            {
                                await Task.Delay(100);
                            }

                            while (!App.IsCallActive())
                            {
                                await Task.Delay(100);

                                if (App.IsCallDeclined())
                                {
                                    Ext.WriteLog("App call unanswered or declined", ConsoleColor.Red);
                                    IsCallCancelled = true;

                                    //decline the call
                                    var busyResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.BusyHere, null);
                                    await _sipTransport.SendResponseAsync(busyResponse);

                                    break;
                                }
                            }

                            if (!IsCallCancelled)
                            {
                                Ext.WriteLog("App answered", ConsoleColor.Green);

                                //await ua.Answer(uas, rtpSession);
                                var isAnswered = ua.Answer(uas, RtpSession).Result;

                                if (isAnswered)
                                {
                                    Ext.WriteLog("SIP answered", ConsoleColor.Blue);
                                }

                                if (ua.IsCallActive)
                                {
                                    await RtpSession.Start();
                                    Ext.WriteLog("RTP session started", ConsoleColor.Green);

                                    _calls.TryAdd(ua.Dialogue.CallId, ua);
                                    //Ext.WriteLog($"Caller id = {ua.Dialogue.CallId}", ConsoleColor.Gray);
                                }

                                while (App.IsCallingScreen())
                                {
                                    await Task.Delay(100);
                                }

                                EndCall(ua.Dialogue);
                            }
                        }
                    }

                    IsInvite = false;
                    IsCallEnded = true;

                    App.ReopenApp();
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

                    if (App.IsCallingScreen())
                    {
                        App.EndCall();
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


        private void EndCall(SIPDialogue? dialogue)
        {
            RtpSession?.Close("Call ended");

            if (dialogue == null) return;
            string callId = dialogue.CallId;

            if (_calls.TryGetValue(callId, out var userAgent))
            {
                userAgent.Hangup();
                _calls.TryRemove(callId, out _);
                Console.WriteLine("Call ended with BYE request.");
            }
        }

        private void OnHangup(SIPDialogue? dialogue)
        {
            if (dialogue == null) return;
            string callId = dialogue.CallId;

            if (!_calls.ContainsKey(callId)) return;

            if (_calls.TryRemove(callId, out var ua))
            {
                ua.Close();
            }
        }

        private VoIPMediaSession CreateRtpSession(SIPUserAgent ua, string dst)
        {
            var rtpAudioSession = new VoIPMediaSession(new MediaEndPoints
            {
                AudioSource = _audioEndPoint,
                AudioSink = _audioEndPoint
            });

            rtpAudioSession.AcceptRtpFromAny = true;

            rtpAudioSession.OnTimeout += (_) =>
            {
                Console.WriteLine(ua.Dialogue != null
                    ? $"RTP timeout on call with {ua.Dialogue.RemoteTarget}, hanging up."
                    : "RTP timeout on incomplete call, closing RTP session.");

                ua.Hangup();
            };

            return rtpAudioSession;
        }

        public void StopService()
        {
            _sipTransport.Shutdown();
            _stunClient?.Stop();
        }
    }
}
