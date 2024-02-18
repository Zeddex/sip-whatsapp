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
using Spectre.Console;

namespace SipIntercept
{
    public class NoExtraAppHandler
    {
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Domain { get; set; }
        public int Expire { get; set; }
        public bool IsCallCancelled { get; set; }
        public bool IsCallEnded { get; set; }
        public bool IsInvite { get; set; }
        public List<AudioCodecsEnum> Codecs { get; set; }
        public VoIPMediaSession? RtpSession { get; set; }

        private StunClient _stunClient;
        private readonly WindowsAudioEndPoint _audioEndPoint;
        private readonly SIPTransport _sipTransport;
        private readonly ConcurrentDictionary<string, SIPUserAgent> _calls;


        public NoExtraAppHandler(string user, string password, string domain, int port = 5060, int expire = 120)
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

        public void Init(string? stunServer = null)
        {
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

        private async Task OnRequest(SIPEndPoint localSipEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                if (sipRequest.Method == SIPMethodsEnum.INVITE && !IsInvite)
                {
                    IsInvite = true;
                    IsCallCancelled = false;

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

                            var ringingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ringing, null);
                            await _sipTransport.SendResponseAsync(ringingResponse);

                            await ua.Answer(uas, RtpSession);

                            if (ua.IsCallActive)
                            {
                                await RtpSession.Start();
                                Ext.WriteLog("RTP session started", ConsoleColor.Green);

                                _calls.TryAdd(ua.Dialogue.CallId, ua);
                            }

                            while (ua.IsCallActive)
                            {
                                await Task.Delay(100);
                            }

                            EndCall(ua.Dialogue);
                        }
                    }

                    IsInvite = false;
                    IsCallEnded = true;
                }
                else if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    if (_calls.TryRemove(sipRequest.Header.CallId, out var ua))
                    {
                        var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                        await _sipTransport.SendResponseAsync(okResponse);
                        Ext.WriteLog("Call ended\n", ConsoleColor.Blue);

                        EndCall(ua.Dialogue);

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

        #region looped

        //private async Task OnRequest(SIPEndPoint localSipEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        //{
        //    try
        //    {
        //        if (sipRequest.Method == SIPMethodsEnum.INVITE && !IsInvite)
        //        {
        //            IsInvite = true;
        //            IsCallCancelled = false;

        //            Console.WriteLine($"Incoming call request: {localSipEndPoint}<-{remoteEndPoint} {sipRequest.URI}.");

        //            var tryingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Trying, null);
        //            await _sipTransport.SendResponseAsync(tryingResponse);
        //            //Ext.WriteLog("\n100 Trying sent\n", ConsoleColor.Blue);

        //            if (!AnsiConsole.Confirm("Accept call?"))
        //            {
        //                //decline the call
        //                var unavailResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.ServiceUnavailable, null);
        //                await _sipTransport.SendResponseAsync(unavailResponse);

        //                AnsiConsole.MarkupLine($"Sent {SIPResponseStatusCodesEnum.ServiceUnavailable} to {sipRequest.URI}");
        //            }

        //            else
        //            {
        //                // STUN
        //                //string host = Ext.ParseHost(sipRequest.URI.ToString());
        //                //_sipTransport.ContactHost = host;

        //                var ua = new SIPUserAgent(_sipTransport, null);

        //                ua.OnCallHungup += OnHangup;

        //                ua.ServerCallCancelled += (_) =>
        //                {
        //                    Console.WriteLine("Incoming call cancelled by remote party.");
        //                    EndCall(ua.Dialogue);

        //                    IsCallCancelled = true;
        //                };

        //                ua.ServerCallRingTimeout += (uas) =>
        //                {
        //                    Console.WriteLine($"Incoming call timed out in {uas.ClientTransaction.TransactionState} state waiting for client ACK, terminating.");
        //                    EndCall(ua.Dialogue);
        //                    ua.Hangup();

        //                    IsCallCancelled = true;
        //                };

        //                if (!IsCallCancelled)
        //                {
        //                    var uas = ua.AcceptCall(sipRequest);

        //                    RtpSession = CreateRtpSession(ua, sipRequest.URI.User);
        //                    //var rtpSession = CreateRtpSessionTestSound(ua, sipRequest.URI.User);

        //                    var ringingResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ringing, null);
        //                    await _sipTransport.SendResponseAsync(ringingResponse);
        //                    //Ext.WriteLog("180 Ringing sent\n", ConsoleColor.Blue);

        //                    await ua.Answer(uas, RtpSession);

        //                    if (ua.IsCallActive)
        //                    {
        //                        await RtpSession.Start();
        //                        Ext.WriteLog("RTP session started", ConsoleColor.Green);

        //                        _calls.TryAdd(ua.Dialogue.CallId, ua);
        //                    }

        //                    //Ext.WriteLog("Press SPACE to end the call", ConsoleColor.Red);

        //                    //bool exitKey = false;
        //                    //Task task = Task.Run(() =>
        //                    //{
        //                    //    while (Console.ReadKey().Key != ConsoleKey.Spacebar)
        //                    //    {
        //                    //        exitKey = true;

        //                    //        if (exitKey == false)
        //                    //            break;
        //                    //    }
        //                    //});

        //                    //while (!exitKey)
        //                    //{
        //                    //    await Task.Delay(100);
        //                    //}

        //                    while (ua.IsCallActive)
        //                    {
        //                        await Task.Delay(100);
        //                    }

        //                    EndCall(ua.Dialogue);
        //                }
        //            }

        //            IsInvite = false;
        //        }
        //        else if (sipRequest.Method == SIPMethodsEnum.BYE)
        //        {
        //            if (_calls.TryRemove(sipRequest.Header.CallId, out var ua))
        //            {
        //                var okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
        //                await _sipTransport.SendResponseAsync(okResponse);
        //                Ext.WriteLog("Call ended\n", ConsoleColor.Blue);

        //                EndCall(ua.Dialogue);

        //                ua.Close();
        //            }
        //            else
        //            {
        //                // The call does not exist or is already terminated.
        //                var notFoundResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
        //                await _sipTransport.SendResponseAsync(notFoundResponse);
        //            }
        //        }
        //        else if (sipRequest.Method == SIPMethodsEnum.SUBSCRIBE)
        //        {
        //            var notAllowededResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
        //            await _sipTransport.SendResponseAsync(notAllowededResponse);
        //        }
        //        else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
        //        {
        //            var optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
        //            await _sipTransport.SendResponseAsync(optionsResponse);
        //        }
        //        else if (sipRequest.Method == SIPMethodsEnum.ACK)
        //        {
        //            var optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
        //            await _sipTransport.SendResponseAsync(optionsResponse);
        //            Ext.WriteLog($"SIP Status: {sipRequest.Method}", ConsoleColor.Red);
        //        }
        //    }
        //    catch (Exception reqExcp)
        //    {
        //        Console.WriteLine($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
        //    }
        //}

        #endregion

        private void EndCall(SIPDialogue? dialogue)
        //private void EndCall(SIPDialogue dialogue, VoIPMediaSession rtpSession = null)
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

            //_audioEndPoint.RestrictFormats(format => Codecs.Contains(format.Codec));

            //var codecs = new List<AudioCodecsEnum> { AudioCodecsEnum.PCMA };
            //_audioEndPoint.RestrictFormats(format => codecs.Contains(format.Codec));

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

        public void StopService()
        {
            _sipTransport.Shutdown();
            _stunClient?.Stop();
        }
    }
}
