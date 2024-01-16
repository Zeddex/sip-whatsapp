using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;

namespace SipWA
{
    public class StunClient
    {
        private Timer updateTimer;

        private readonly TimeSpan updateIntervalNormal = TimeSpan.FromMinutes(1);

        private readonly TimeSpan updateIntervalShort = TimeSpan.FromSeconds(5);

        private readonly string m_stunServerHostname;

        private volatile bool m_stop;

        public event Action<IPAddress> PublicIPAddressDetected;

        public StunClient(string stunServerHostname)
        {
            m_stunServerHostname = stunServerHostname;
        }

        public void Run()
        {
            m_stop = false;

            updateTimer = new Timer(e =>
            {
                if (!m_stop)
                {
                    var publicIPAddress = GetPublicIPAddress();

                    if (publicIPAddress != null)
                    {
                        PublicIPAddressDetected?.Invoke(publicIPAddress);
                    }

                    var timerInterval = (publicIPAddress == null) ? updateIntervalShort : updateIntervalNormal;
                    updateTimer.Change(timerInterval, timerInterval);
                }
            }, null, TimeSpan.Zero, updateIntervalNormal);

            Ext.WriteLog("STUN client started.", ConsoleColor.Cyan);
        }

        public void Stop()
        {
            m_stop = true;
            updateTimer.Change(Timeout.Infinite, Timeout.Infinite);

            Ext.WriteLog("STUN client stopped.", ConsoleColor.Cyan);
        }

        private IPAddress GetPublicIPAddress()
        {
            try
            {
                var publicIP = STUNClient.GetPublicIPAddress(m_stunServerHostname);

                if (publicIP != null)
                {
                    Ext.WriteLog($"The STUN client was able to determine the public IP address as {publicIP}", ConsoleColor.Cyan);
                }
                else
                {
                    Ext.WriteLog("The STUN client could not determine the public IP address.", ConsoleColor.Cyan);
                }

                return publicIP;
            }
            catch (Exception getAddrExcp)
            {
                Ext.WriteLog("Exception GetPublicIPAddress. " + getAddrExcp.Message, ConsoleColor.Red);
                return null;
            }
        }
    }
}