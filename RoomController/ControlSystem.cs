using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;

namespace RoomController
{
    public class ControlSystem : CrestronControlSystem
    {
        // ------------------------------------------------------------
        // NETWORK TOPOLOGY / IP SCHEMA (documentation only)
        // Netgear M4250 Port 1 -> RMC4                    -> 192.168.50.58
        // Netgear M4250 Port 2 -> TS-770                  -> 192.168.50.151
        // Netgear M4250 Port 3 -> Q-SYS Core Nano         -> 192.168.50.253
        // Netgear M4250 Port 4 -> Yealink MeetingBar A20  -> 192.168.50.190
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // DEVICE CONFIGURATION
        // ------------------------------------------------------------
        private Tsw770 _ts770;
        private const uint TS770_IPID = 0x03;

        private const string QSYS_IP = "192.168.50.253";
        private const int QSYS_PORT = 1702;

        private const string A20_IP = "192.168.50.190";
        private const int A20_PORT = 5000; // Placeholder only

        private TCPClient _qsysClient;
        private TCPClient _a20Client;

        // ------------------------------------------------------------
        // TOUCHPANEL JOIN MAP
        // ------------------------------------------------------------
        private const uint JOIN_ROOM_ON     = 1;
        private const uint JOIN_ROOM_OFF    = 2;
        private const uint JOIN_VOL_UP      = 3;
        private const uint JOIN_VOL_DOWN    = 4;
        private const uint JOIN_MUTE_TOGGLE = 5;
        private const uint JOIN_TEAMS_HOME  = 6;

        private const uint FB_QSYS_ONLINE   = 101;
        private const uint FB_A20_ONLINE    = 102;
        private const uint FB_SYSTEM_READY  = 103;

        private const uint TXT_STATUS       = 1;

        public ControlSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Thread config error: {0}", ex.Message);
            }
        }

        public override void InitializeSystem()
        {
            try
            {
                UpdateStatusText("System initializing...");
                RegisterTouchpanel();
                InitializeQsys();
                InitializeA20();
                UpdateSystemReady();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeSystem error: {0}", ex.Message);
            }
        }

        private void RegisterTouchpanel()
        {
            try
            {
                _ts770 = new Tsw770(TS770_IPID, this);
                _ts770.SigChange += Ts770SigChange;

                var result = _ts770.Register();
                if (result == eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Notice("TS-770 registered successfully on IPID 0x{0:X2}", TS770_IPID);
                    UpdateStatusText("TS-770 registered");
                }
                else
                {
                    ErrorLog.Error("TS-770 registration failed: {0}", result);
                    UpdateStatusText("TS-770 registration failed");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("RegisterTouchpanel error: {0}", ex.Message);
            }
        }

        private void InitializeQsys()
        {
            try
            {
                _qsysClient = new TCPClient(QSYS_IP, QSYS_PORT, 4096);
                var result = _qsysClient.ConnectToServer();

                if (result == SocketErrorCodes.SOCKET_OK)
                {
                    ErrorLog.Notice("Connected to Q-SYS Core Nano at {0}:{1}", QSYS_IP, QSYS_PORT);
                    SetBoolFeedback(FB_QSYS_ONLINE, true);
                    UpdateStatusText("Q-SYS online");
                }
                else
                {
                    ErrorLog.Error("Q-SYS connection failed: {0}", result);
                    SetBoolFeedback(FB_QSYS_ONLINE, false);
                    UpdateStatusText("Q-SYS offline");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeQsys error: {0}", ex.Message);
                SetBoolFeedback(FB_QSYS_ONLINE, false);
            }
        }

        private void InitializeA20()
        {
            try
            {
                _a20Client = new TCPClient(A20_IP, A20_PORT, 4096);
                var result = _a20Client.ConnectToServer();

                if (result == SocketErrorCodes.SOCKET_OK)
                {
                    ErrorLog.Notice("Connected to Yealink A20 at {0}:{1}", A20_IP, A20_PORT);
                    SetBoolFeedback(FB_A20_ONLINE, true);
                    UpdateStatusText("A20 online");
                }
                else
                {
                    ErrorLog.Error("A20 connection failed: {0}", result);
                    SetBoolFeedback(FB_A20_ONLINE, false);
                    UpdateStatusText("A20 offline");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeA20 error: {0}", ex.Message);
                SetBoolFeedback(FB_A20_ONLINE, false);
            }
        }

        private void Ts770SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            try
            {
                if (args.Sig.Type != eSigType.Bool)
                    return;

                if (!args.Sig.BoolValue)
                    return;

                switch (args.Sig.Number)
                {
                    case JOIN_ROOM_ON:
                        RoomOn();
                        break;

                    case JOIN_ROOM_OFF:
                        RoomOff();
                        break;

                    case JOIN_VOL_UP:
                        VolumeUp();
                        break;

                    case JOIN_VOL_DOWN:
                        VolumeDown();
                        break;

                    case JOIN_MUTE_TOGGLE:
                        MuteToggle();
                        break;

                    case JOIN_TEAMS_HOME:
                        TeamsHome();
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Ts770SigChange error: {0}", ex.Message);
            }
        }

        private void RoomOn()
        {
            UpdateStatusText("Room On");

            SendQsys("csp \"RoomPower\" 1\n");
            SendQsys("csv \"MainMute\" 0\n");

            SendA20("ROOM_ON\r\n");

            UpdateSystemReady();
        }

        private void RoomOff()
        {
            UpdateStatusText("Room Off");

            SendQsys("csp \"RoomPower\" 0\n");
            SendQsys("csv \"MainMute\" 1\n");

            SendA20("ROOM_OFF\r\n");

            UpdateSystemReady();
        }

        private void VolumeUp()
        {
            UpdateStatusText("Volume Up");
            SendQsys("css \"MainLevel\" 1 1\n");
        }

        private void VolumeDown()
        {
            UpdateStatusText("Volume Down");
            SendQsys("css \"MainLevel\" -1 1\n");
        }

        private void MuteToggle()
        {
            UpdateStatusText("Mute Toggle");
            SendQsys("ct \"MainMute\"\n");
        }

        private void TeamsHome()
        {
            UpdateStatusText("Teams Home");
            SendA20("TEAMS_HOME\r\n");
        }

        private void SendQsys(string command)
        {
            try
            {
                if (_qsysClient != null && _qsysClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    byte[] data = Encoding.ASCII.GetBytes(command);
                    _qsysClient.SendData(data, data.Length);
                    ErrorLog.Notice("Q-SYS >> {0}", command.Trim());
                    SetBoolFeedback(FB_QSYS_ONLINE, true);
                }
                else
                {
                    ErrorLog.Warn("Q-SYS not connected. Command not sent.");
                    SetBoolFeedback(FB_QSYS_ONLINE, false);
                    UpdateStatusText("Q-SYS offline");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SendQsys error: {0}", ex.Message);
                SetBoolFeedback(FB_QSYS_ONLINE, false);
            }
        }

        private void SendA20(string command)
        {
            try
            {
                if (_a20Client != null && _a20Client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    byte[] data = Encoding.ASCII.GetBytes(command);
                    _a20Client.SendData(data, data.Length);
                    ErrorLog.Notice("A20 >> {0}", command.Trim());
                    SetBoolFeedback(FB_A20_ONLINE, true);
                }
                else
                {
                    ErrorLog.Warn("A20 not connected. Command not sent.");
                    SetBoolFeedback(FB_A20_ONLINE, false);
                    UpdateStatusText("A20 offline");
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SendA20 error: {0}", ex.Message);
                SetBoolFeedback(FB_A20_ONLINE, false);
            }
        }

        private void SetBoolFeedback(uint join, bool value)
        {
            try
            {
                if (_ts770 != null)
                    _ts770.BooleanInput[join].BoolValue = value;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SetBoolFeedback error: {0}", ex.Message);
            }
        }

        private void UpdateStatusText(string message)
        {
            try
            {
                if (_ts770 != null)
                    _ts770.StringInput[TXT_STATUS].StringValue = message;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("UpdateStatusText error: {0}", ex.Message);
            }
        }

        private void UpdateSystemReady()
        {
            bool qsysOnline = _qsysClient != null &&
                              _qsysClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;

            bool a20Online = _a20Client != null &&
                             _a20Client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;

            SetBoolFeedback(FB_SYSTEM_READY, qsysOnline && a20Online);
        }
    }
}
