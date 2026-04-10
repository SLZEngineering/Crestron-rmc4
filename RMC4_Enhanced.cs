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
    // ----------------------------------------------------------------
    // ROOM EVENT — telemetry record emitted for every system event
    // All fields correspond to the required telemetry schema.
    // ----------------------------------------------------------------
    public class RoomEvent
    {
        // ---- Core identity
        public string event_id                  { get; set; }
        public string event_type                { get; set; }
        public string event_value               { get; set; }
        public string timestamp_utc             { get; set; }
        public string timestamp_local           { get; set; }

        // ---- Processor / site context
        public string processor_name            { get; set; }
        public string processor_ip              { get; set; }
        public string room_name                 { get; set; }
        public string room_id                   { get; set; }
        public string site_name                 { get; set; }
        public string building_name             { get; set; }

        // ---- Source routing context
        public string source_device             { get; set; }
        public string source_device_type        { get; set; }
        public string source_device_ip          { get; set; }
        public string source_input              { get; set; }
        public string source_output             { get; set; }

        // ---- Session context
        public string session_id                { get; set; }
        public string session_state             { get; set; }
        public string session_start_time        { get; set; }
        public string session_end_time          { get; set; }
        public int    session_duration_seconds  { get; set; }
        public bool   session_valid             { get; set; }

        // ---- Room state snapshot
        public bool   occupancy_state           { get; set; }
        public bool   byod_state                { get; set; }
        public bool   usb_state                 { get; set; }
        public bool   input_sync_state          { get; set; }
        public bool   display_state             { get; set; }
        public bool   system_power_state        { get; set; }
        public bool   source_route_state        { get; set; }

        // ---- Device health snapshot
        public string device_health_state       { get; set; }
        public int    offline_incident_count    { get; set; }
        public string outage_start_time         { get; set; }
        public string outage_end_time           { get; set; }
        public int    outage_duration_seconds   { get; set; }

        // ---- Metadata
        public bool   user_action_detected      { get; set; }
        public string notes                     { get; set; }
        public string event_origin              { get; set; }
        public string firmware_version          { get; set; }
        public string program_version           { get; set; }
    }

    // ----------------------------------------------------------------
    // CONTROL SYSTEM
    // ----------------------------------------------------------------
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
        private const uint   TS770_IPID  = 0x03;

        private const string QSYS_IP     = "192.168.50.253";
        private const int    QSYS_PORT   = 1702;

        private const string A20_IP      = "192.168.50.190";
        private const int    A20_PORT    = 5000;

        private TCPClient _qsysClient;
        private TCPClient _a20Client;

        // ------------------------------------------------------------
        // PROGRAM / SITE METADATA
        // ------------------------------------------------------------
        private const string PROGRAM_VERSION  = "2.0.0";
        private const string ROOM_NAME        = "Conference Room";
        private const string ROOM_ID          = "CR-001";
        private const string SITE_NAME        = "Main Campus";
        private const string BUILDING_NAME    = "Building A";

        // ------------------------------------------------------------
        // TOUCHPANEL JOIN MAP — DIGITAL INPUTS (button presses)
        // ------------------------------------------------------------
        // Original room controls (joins 1-6)
        private const uint JOIN_ROOM_ON               = 1;
        private const uint JOIN_ROOM_OFF              = 2;
        private const uint JOIN_VOL_UP                = 3;
        private const uint JOIN_VOL_DOWN              = 4;
        private const uint JOIN_MUTE_TOGGLE           = 5;
        private const uint JOIN_TEAMS_HOME            = 6;

        // Occupancy (joins 10-11)
        private const uint JOIN_OCCUPANCY_ON          = 10;
        private const uint JOIN_OCCUPANCY_OFF         = 11;

        // BYOD (joins 12-13)
        private const uint JOIN_BYOD_ENTER            = 12;
        private const uint JOIN_BYOD_EXIT             = 13;

        // USB (joins 14-15)
        private const uint JOIN_USB_CONNECTED         = 14;
        private const uint JOIN_USB_DISCONNECTED      = 15;

        // Input sync (joins 16-17)
        private const uint JOIN_INPUT_SYNC_ON         = 16;
        private const uint JOIN_INPUT_SYNC_OFF        = 17;

        // Display (joins 18-19)
        private const uint JOIN_DISPLAY_ON            = 18;
        private const uint JOIN_DISPLAY_OFF           = 19;

        // Source routing (joins 20-21)
        private const uint JOIN_SOURCE_ROUTE_ACTIVE   = 20;
        private const uint JOIN_SOURCE_ROUTE_INACTIVE = 21;

        // Presentation (joins 22-23)
        private const uint JOIN_PRESENTATION_START    = 22;
        private const uint JOIN_PRESENTATION_STOP     = 23;

        // Manual heartbeat trigger (join 24)
        private const uint JOIN_HEARTBEAT_TRIGGER     = 24;

        // Device online/offline reported by external signals (joins 30-43)
        private const uint JOIN_PROCESSOR_ONLINE      = 30;
        private const uint JOIN_PROCESSOR_OFFLINE     = 31;
        private const uint JOIN_DISPLAY_ONLINE        = 32;
        private const uint JOIN_DISPLAY_OFFLINE       = 33;
        private const uint JOIN_CAMERA_ONLINE         = 34;
        private const uint JOIN_CAMERA_OFFLINE        = 35;
        private const uint JOIN_MIC_ONLINE            = 36;
        private const uint JOIN_MIC_OFFLINE           = 37;
        private const uint JOIN_SPEAKER_ONLINE        = 38;
        private const uint JOIN_SPEAKER_OFFLINE       = 39;
        private const uint JOIN_SWITCHER_ONLINE       = 40;
        private const uint JOIN_SWITCHER_OFFLINE      = 41;
        private const uint JOIN_USB_BRIDGE_ONLINE     = 42;
        private const uint JOIN_USB_BRIDGE_OFFLINE    = 43;

        // ------------------------------------------------------------
        // TOUCHPANEL JOIN MAP — DIGITAL OUTPUTS (feedback)
        // ------------------------------------------------------------
        private const uint FB_QSYS_ONLINE             = 101;
        private const uint FB_A20_ONLINE              = 102;
        private const uint FB_SYSTEM_READY            = 103;
        private const uint FB_ROOM_OCCUPIED           = 104;
        private const uint FB_BYOD_ACTIVE             = 105;
        private const uint FB_USB_CONNECTED           = 106;
        private const uint FB_INPUT_SYNC              = 107;
        private const uint FB_DISPLAY_ON              = 108;
        private const uint FB_SYSTEM_ON               = 109;
        private const uint FB_SOURCE_ROUTE_ACTIVE     = 110;
        private const uint FB_PRESENTATION_ACTIVE     = 111;
        private const uint FB_SESSION_VALID           = 112;
        private const uint FB_PROCESSOR_HEALTHY       = 120;
        private const uint FB_DISPLAY_HEALTHY         = 121;
        private const uint FB_CAMERA_HEALTHY          = 122;
        private const uint FB_MIC_HEALTHY             = 123;
        private const uint FB_SPEAKER_HEALTHY         = 124;
        private const uint FB_SWITCHER_HEALTHY        = 125;
        private const uint FB_USB_BRIDGE_HEALTHY      = 126;

        // ------------------------------------------------------------
        // TOUCHPANEL JOIN MAP — SERIAL OUTPUTS (text)
        // ------------------------------------------------------------
        private const uint TXT_STATUS                 = 1;
        private const uint TXT_SESSION_ID             = 2;
        private const uint TXT_LAST_EVENT             = 3;
        private const uint TXT_SESSION_START          = 4;
        private const uint TXT_DEVICE_HEALTH          = 5;

        // ------------------------------------------------------------
        // TIMER INTERVALS
        // ------------------------------------------------------------
        private const long SESSION_GRACE_MS           = 30000;   // 30 s
        private const long IDLE_TIMEOUT_MS            = 300000;  // 5 min
        private const long HEARTBEAT_INTERVAL_MS      = 60000;   // 1 min

        // ------------------------------------------------------------
        // STATE VARIABLES
        // ------------------------------------------------------------
        private bool   RoomOccupied;
        private bool   BYODActive;
        private bool   USBConnected;
        private bool   InputSyncActive;
        private bool   DisplayOn;
        private bool   SystemOn;
        private bool   SourceRouteActive;
        private bool   PresentationActive;

        // Session tracking
        private string CurrentSessionID;
        private string CurrentSessionStart;
        private string CurrentSessionEnd;
        private bool   CurrentSessionValid;

        // Timers
        private CTimer SessionGraceTimer;
        private CTimer IdleTimeoutTimer;
        private CTimer HeartbeatTimer;

        // Device health
        private bool ProcessorHealthy;
        private bool DisplayHealthy;
        private bool CameraHealthy;
        private bool MicHealthy;
        private bool SpeakerHealthy;
        private bool SwitcherHealthy;
        private bool USBBridgeHealthy;

        // Event timestamps
        private string OccupancyStartTime;
        private string OccupancyEndTime;
        private string BYODStartTime;
        private string BYODEndTime;
        private string USBConnectTime;
        private string USBDisconnectTime;
        private string SystemStartTime;
        private string SystemEndTime;

        // Outage / incident tracking
        private int    _OfflineIncidentCount;
        private string _OutageStartTime;

        // Event sequencing
        private int    _EventCounter;
        private readonly object _EventLock = new object();

        // ============================================================
        // CONSTRUCTOR
        // ============================================================
        public ControlSystem() : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                // Assume processor is healthy at boot
                ProcessorHealthy  = true;
                DisplayHealthy    = true;
                CameraHealthy     = true;
                MicHealthy        = true;
                SpeakerHealthy    = true;
                SwitcherHealthy   = true;
                USBBridgeHealthy  = true;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Thread config error: {0}", ex.Message);
            }
        }

        // ============================================================
        // INITIALIZATION
        // ============================================================
        public override void InitializeSystem()
        {
            try
            {
                UpdateStatusText("System initializing...");
                RegisterTouchpanel();
                InitializeQsys();
                InitializeA20();
                UpdateSystemReady();

                StartHeartbeatTimer();

                FireEvent("program_restart_recovery",
                          eventValue: "Program initialized / restarted",
                          userAction: false,
                          notes: "Processor boot or program reload detected");
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeSystem error: {0}", ex.Message);
            }
        }

        // ============================================================
        // DEVICE REGISTRATION
        // ============================================================
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
                var result   = _qsysClient.ConnectToServer();

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
                var result  = _a20Client.ConnectToServer();

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

        // ============================================================
        // TOUCHPANEL SIGNAL HANDLER
        // ============================================================
        private void Ts770SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            try
            {
                if (args.Sig.Type != eSigType.Bool)
                    return;

                if (!args.Sig.BoolValue)   // act only on press (rising edge)
                    return;

                switch (args.Sig.Number)
                {
                    // ---- Original room controls
                    case JOIN_ROOM_ON:               RoomOn();              break;
                    case JOIN_ROOM_OFF:              RoomOff();             break;
                    case JOIN_VOL_UP:                VolumeUp();            break;
                    case JOIN_VOL_DOWN:              VolumeDown();          break;
                    case JOIN_MUTE_TOGGLE:           MuteToggle();          break;
                    case JOIN_TEAMS_HOME:            TeamsHome();           break;

                    // ---- Occupancy
                    case JOIN_OCCUPANCY_ON:          OnOccupancyOn();       break;
                    case JOIN_OCCUPANCY_OFF:         OnOccupancyOff();      break;

                    // ---- BYOD
                    case JOIN_BYOD_ENTER:            OnByodEnter();         break;
                    case JOIN_BYOD_EXIT:             OnByodExit();          break;

                    // ---- USB
                    case JOIN_USB_CONNECTED:         OnUsbConnected();      break;
                    case JOIN_USB_DISCONNECTED:      OnUsbDisconnected();   break;

                    // ---- Input sync
                    case JOIN_INPUT_SYNC_ON:         OnInputSyncOn();       break;
                    case JOIN_INPUT_SYNC_OFF:        OnInputSyncOff();      break;

                    // ---- Display
                    case JOIN_DISPLAY_ON:            OnDisplayOn();         break;
                    case JOIN_DISPLAY_OFF:           OnDisplayOff();        break;

                    // ---- Source routing
                    case JOIN_SOURCE_ROUTE_ACTIVE:   OnSourceRouteActive(); break;
                    case JOIN_SOURCE_ROUTE_INACTIVE: OnSourceRouteInactive(); break;

                    // ---- Presentation
                    case JOIN_PRESENTATION_START:    OnPresentationStart(); break;
                    case JOIN_PRESENTATION_STOP:     OnPresentationStop();  break;

                    // ---- Manual heartbeat
                    case JOIN_HEARTBEAT_TRIGGER:     OnHeartbeatSnapshot(); break;

                    // ---- Device health (reported via sensor/poll signals)
                    case JOIN_PROCESSOR_ONLINE:      OnProcessorOnline();   break;
                    case JOIN_PROCESSOR_OFFLINE:     OnProcessorOffline();  break;
                    case JOIN_DISPLAY_ONLINE:        OnDisplayOnline();     break;
                    case JOIN_DISPLAY_OFFLINE:       OnDisplayOffline();    break;
                    case JOIN_CAMERA_ONLINE:         OnCameraOnline();      break;
                    case JOIN_CAMERA_OFFLINE:        OnCameraOffline();     break;
                    case JOIN_MIC_ONLINE:            OnMicOnline();         break;
                    case JOIN_MIC_OFFLINE:           OnMicOffline();        break;
                    case JOIN_SPEAKER_ONLINE:        OnSpeakerOnline();     break;
                    case JOIN_SPEAKER_OFFLINE:       OnSpeakerOffline();    break;
                    case JOIN_SWITCHER_ONLINE:       OnSwitcherOnline();    break;
                    case JOIN_SWITCHER_OFFLINE:      OnSwitcherOffline();   break;
                    case JOIN_USB_BRIDGE_ONLINE:     OnUsbBridgeOnline();   break;
                    case JOIN_USB_BRIDGE_OFFLINE:    OnUsbBridgeOffline();  break;
                }

                ResetIdleTimer();
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Ts770SigChange error: {0}", ex.Message);
            }
        }

        // ============================================================
        // ORIGINAL ROOM CONTROLS
        // ============================================================
        private void RoomOn()
        {
            SystemOn       = true;
            SystemStartTime = UtcNow();

            UpdateStatusText("Room On");

            SendQsys("csp \"RoomPower\" 1\n");
            SendQsys("csv \"MainMute\" 0\n");
            SendA20("ROOM_ON\r\n");

            SetBoolFeedback(FB_SYSTEM_ON, true);
            UpdateSystemReady();

            StartSession();
            FireEvent("system_on", eventValue: "1", userAction: true);
        }

        private void RoomOff()
        {
            SystemOn     = false;
            SystemEndTime = UtcNow();

            UpdateStatusText("Room Off");

            SendQsys("csp \"RoomPower\" 0\n");
            SendQsys("csv \"MainMute\" 1\n");
            SendA20("ROOM_OFF\r\n");

            SetBoolFeedback(FB_SYSTEM_ON, false);
            UpdateSystemReady();

            EndSession();
            FireEvent("system_off", eventValue: "0", userAction: true);
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

        // ============================================================
        // EVENT TRIGGERS — OCCUPANCY
        // ============================================================
        private void OnOccupancyOn()
        {
            if (RoomOccupied) return;
            RoomOccupied       = true;
            OccupancyStartTime = UtcNow();
            OccupancyEndTime   = string.Empty;

            SetBoolFeedback(FB_ROOM_OCCUPIED, true);
            UpdateStatusText("Room Occupied");
            FireEvent("occupancy_on", eventValue: "1", userAction: false);
        }

        private void OnOccupancyOff()
        {
            if (!RoomOccupied) return;
            RoomOccupied     = false;
            OccupancyEndTime = UtcNow();

            SetBoolFeedback(FB_ROOM_OCCUPIED, false);
            UpdateStatusText("Room Vacant");

            StartSessionGraceTimer();
            FireEvent("occupancy_off", eventValue: "0", userAction: false);
        }

        // ============================================================
        // EVENT TRIGGERS — BYOD
        // ============================================================
        private void OnByodEnter()
        {
            if (BYODActive) return;
            BYODActive    = true;
            BYODStartTime = UtcNow();
            BYODEndTime   = string.Empty;

            SetBoolFeedback(FB_BYOD_ACTIVE, true);
            UpdateStatusText("BYOD Active");
            SendQsys("csp \"BYODMode\" 1\n");
            FireEvent("byod_enter", eventValue: "1", userAction: true);
        }

        private void OnByodExit()
        {
            if (!BYODActive) return;
            BYODActive  = false;
            BYODEndTime = UtcNow();

            SetBoolFeedback(FB_BYOD_ACTIVE, false);
            UpdateStatusText("BYOD Inactive");
            SendQsys("csp \"BYODMode\" 0\n");
            FireEvent("byod_exit", eventValue: "0", userAction: true);
        }

        // ============================================================
        // EVENT TRIGGERS — USB
        // ============================================================
        private void OnUsbConnected()
        {
            if (USBConnected) return;
            USBConnected    = true;
            USBConnectTime  = UtcNow();
            USBDisconnectTime = string.Empty;

            SetBoolFeedback(FB_USB_CONNECTED, true);
            UpdateStatusText("USB Connected");
            FireEvent("usb_connected", eventValue: "1", userAction: false);
        }

        private void OnUsbDisconnected()
        {
            if (!USBConnected) return;
            USBConnected      = false;
            USBDisconnectTime = UtcNow();

            SetBoolFeedback(FB_USB_CONNECTED, false);
            UpdateStatusText("USB Disconnected");
            FireEvent("usb_disconnected", eventValue: "0", userAction: false);
        }

        // ============================================================
        // EVENT TRIGGERS — INPUT SYNC
        // ============================================================
        private void OnInputSyncOn()
        {
            if (InputSyncActive) return;
            InputSyncActive = true;

            SetBoolFeedback(FB_INPUT_SYNC, true);
            UpdateStatusText("Input Sync Active");
            SendQsys("csp \"InputSync\" 1\n");
            FireEvent("input_sync_on", eventValue: "1", userAction: false);
        }

        private void OnInputSyncOff()
        {
            if (!InputSyncActive) return;
            InputSyncActive = false;

            SetBoolFeedback(FB_INPUT_SYNC, false);
            UpdateStatusText("Input Sync Off");
            SendQsys("csp \"InputSync\" 0\n");
            FireEvent("input_sync_off", eventValue: "0", userAction: false);
        }

        // ============================================================
        // EVENT TRIGGERS — DISPLAY
        // ============================================================
        private void OnDisplayOn()
        {
            if (DisplayOn) return;
            DisplayOn = true;

            SetBoolFeedback(FB_DISPLAY_ON, true);
            UpdateStatusText("Display On");
            SendQsys("csp \"DisplayPower\" 1\n");
            FireEvent("display_on", eventValue: "1", userAction: true);
        }

        private void OnDisplayOff()
        {
            if (!DisplayOn) return;
            DisplayOn = false;

            SetBoolFeedback(FB_DISPLAY_ON, false);
            UpdateStatusText("Display Off");
            SendQsys("csp \"DisplayPower\" 0\n");
            FireEvent("display_off", eventValue: "0", userAction: true);
        }

        // ============================================================
        // EVENT TRIGGERS — SOURCE ROUTING
        // ============================================================
        private void OnSourceRouteActive()
        {
            if (SourceRouteActive) return;
            SourceRouteActive = true;

            SetBoolFeedback(FB_SOURCE_ROUTE_ACTIVE, true);
            UpdateStatusText("Source Route Active");
            SendQsys("csp \"SourceRoute\" 1\n");
            FireEvent("source_route_active", eventValue: "1", userAction: true);
        }

        private void OnSourceRouteInactive()
        {
            if (!SourceRouteActive) return;
            SourceRouteActive = false;

            SetBoolFeedback(FB_SOURCE_ROUTE_ACTIVE, false);
            UpdateStatusText("Source Route Inactive");
            SendQsys("csp \"SourceRoute\" 0\n");
            FireEvent("source_route_inactive", eventValue: "0", userAction: true);
        }

        // ============================================================
        // EVENT TRIGGERS — PRESENTATION
        // ============================================================
        private void OnPresentationStart()
        {
            if (PresentationActive) return;
            PresentationActive = true;

            SetBoolFeedback(FB_PRESENTATION_ACTIVE, true);
            UpdateStatusText("Presentation Started");
            SendQsys("csp \"Presentation\" 1\n");
            FireEvent("presentation_start", eventValue: "1", userAction: true);
        }

        private void OnPresentationStop()
        {
            if (!PresentationActive) return;
            PresentationActive = false;

            SetBoolFeedback(FB_PRESENTATION_ACTIVE, false);
            UpdateStatusText("Presentation Stopped");
            SendQsys("csp \"Presentation\" 0\n");
            FireEvent("presentation_stop", eventValue: "0", userAction: true);
        }

        // ============================================================
        // EVENT TRIGGERS — DEVICE HEALTH
        // ============================================================
        private void OnProcessorOnline()
        {
            if (ProcessorHealthy) return;
            RecordOutageEnd();
            ProcessorHealthy = true;
            SetBoolFeedback(FB_PROCESSOR_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("processor_online", eventValue: "1", userAction: false);
        }

        private void OnProcessorOffline()
        {
            if (!ProcessorHealthy) return;
            RecordOutageStart();
            ProcessorHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_PROCESSOR_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("processor_offline", eventValue: "0", userAction: false,
                      notes: "Processor connectivity lost");
        }

        private void OnDisplayOnline()
        {
            if (DisplayHealthy) return;
            DisplayHealthy = true;
            SetBoolFeedback(FB_DISPLAY_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("display_online", eventValue: "1", userAction: false);
        }

        private void OnDisplayOffline()
        {
            if (!DisplayHealthy) return;
            DisplayHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_DISPLAY_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("display_offline", eventValue: "0", userAction: false,
                      notes: "Display connectivity lost");
        }

        private void OnCameraOnline()
        {
            if (CameraHealthy) return;
            CameraHealthy = true;
            SetBoolFeedback(FB_CAMERA_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("camera_online", eventValue: "1", userAction: false);
        }

        private void OnCameraOffline()
        {
            if (!CameraHealthy) return;
            CameraHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_CAMERA_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("camera_offline", eventValue: "0", userAction: false,
                      notes: "Camera connectivity lost");
        }

        private void OnMicOnline()
        {
            if (MicHealthy) return;
            MicHealthy = true;
            SetBoolFeedback(FB_MIC_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("mic_online", eventValue: "1", userAction: false);
        }

        private void OnMicOffline()
        {
            if (!MicHealthy) return;
            MicHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_MIC_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("mic_offline", eventValue: "0", userAction: false,
                      notes: "Microphone connectivity lost");
        }

        private void OnSpeakerOnline()
        {
            if (SpeakerHealthy) return;
            SpeakerHealthy = true;
            SetBoolFeedback(FB_SPEAKER_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("speaker_online", eventValue: "1", userAction: false);
        }

        private void OnSpeakerOffline()
        {
            if (!SpeakerHealthy) return;
            SpeakerHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_SPEAKER_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("speaker_offline", eventValue: "0", userAction: false,
                      notes: "Speaker connectivity lost");
        }

        private void OnSwitcherOnline()
        {
            if (SwitcherHealthy) return;
            SwitcherHealthy = true;
            SetBoolFeedback(FB_SWITCHER_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("switcher_online", eventValue: "1", userAction: false);
        }

        private void OnSwitcherOffline()
        {
            if (!SwitcherHealthy) return;
            SwitcherHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_SWITCHER_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("switcher_offline", eventValue: "0", userAction: false,
                      notes: "Switcher connectivity lost");
        }

        private void OnUsbBridgeOnline()
        {
            if (USBBridgeHealthy) return;
            USBBridgeHealthy = true;
            SetBoolFeedback(FB_USB_BRIDGE_HEALTHY, true);
            UpdateDeviceHealthText();
            FireEvent("usb_bridge_online", eventValue: "1", userAction: false);
        }

        private void OnUsbBridgeOffline()
        {
            if (!USBBridgeHealthy) return;
            USBBridgeHealthy = false;
            _OfflineIncidentCount++;
            SetBoolFeedback(FB_USB_BRIDGE_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("usb_bridge_offline", eventValue: "0", userAction: false,
                      notes: "USB bridge connectivity lost");
        }

        // ============================================================
        // EVENT TRIGGERS — HEARTBEAT
        // ============================================================
        private void OnHeartbeatSnapshot()
        {
            FireEvent("heartbeat_snapshot",
                      eventValue: "periodic",
                      userAction: false,
                      notes: "Scheduled system health snapshot");
        }

        // ============================================================
        // TIMER CALLBACKS
        // ============================================================
        private void HeartbeatTimerCallback(object notUsed)
        {
            OnHeartbeatSnapshot();
        }

        private void SessionGraceTimerCallback(object notUsed)
        {
            // Room still empty after grace period — fire session_timeout
            if (!RoomOccupied)
            {
                FireEvent("session_timeout",
                          eventValue: "grace_expired",
                          userAction: false,
                          notes: "Session grace period expired with no occupancy");
                EndSession();
            }
        }

        private void IdleTimerCallback(object notUsed)
        {
            // No user activity within idle window
            FireEvent("idle_timeout",
                      eventValue: "idle",
                      userAction: false,
                      notes: "No user interaction within idle timeout window");

            // Auto power-off
            RoomOff();
        }

        // ============================================================
        // SESSION MANAGEMENT
        // ============================================================
        private void StartSession()
        {
            CurrentSessionID    = GenerateSessionId();
            CurrentSessionStart = UtcNow();
            CurrentSessionEnd   = string.Empty;
            CurrentSessionValid = true;

            StopTimer(ref SessionGraceTimer);
            SetBoolFeedback(FB_SESSION_VALID, true);
            UpdateSerialFeedback(TXT_SESSION_ID,    CurrentSessionID);
            UpdateSerialFeedback(TXT_SESSION_START, CurrentSessionStart);

            ErrorLog.Notice("Session started: {0}", CurrentSessionID);
        }

        private void EndSession()
        {
            CurrentSessionEnd   = UtcNow();
            CurrentSessionValid = false;

            SetBoolFeedback(FB_SESSION_VALID, false);
            UpdateSerialFeedback(TXT_SESSION_ID, string.Empty);

            StopTimer(ref SessionGraceTimer);
            StopTimer(ref IdleTimeoutTimer);

            ErrorLog.Notice("Session ended: {0} (duration {1}s)",
                            CurrentSessionID, CurrentSessionDurationSeconds());
        }

        private void StartSessionGraceTimer()
        {
            StopTimer(ref SessionGraceTimer);
            SessionGraceTimer = new CTimer(SessionGraceTimerCallback, null, SESSION_GRACE_MS);
        }

        private void StartHeartbeatTimer()
        {
            StopTimer(ref HeartbeatTimer);
            HeartbeatTimer = new CTimer(HeartbeatTimerCallback, null,
                                        HEARTBEAT_INTERVAL_MS, HEARTBEAT_INTERVAL_MS);
        }

        private void ResetIdleTimer()
        {
            if (!SystemOn) return;
            StopTimer(ref IdleTimeoutTimer);
            IdleTimeoutTimer = new CTimer(IdleTimerCallback, null, IDLE_TIMEOUT_MS);
        }

        private static void StopTimer(ref CTimer timer)
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        // ============================================================
        // OUTAGE TRACKING
        // ============================================================
        private void RecordOutageStart()
        {
            if (string.IsNullOrEmpty(_OutageStartTime))
                _OutageStartTime = UtcNow();
        }

        private void RecordOutageEnd()
        {
            _OutageStartTime = string.Empty;
        }

        private int OutageDurationSeconds()
        {
            if (string.IsNullOrEmpty(_OutageStartTime))
                return 0;
            return (int)(DateTime.UtcNow - ParseUtc(_OutageStartTime)).TotalSeconds;
        }

        // ============================================================
        // EVENT DISPATCHER
        // ============================================================
        /// <summary>
        /// Builds a fully-populated RoomEvent, logs it, and dispatches it
        /// to Q-SYS as a JSON-formatted serial string.
        /// </summary>
        private void FireEvent(string eventType,
                               string eventValue  = "",
                               bool   userAction  = false,
                               string notes       = "")
        {
            try
            {
                string nowUtc   = UtcNow();
                string nowLocal = LocalNow();
                int    seqId;

                lock (_EventLock)
                {
                    seqId = ++_EventCounter;
                }

                // Build outage end time if applicable
                string outageEnd = string.Empty;
                int    outageDur = 0;

                if (!string.IsNullOrEmpty(_OutageStartTime) && eventType.EndsWith("_online"))
                {
                    outageEnd = nowUtc;
                    outageDur = OutageDurationSeconds();
                }

                var ev = new RoomEvent
                {
                    // Core identity
                    event_id                = string.Format("{0}-{1}", ROOM_ID, seqId),
                    event_type              = eventType,
                    event_value             = eventValue,
                    timestamp_utc           = nowUtc,
                    timestamp_local         = nowLocal,

                    // Processor / site context
                    processor_name          = InitialParametersClass.ControllerPromptName,
                    processor_ip            = CrestronEthernetHelper.GetEthernetParameter(
                                                CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                    room_name               = ROOM_NAME,
                    room_id                 = ROOM_ID,
                    site_name               = SITE_NAME,
                    building_name           = BUILDING_NAME,

                    // Source context (populated by caller if relevant; defaults empty)
                    source_device           = string.Empty,
                    source_device_type      = string.Empty,
                    source_device_ip        = string.Empty,
                    source_input            = string.Empty,
                    source_output           = string.Empty,

                    // Session context
                    session_id              = CurrentSessionID      ?? string.Empty,
                    session_state           = CurrentSessionValid   ? "active" : "inactive",
                    session_start_time      = CurrentSessionStart   ?? string.Empty,
                    session_end_time        = CurrentSessionEnd     ?? string.Empty,
                    session_duration_seconds = CurrentSessionDurationSeconds(),
                    session_valid           = CurrentSessionValid,

                    // Room state snapshot
                    occupancy_state         = RoomOccupied,
                    byod_state              = BYODActive,
                    usb_state               = USBConnected,
                    input_sync_state        = InputSyncActive,
                    display_state           = DisplayOn,
                    system_power_state      = SystemOn,
                    source_route_state      = SourceRouteActive,

                    // Device health snapshot
                    device_health_state     = BuildHealthSummary(),
                    offline_incident_count  = _OfflineIncidentCount,
                    outage_start_time       = _OutageStartTime ?? string.Empty,
                    outage_end_time         = outageEnd,
                    outage_duration_seconds = outageDur,

                    // Metadata
                    user_action_detected    = userAction,
                    notes                   = notes,
                    event_origin            = userAction ? "touchpanel" : "system",
                    firmware_version        = InitialParametersClass.FirmwareVersion,
                    program_version         = PROGRAM_VERSION
                };

                string json = SerializeEvent(ev);

                ErrorLog.Notice("EVENT: {0}", json);
                UpdateSerialFeedback(TXT_LAST_EVENT, string.Format("{0}: {1}", eventType, nowLocal));

                // Dispatch to Q-SYS as a serial command so it can be logged/routed
                SendQsys(string.Format("csv \"RMC4Event\" \"{0}\"\n",
                         json.Replace("\"", "\\\"").Replace("\n", "")));
            }
            catch (Exception ex)
            {
                ErrorLog.Error("FireEvent error ({0}): {1}", eventType, ex.Message);
            }
        }

        // ============================================================
        // Q-SYS / A20 COMMUNICATION HELPERS
        // ============================================================
        private void SendQsys(string command)
        {
            try
            {
                if (_qsysClient != null &&
                    _qsysClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
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
                if (_a20Client != null &&
                    _a20Client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
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

        // ============================================================
        // TOUCHPANEL FEEDBACK HELPERS
        // ============================================================
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
            UpdateSerialFeedback(TXT_STATUS, message);
        }

        private void UpdateSerialFeedback(uint join, string value)
        {
            try
            {
                if (_ts770 != null)
                    _ts770.StringInput[join].StringValue = value ?? string.Empty;
            }
            catch (Exception ex)
            {
                ErrorLog.Error("UpdateSerialFeedback error: {0}", ex.Message);
            }
        }

        private void UpdateSystemReady()
        {
            bool qsysOnline = _qsysClient != null &&
                              _qsysClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED;
            bool a20Online  = _a20Client  != null &&
                              _a20Client.ClientStatus  == SocketStatus.SOCKET_STATUS_CONNECTED;

            SetBoolFeedback(FB_SYSTEM_READY, qsysOnline && a20Online);
        }

        private void UpdateDeviceHealthText()
        {
            UpdateSerialFeedback(TXT_DEVICE_HEALTH, BuildHealthSummary());
        }

        // ============================================================
        // UTILITY HELPERS
        // ============================================================
        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        private static string LocalNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static DateTime ParseUtc(string s)
        {
            DateTime dt;
            DateTime.TryParse(s, out dt);
            return dt;
        }

        private static string GenerateSessionId()
        {
            // Compact session identifier: YYYYMMDD-HHmmss-<hash>
            var now = DateTime.UtcNow;
            return string.Format("{0:yyyyMMdd}-{0:HHmmss}-{1:X4}",
                                 now, (now.Ticks & 0xFFFF));
        }

        private int CurrentSessionDurationSeconds()
        {
            if (string.IsNullOrEmpty(CurrentSessionStart))
                return 0;

            DateTime end = string.IsNullOrEmpty(CurrentSessionEnd)
                           ? DateTime.UtcNow
                           : ParseUtc(CurrentSessionEnd);

            return (int)(end - ParseUtc(CurrentSessionStart)).TotalSeconds;
        }

        private string BuildHealthSummary()
        {
            // Compact health summary string: "P:1|D:1|C:1|M:1|S:1|SW:1|UB:1"
            return string.Format("P:{0}|D:{1}|C:{2}|M:{3}|SP:{4}|SW:{5}|UB:{6}",
                ProcessorHealthy  ? 1 : 0,
                DisplayHealthy    ? 1 : 0,
                CameraHealthy     ? 1 : 0,
                MicHealthy        ? 1 : 0,
                SpeakerHealthy    ? 1 : 0,
                SwitcherHealthy   ? 1 : 0,
                USBBridgeHealthy  ? 1 : 0);
        }

        /// <summary>
        /// Minimal JSON serialization for RoomEvent without external libraries.
        /// </summary>
        private static string SerializeEvent(RoomEvent e)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            AppendStr(sb,  "event_id",                e.event_id);
            AppendStr(sb,  "event_type",              e.event_type);
            AppendStr(sb,  "event_value",             e.event_value);
            AppendStr(sb,  "timestamp_utc",           e.timestamp_utc);
            AppendStr(sb,  "timestamp_local",         e.timestamp_local);
            AppendStr(sb,  "processor_name",          e.processor_name);
            AppendStr(sb,  "processor_ip",            e.processor_ip);
            AppendStr(sb,  "room_name",               e.room_name);
            AppendStr(sb,  "room_id",                 e.room_id);
            AppendStr(sb,  "site_name",               e.site_name);
            AppendStr(sb,  "building_name",           e.building_name);
            AppendStr(sb,  "source_device",           e.source_device);
            AppendStr(sb,  "source_device_type",      e.source_device_type);
            AppendStr(sb,  "source_device_ip",        e.source_device_ip);
            AppendStr(sb,  "source_input",            e.source_input);
            AppendStr(sb,  "source_output",           e.source_output);
            AppendStr(sb,  "session_id",              e.session_id);
            AppendStr(sb,  "session_state",           e.session_state);
            AppendStr(sb,  "session_start_time",      e.session_start_time);
            AppendStr(sb,  "session_end_time",        e.session_end_time);
            AppendInt(sb,  "session_duration_seconds",e.session_duration_seconds);
            AppendBool(sb, "session_valid",           e.session_valid);
            AppendBool(sb, "occupancy_state",         e.occupancy_state);
            AppendBool(sb, "byod_state",              e.byod_state);
            AppendBool(sb, "usb_state",               e.usb_state);
            AppendBool(sb, "input_sync_state",        e.input_sync_state);
            AppendBool(sb, "display_state",           e.display_state);
            AppendBool(sb, "system_power_state",      e.system_power_state);
            AppendBool(sb, "source_route_state",      e.source_route_state);
            AppendStr(sb,  "device_health_state",     e.device_health_state);
            AppendInt(sb,  "offline_incident_count",  e.offline_incident_count);
            AppendStr(sb,  "outage_start_time",       e.outage_start_time);
            AppendStr(sb,  "outage_end_time",         e.outage_end_time);
            AppendInt(sb,  "outage_duration_seconds", e.outage_duration_seconds);
            AppendBool(sb, "user_action_detected",    e.user_action_detected);
            AppendStr(sb,  "notes",                   e.notes);
            AppendStr(sb,  "event_origin",            e.event_origin);
            AppendStr(sb,  "firmware_version",        e.firmware_version);
            AppendStrLast(sb, "program_version",      e.program_version);
            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendStr(StringBuilder sb, string key, string val)
        {
            sb.AppendFormat("\"{0}\":\"{1}\",", key, (val ?? string.Empty).Replace("\"", "'"));
        }

        private static void AppendStrLast(StringBuilder sb, string key, string val)
        {
            sb.AppendFormat("\"{0}\":\"{1}\"", key, (val ?? string.Empty).Replace("\"", "'"));
        }

        private static void AppendInt(StringBuilder sb, string key, int val)
        {
            sb.AppendFormat("\"{0}\":{1},", key, val);
        }

        private static void AppendBool(StringBuilder sb, string key, bool val)
        {
            sb.AppendFormat("\"{0}\":{1},", key, val ? "true" : "false");
        }
    }
}
