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
using Spectre.Console;
using WebSocketSharp;

namespace SipIntercept
{
    public class NoExtraAppHandler
    {
        event EventHandler<EventArgs> OnCallStarted;
        event EventHandler<EventArgs> OnActiveCall;
        event EventHandler<EventArgs> OnValidNumber;
        event EventHandler<EventArgs> OnCallEnded;
        event EventHandler<EventArgs> OnCallDeclined;

        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public int Expire { get; set; }
        public bool IsCallCancelled { get; set; }
        public List<AudioCodecsEnum> Codecs { get; set; }
        public VoIPMediaSession RtpSession { get; set; }

        private StunClient _stunClient;
        private readonly WindowsAudioEndPoint _audioEndPoint;
        private readonly SIPTransport _sipTransport;
        private readonly ConcurrentDictionary<string, SIPUserAgent> _calls;

        public NoExtraAppHandler(string user, string password, string domain, int port = 5060, int expire = 120)
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

        public void Init(List<AudioCodecsEnum> codecs, string? stunServer = null)
        {
            Codecs = codecs;

            if (stunServer != null)
                InitializeStunClient(stunServer);

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

                    if (!AnsiConsole.Confirm("Accept call?"))
                    {
                        //decline the call
                        var unavailResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.ServiceUnavailable, null);
                        await _sipTransport.SendResponseAsync(unavailResponse);

                        AnsiConsole.MarkupLine($"Sent {SIPResponseStatusCodesEnum.ServiceUnavailable} to {sipRequest.URI}");
                    }

                    else
                    {
                        // STUN
                        //string host = Ext.ParseHost(sipRequest.URI.ToString());
                        //_sipTransport.ContactHost = host;

                        var ua = new SIPUserAgent(_sipTransport, null);

                        ua.OnCallHungup += OnHangup;

                        ua.ServerCallCancelled += (_) =>
                        {
                            Console.WriteLine("Incoming call cancelled by remote party.");
                            EndCall(ua.Dialogue);

                            IsCallCancelled = true;
                        };

                        ua.ServerCallRingTimeout += (uas) =>
                        {
                            Console.WriteLine($"Incoming call timed out in {uas.ClientTransaction.TransactionState} state waiting for client ACK, terminating.");
                            EndCall(ua.Dialogue);
                            ua.Hangup();

                            IsCallCancelled = true;
                        };

                        if (!IsCallCancelled)
                        {
                            var uas = ua.AcceptCall(sipRequest);

                            RtpSession = CreateRtpSession(ua, sipRequest.URI.User);
                            //var rtpSession = CreateRtpSessionTestSound(ua, sipRequest.URI.User);

                            var ringingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ringing, null);
                            await _sipTransport.SendResponseAsync(ringingResponse);

                            await ua.Answer(uas, RtpSession);

                            if (ua.IsCallActive)
                            {
                                await RtpSession.Start();
                                Ext.WriteLog("RTP session started", ConsoleColor.Green);

                                _calls.TryAdd(ua.Dialogue.CallId, ua);
                            }

                            //Ext.WriteLog("Press SPACE to end the call", ConsoleColor.Red);

                            //bool exitKey = false;
                            //Task task = Task.Run(() =>
                            //{
                            //    while (Console.ReadKey().Key != ConsoleKey.Spacebar)
                            //    {
                            //        exitKey = true;

                            //        if (exitKey == false)
                            //            break;
                            //    }
                            //});

                            //while (!exitKey)
                            //{
                            //    await Task.Delay(100);
                            //}

                            while (true)
                            {
                                await Task.Delay(100);
                            }

                            EndCall(ua.Dialogue, RtpSession);
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

                        EndCall(ua.Dialogue, RtpSession);

                        ua.Close();
                    }
                    else
                    {
                        // The call does not exist or is already terminated.
                        var notFoundResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                        await _sipTransport.SendResponseAsync(notFoundResponse);
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

        private void EndCall(SIPDialogue dialogue, VoIPMediaSession rtpSession = null)
        {
            rtpSession?.Close("Call ended");

            if (dialogue != null)
            {
                string callId = dialogue.CallId;

                if (_calls.TryGetValue(callId, out var userAgent))
                {
                    userAgent.Hangup();
                    _calls.TryRemove(callId, out _);
                    Console.WriteLine("Call ended with BYE request.");
                }
                else
                {
                    //Console.WriteLine("Call not found.");
                }
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

            var rtpAudioSession = new VoIPMediaSession(new MediaEndPoints { AudioSource = audioExtrasSource });

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
    }
}
