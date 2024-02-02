using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SIPSorcery.SIP;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using System.Net;

namespace SipIntercept
{
    internal class StunServer
    {
        private readonly STUNListener _primarySTUNListener;
        private readonly STUNListener _secondarySTUNListener;
        private readonly STUNServer _stunServer;

        public StunServer()
        {
            // STUN servers need two separate end points to listen on.
            IPEndPoint primaryEndPoint = new IPEndPoint(IPAddress.Any, 3478);
            IPEndPoint secondaryEndPoint = new IPEndPoint(IPAddress.Any, 3479);

            // Create the two STUN listeners and wire up the STUN server.
            _primarySTUNListener = new STUNListener(primaryEndPoint);
            _secondarySTUNListener = new STUNListener(secondaryEndPoint);
            _stunServer = new STUNServer(primaryEndPoint, _primarySTUNListener.Send, secondaryEndPoint, _secondarySTUNListener.Send);
            _primarySTUNListener.MessageReceived += _stunServer.STUNPrimaryReceived;
            _secondarySTUNListener.MessageReceived += _stunServer.STUNSecondaryReceived;

            // Optional. Provides verbose logs of STUN server activity.
            EnableVerboseLogs(_stunServer);

            Ext.WriteLog("STUN server successfully initialised.", ConsoleColor.Cyan);
        }

        public void StopStun()
        {
            _primarySTUNListener.Close();
            _secondarySTUNListener.Close();
            _stunServer.Stop();
        }

        private void EnableVerboseLogs(STUNServer stunServer)
        {
            stunServer.STUNPrimaryRequestInTraceEvent += (localEndPoint, fromEndPoint, stunMessage) =>
            {
                Ext.WriteLog($"pri recv {localEndPoint}<-{fromEndPoint}: {stunMessage.ToString()}", ConsoleColor.White);
            };

            stunServer.STUNSecondaryRequestInTraceEvent += (localEndPoint, fromEndPoint, stunMessage) =>
            {
                Ext.WriteLog($"sec recv {localEndPoint}<-{fromEndPoint}: {stunMessage.ToString()}", ConsoleColor.White);
            };

            stunServer.STUNPrimaryResponseOutTraceEvent += (localEndPoint, fromEndPoint, stunMessage) =>
            {
                Ext.WriteLog($"pri send {localEndPoint}->{fromEndPoint}: {stunMessage.ToString()}", ConsoleColor.White);
            };

            stunServer.STUNSecondaryResponseOutTraceEvent += (localEndPoint, fromEndPoint, stunMessage) =>
            {
                Ext.WriteLog($"sec send {localEndPoint}->{fromEndPoint}: {stunMessage.ToString()}", ConsoleColor.White);
            };
        }

        private IPAddress GetPublicIPAddress()
        {
            try
            {
                var publicIP = STUNClient.GetPublicIPAddress("m_stunServerHostname");

                if (publicIP != null)
                {
                    Ext.WriteLog($"The STUN client was able to determine the public IP address as {publicIP}", ConsoleColor.White);
                }
                else
                {
                    Ext.WriteLog("The STUN client could not determine the public IP address.", ConsoleColor.White);
                }

                return publicIP;
            }
            catch (Exception getAddrExcp)
            {
                Ext.WriteLog("Exception GetPublicIPAddress. " + getAddrExcp.Message, ConsoleColor.White);
                return null;
            }
        }
    }
}
