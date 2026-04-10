# RMC4_Enhanced.cs — Line-by-Line Plain-English Translation

Every line of `RMC4_Enhanced.cs` is reproduced below with an inline
explanation on the same or following line.  Section headers match the
comment blocks in the source file.

---

## Using Statements (Lines 1–8)

```
using System;                                    // Pull in core .NET types: DateTime, Exception, Math, etc.
using System.Text;                               // Pull in StringBuilder, used to build the JSON string.
using Crestron.SimplSharp;                       // Core Crestron runtime: ErrorLog, CrestronEthernetHelper, etc.
using Crestron.SimplSharp.CrestronSockets;       // TCP networking: TCPClient, SocketErrorCodes, SocketStatus.
using Crestron.SimplSharpPro;                    // Crestron Pro hardware SDK: CrestronControlSystem base class.
using Crestron.SimplSharpPro.CrestronThread;     // Threading helpers: CTimer (Crestron countdown timer), Thread.
using Crestron.SimplSharpPro.DeviceSupport;      // Signal types: BasicTriList, SigEventArgs, eSigType.
using Crestron.SimplSharpPro.UI;                 // Touchpanel device type: Tsw770.
```

---

## Namespace & RoomEvent Class (Lines 10–71)

```
namespace RoomController                         // Groups everything under a single logical namespace called "RoomController".
{
```

### RoomEvent — Telemetry Data Record

```
    public class RoomEvent                       // Blueprint for one telemetry record; every system event creates one of these.
    {
```

#### Core Identity Fields

```
        public string event_id               // Unique event identifier built as "<ROOM_ID>-<sequence number>", e.g. "CR-001-42".
        public string event_type             // Name of the event that occurred, e.g. "system_on", "occupancy_off".
        public string event_value            // The value associated with the event, typically "1" (on/connected) or "0" (off/disconnected).
        public string timestamp_utc          // Date-and-time the event fired expressed in UTC, format: "2026-04-10T22:07:05Z".
        public string timestamp_local        // Same moment expressed in local time, format: "2026-04-10 22:07:05".
```

#### Processor / Site Context Fields

```
        public string processor_name         // Human-readable name of the Crestron processor (read at runtime from the hardware).
        public string processor_ip           // IP address of the Crestron processor (read at runtime from the network stack).
        public string room_name              // Human-readable room name constant, e.g. "Conference Room".
        public string room_id                // Short room code constant, e.g. "CR-001".
        public string site_name              // Campus/site name constant, e.g. "Main Campus".
        public string building_name          // Building name constant, e.g. "Building A".
```

#### Source Routing Context Fields

```
        public string source_device          // Name of the device providing the content signal (left empty unless caller populates it).
        public string source_device_type     // Category of that device (e.g. "laptop", "media player").
        public string source_device_ip       // IP address of that source device.
        public string source_input           // Which physical input port is active.
        public string source_output          // Which physical output port is active.
```

#### Session Context Fields

```
        public string session_id             // Unique ID for the current room session (generated when the room turns on).
        public string session_state          // "active" if a session is running; "inactive" if not.
        public string session_start_time     // UTC timestamp when this session started.
        public string session_end_time       // UTC timestamp when this session ended (empty while still running).
        public int    session_duration_seconds // How many seconds the session has been (or was) active.
        public bool   session_valid          // true = a session is currently running; false = no active session.
```

#### Room State Snapshot Fields

```
        public bool   occupancy_state        // true = someone is detected in the room right now.
        public bool   byod_state             // true = BYOD (Bring Your Own Device) mode is currently active.
        public bool   pc_teams_state         // true = the PC has taken over room peripherals for a Teams call.
        public bool   usb_state              // true = a USB device is currently connected.
        public bool   input_sync_state       // true = the source/display input sync is currently active.
        public bool   display_state          // true = the display is powered on.
        public bool   system_power_state     // true = the room system is powered on.
        public bool   source_route_state     // true = a source-to-display routing path is currently active.
```

#### Device Health Snapshot Fields

```
        public string device_health_state    // Compact string summarizing every device's health, e.g. "P:1|D:1|C:0|M:1|S:1|SW:1|UB:1".
        public int    offline_incident_count // Running total of device-offline events recorded since program start.
        public string outage_start_time      // UTC timestamp when the most recent device outage began.
        public string outage_end_time        // UTC timestamp when the outage ended (empty while still ongoing).
        public int    outage_duration_seconds // How many seconds the outage lasted (or has lasted so far).
```

#### Metadata Fields

```
        public bool   user_action_detected   // true = the event was triggered by a person pressing a button on the touchpanel.
        public string notes                  // Free-text context note added by the calling code (e.g. "Processor connectivity lost").
        public string event_origin           // "touchpanel" if a user triggered it; "system" if software triggered it automatically.
        public string firmware_version       // Crestron processor firmware version string (read at runtime from the hardware).
        public string program_version        // Version of this C# program, set by the PROGRAM_VERSION constant.
    }
```

---

## ControlSystem Class (Lines 76–1447)

```
    public class ControlSystem : CrestronControlSystem  // Main program class. Inheriting CrestronControlSystem is required by Crestron's SDK; it wires this class into the processor lifecycle.
    {
```

---

### Network Topology Comment (Lines 79–84)

```
        // Netgear M4250 Port 1 -> RMC4                    -> 192.168.50.58    // The Crestron processor itself is on this switch port.
        // Netgear M4250 Port 2 -> TS-770                  -> 192.168.50.151   // The TS-770 touchpanel is on this switch port.
        // Netgear M4250 Port 3 -> Q-SYS Core Nano         -> 192.168.50.253   // The Q-SYS audio/video DSP is on this switch port.
        // Netgear M4250 Port 4 -> Yealink MeetingBar A20  -> 192.168.50.190   // The Yealink A20 video bar is on this switch port.
```

---

### Device Configuration Constants (Lines 89–99)

```
        private Tsw770 _ts770;                           // Object that represents the physical TS-770 touchpanel; null until RegisterTouchpanel() runs.
        private const uint   TS770_IPID  = 0x03;         // The TS-770's IP-ID (Crestron internal bus address, hex 03 = decimal 3).

        private const string QSYS_IP     = "192.168.50.253";  // Q-SYS Core Nano's fixed IP address on the room LAN.
        private const int    QSYS_PORT   = 1702;               // TCP port the Q-SYS Core listens on for external control commands.

        private const string A20_IP      = "192.168.50.190";  // Yealink A20 MeetingBar's fixed IP address on the room LAN.
        private const int    A20_PORT    = 5000;               // TCP port the A20 listens on for control commands.

        private TCPClient _qsysClient;                   // Active TCP connection to the Q-SYS Core; null until InitializeQsys() succeeds.
        private TCPClient _a20Client;                    // Active TCP connection to the Yealink A20; null until InitializeA20() succeeds.
```

---

### Program / Site Metadata Constants (Lines 104–108)

```
        private const string PROGRAM_VERSION  = "2.0.0";          // Version label for this program, embedded in every telemetry event.
        private const string ROOM_NAME        = "Conference Room"; // Descriptive room name embedded in every telemetry event.
        private const string ROOM_ID          = "CR-001";          // Short room code used as the prefix of every event_id.
        private const string SITE_NAME        = "Main Campus";     // Site/campus name embedded in every telemetry event.
        private const string BUILDING_NAME    = "Building A";      // Building name embedded in every telemetry event.
```

---

### Join Map — Digital Inputs (Lines 114–176)

Each constant maps a Crestron "join number" to a named action.
When the touchpanel (or an external signal) asserts a digital join,
the `Ts770SigChange` handler calls the corresponding method.

```
        // ---- Original room controls (joins 1–6)
        private const uint JOIN_ROOM_ON               = 1;   // Join 1  — user presses "Room On" button.
        private const uint JOIN_ROOM_OFF              = 2;   // Join 2  — user presses "Room Off" button.
        private const uint JOIN_VOL_UP                = 3;   // Join 3  — user presses "Volume Up" button.
        private const uint JOIN_VOL_DOWN              = 4;   // Join 4  — user presses "Volume Down" button.
        private const uint JOIN_MUTE_TOGGLE           = 5;   // Join 5  — user presses "Mute" button (toggles on/off).
        private const uint JOIN_TEAMS_HOME            = 6;   // Join 6  — user presses "Teams Home" to return the A20 to its home screen.

        // ---- Occupancy (joins 10–11)
        private const uint JOIN_OCCUPANCY_ON          = 10;  // Join 10 — occupancy sensor reports room is occupied.
        private const uint JOIN_OCCUPANCY_OFF         = 11;  // Join 11 — occupancy sensor reports room is empty.

        // ---- BYOD (joins 12–13)
        private const uint JOIN_BYOD_ENTER            = 12;  // Join 12 — user has connected their own device (BYOD session starts).
        private const uint JOIN_BYOD_EXIT             = 13;  // Join 13 — user has disconnected their BYOD device (BYOD session ends).

        // ---- USB (joins 14–15)
        private const uint JOIN_USB_CONNECTED         = 14;  // Join 14 — a USB device has been plugged in.
        private const uint JOIN_USB_DISCONNECTED      = 15;  // Join 15 — a USB device has been unplugged.

        // ---- Input sync (joins 16–17)
        private const uint JOIN_INPUT_SYNC_ON         = 16;  // Join 16 — the display/source input sync signal is now active.
        private const uint JOIN_INPUT_SYNC_OFF        = 17;  // Join 17 — the input sync signal has been lost.

        // ---- Display (joins 18–19)
        private const uint JOIN_DISPLAY_ON            = 18;  // Join 18 — user presses "Display On" button.
        private const uint JOIN_DISPLAY_OFF           = 19;  // Join 19 — user presses "Display Off" button.

        // ---- Source routing (joins 20–21)
        private const uint JOIN_SOURCE_ROUTE_ACTIVE   = 20;  // Join 20 — a source-to-display route has been activated.
        private const uint JOIN_SOURCE_ROUTE_INACTIVE = 21;  // Join 21 — the active source route has been deactivated.

        // ---- Presentation (joins 22–23)
        private const uint JOIN_PRESENTATION_START    = 22;  // Join 22 — user presses "Start Presentation" button.
        private const uint JOIN_PRESENTATION_STOP     = 23;  // Join 23 — user presses "Stop Presentation" button.

        // ---- PC Teams peripheral takeover (joins 25–28)
        // Join 25 — user presses "PC Teams" button; USB peripherals and display switch to the PC.
        // Join 26 — user exits PC Teams mode; USB peripherals and display return to the room system.
        // Join 27 — hardware sensor reports the USB-C hub/dock is physically connected to the PC.
        // Join 28 — hardware sensor reports the USB-C hub/dock has been disconnected from the PC.
        private const uint JOIN_PC_TEAMS_ENTER        = 25;
        private const uint JOIN_PC_TEAMS_EXIT         = 26;
        private const uint JOIN_USBC_CONNECTED        = 27;
        private const uint JOIN_USBC_DISCONNECTED     = 28;

        // ---- Manual heartbeat trigger (join 24)
        private const uint JOIN_HEARTBEAT_TRIGGER     = 24;  // Join 24 — user or automation manually fires a health snapshot event.

        // ---- Device online/offline signals (joins 30–43)
        private const uint JOIN_PROCESSOR_ONLINE      = 30;  // Join 30 — processor has come back online.
        private const uint JOIN_PROCESSOR_OFFLINE     = 31;  // Join 31 — processor has gone offline.
        private const uint JOIN_DISPLAY_ONLINE        = 32;  // Join 32 — display has come back online.
        private const uint JOIN_DISPLAY_OFFLINE       = 33;  // Join 33 — display has gone offline.
        private const uint JOIN_CAMERA_ONLINE         = 34;  // Join 34 — camera has come back online.
        private const uint JOIN_CAMERA_OFFLINE        = 35;  // Join 35 — camera has gone offline.
        private const uint JOIN_MIC_ONLINE            = 36;  // Join 36 — microphone has come back online.
        private const uint JOIN_MIC_OFFLINE           = 37;  // Join 37 — microphone has gone offline.
        private const uint JOIN_SPEAKER_ONLINE        = 38;  // Join 38 — speaker has come back online.
        private const uint JOIN_SPEAKER_OFFLINE       = 39;  // Join 39 — speaker has gone offline.
        private const uint JOIN_SWITCHER_ONLINE       = 40;  // Join 40 — switcher has come back online.
        private const uint JOIN_SWITCHER_OFFLINE      = 41;  // Join 41 — switcher has gone offline.
        private const uint JOIN_USB_BRIDGE_ONLINE     = 42;  // Join 42 — USB bridge has come back online.
        private const uint JOIN_USB_BRIDGE_OFFLINE    = 43;  // Join 43 — USB bridge has gone offline.
```

---

### Join Map — Digital Outputs / Feedback (Lines 181–201)

These constants are join numbers written *to* the touchpanel to turn
indicator lights on or off.

```
        private const uint FB_QSYS_ONLINE             = 101; // Indicator: Q-SYS Core is TCP-connected.
        private const uint FB_A20_ONLINE              = 102; // Indicator: Yealink A20 is TCP-connected.
        private const uint FB_SYSTEM_READY            = 103; // Indicator: both Q-SYS and A20 are online (system fully ready).
        private const uint FB_ROOM_OCCUPIED           = 104; // Indicator: room is currently occupied.
        private const uint FB_BYOD_ACTIVE             = 105; // Indicator: BYOD mode is active.
        private const uint FB_USB_CONNECTED           = 106; // Indicator: a USB device is connected.
        private const uint FB_INPUT_SYNC              = 107; // Indicator: input sync is active.
        private const uint FB_DISPLAY_ON              = 108; // Indicator: display is on.
        private const uint FB_SYSTEM_ON               = 109; // Indicator: room system is powered on.
        private const uint FB_SOURCE_ROUTE_ACTIVE     = 110; // Indicator: a source route is currently active.
        private const uint FB_PRESENTATION_ACTIVE     = 111; // Indicator: a presentation is in progress.
        private const uint FB_SESSION_VALID           = 112; // Indicator: a valid room session is running.
        private const uint FB_PC_TEAMS_ACTIVE         = 113; // Indicator: PC Teams peripheral-takeover mode is active.
        private const uint FB_USBC_CONNECTED          = 114; // Indicator: USB-C hub/dock is physically connected to the PC.
        private const uint FB_PROCESSOR_HEALTHY       = 120; // Indicator: processor is healthy/online.
        private const uint FB_DISPLAY_HEALTHY         = 121; // Indicator: display is healthy/online.
        private const uint FB_CAMERA_HEALTHY          = 122; // Indicator: camera is healthy/online.
        private const uint FB_MIC_HEALTHY             = 123; // Indicator: microphone is healthy/online.
        private const uint FB_SPEAKER_HEALTHY         = 124; // Indicator: speaker is healthy/online.
        private const uint FB_SWITCHER_HEALTHY        = 125; // Indicator: switcher is healthy/online.
        private const uint FB_USB_BRIDGE_HEALTHY      = 126; // Indicator: USB bridge is healthy/online.
```

---

### Join Map — Serial Outputs / Text (Lines 206–210)

These constants are join numbers written *to* the touchpanel to update
on-screen text fields.

```
        private const uint TXT_STATUS                 = 1;   // Text field 1: current status message shown on the touchpanel.
        private const uint TXT_SESSION_ID             = 2;   // Text field 2: current session ID shown on the touchpanel.
        private const uint TXT_LAST_EVENT             = 3;   // Text field 3: last event type and local timestamp.
        private const uint TXT_SESSION_START          = 4;   // Text field 4: session start time shown on the touchpanel.
        private const uint TXT_DEVICE_HEALTH          = 5;   // Text field 5: compact device health summary string.
```

---

### Timer Intervals (Lines 215–217)

```
        private const long SESSION_GRACE_MS           = 30000;  // 30,000 milliseconds = 30 seconds; how long to wait after the room empties before ending the session.
        private const long IDLE_TIMEOUT_MS            = 300000; // 300,000 milliseconds = 5 minutes; auto power-off if no button is pressed within this window.
        private const long HEARTBEAT_INTERVAL_MS      = 60000;  // 60,000 milliseconds = 1 minute; how often to fire a health-snapshot event automatically.
```

---

### State Variables (Lines 222–269)

```
        // ---- Room state booleans
        private bool   RoomOccupied;         // true while the occupancy sensor reports someone in the room.
        private bool   BYODActive;           // true while a user's own device is connected in BYOD mode.
        private bool   PCTeamsActive;        // true while the PC has taken over room peripherals for a Teams call.
        private bool   USBCConnected;        // true while a USB-C hub/dock is physically connected to the PC.
        private bool   USBConnected;         // true while a USB device is plugged in.
        private bool   InputSyncActive;      // true while the source/display input sync signal is active.
        private bool   DisplayOn;            // true while the room display is powered on.
        private bool   SystemOn;             // true while the room system is powered on.
        private bool   SourceRouteActive;    // true while a source-to-display routing path is active.
        private bool   PresentationActive;   // true while a presentation is in progress.

        // ---- Session tracking
        private string CurrentSessionID;     // The unique ID string for the currently active session; empty between sessions.
        private string CurrentSessionStart;  // UTC timestamp when the current session started; empty between sessions.
        private string CurrentSessionEnd;    // UTC timestamp when the current session ended; empty while still running.
        private bool   CurrentSessionValid;  // true = a session is currently active.

        // ---- Timers
        private CTimer SessionGraceTimer;    // One-shot 30-second countdown; fires EndSession() if the room is still empty.
        private CTimer IdleTimeoutTimer;     // One-shot 5-minute countdown; fires RoomOff() if no button is pressed.
        private CTimer HeartbeatTimer;       // Repeating 60-second countdown; fires a health-snapshot event on each tick.

        // ---- Device health flags (true = device is online/healthy)
        private bool ProcessorHealthy;      // true = Crestron processor is online.
        private bool DisplayHealthy;        // true = display is online.
        private bool CameraHealthy;         // true = camera is online.
        private bool MicHealthy;            // true = microphone is online.
        private bool SpeakerHealthy;        // true = speaker is online.
        private bool SwitcherHealthy;       // true = switcher is online.
        private bool USBBridgeHealthy;      // true = USB bridge is online.

        // ---- Event timestamps
        private string OccupancyStartTime;  // UTC time occupancy was first detected (this session).
        private string OccupancyEndTime;    // UTC time the room became empty.
        private string BYODStartTime;       // UTC time BYOD mode started.
        private string BYODEndTime;         // UTC time BYOD mode ended.
        private string USBConnectTime;      // UTC time a USB device was connected.
        private string USBDisconnectTime;   // UTC time a USB device was disconnected.
        private string SystemStartTime;     // UTC time the room system was turned on.
        private string SystemEndTime;       // UTC time the room system was turned off.

        // ---- Outage / incident tracking
        private int    _OfflineIncidentCount; // Running total of device-offline events since program start.
        private string _OutageStartTime;      // UTC timestamp when the most recent outage began; empty if no current outage.

        // ---- Event sequencing
        private int    _EventCounter;                        // Incremented by 1 each time FireEvent() runs; forms the numeric part of event_id.
        private readonly object _EventLock = new object();  // Thread-safety lock so _EventCounter increments atomically even across parallel calls.
```

---

## Constructor (Lines 274–293)

```
        public ControlSystem() : base()      // Constructor; called once by Crestron when the program loads. Calls the parent class constructor first.
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;  // Allow up to 20 simultaneous threads in this program (Crestron default is lower).

                // Assume all devices are healthy at boot — they will report back if they are not.
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
                ErrorLog.Error("Thread config error: {0}", ex.Message);  // If any setup step throws, log the error to the Crestron error log.
            }
        }
```

---

## InitializeSystem (Lines 298–319)

```
        public override void InitializeSystem()  // Called by Crestron after the constructor; this is the main startup entry point.
        {
            try
            {
                UpdateStatusText("System initializing...");  // Show "System initializing..." on the touchpanel status text field.
                RegisterTouchpanel();   // Register the TS-770 touchpanel with the Crestron runtime and subscribe to its button events.
                InitializeQsys();       // Open a TCP connection to the Q-SYS Core Nano.
                InitializeA20();        // Open a TCP connection to the Yealink A20.
                UpdateSystemReady();    // Update the "System Ready" indicator based on both connection states.

                StartHeartbeatTimer();  // Start firing a health-snapshot event every 60 seconds.

                FireEvent("program_restart_recovery",         // Log that the program has just started or restarted.
                          eventValue: "Program initialized / restarted",
                          userAction: false,
                          notes: "Processor boot or program reload detected");
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeSystem error: {0}", ex.Message);  // Log any startup error.
            }
        }
```

---

## RegisterTouchpanel (Lines 324–347)

```
        private void RegisterTouchpanel()
        {
            try
            {
                _ts770 = new Tsw770(TS770_IPID, this);     // Create the TS-770 object at IP-ID 0x03, passing this control system as its parent.
                _ts770.SigChange += Ts770SigChange;         // Subscribe: every time any signal changes on the touchpanel, call Ts770SigChange.

                var result = _ts770.Register();             // Tell the Crestron runtime to start communicating with this touchpanel.
                if (result == eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Notice("TS-770 registered successfully on IPID 0x{0:X2}", TS770_IPID);  // Log success.
                    UpdateStatusText("TS-770 registered");   // Show success on touchpanel.
                }
                else
                {
                    ErrorLog.Error("TS-770 registration failed: {0}", result);       // Log failure with the error code.
                    UpdateStatusText("TS-770 registration failed");                  // Show failure on touchpanel.
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("RegisterTouchpanel error: {0}", ex.Message);         // Log any unexpected exception.
            }
        }
```

---

## InitializeQsys (Lines 349–374)

```
        private void InitializeQsys()
        {
            try
            {
                _qsysClient = new TCPClient(QSYS_IP, QSYS_PORT, 4096);  // Create a TCP client aimed at the Q-SYS Core, with a 4 KB receive buffer.
                var result   = _qsysClient.ConnectToServer();             // Attempt the TCP connection.

                if (result == SocketErrorCodes.SOCKET_OK)
                {
                    ErrorLog.Notice("Connected to Q-SYS Core Nano at {0}:{1}", QSYS_IP, QSYS_PORT);  // Log success.
                    SetBoolFeedback(FB_QSYS_ONLINE, true);    // Light up the "Q-SYS Online" indicator on the touchpanel.
                    UpdateStatusText("Q-SYS online");          // Show "Q-SYS online" in the status text field.
                }
                else
                {
                    ErrorLog.Error("Q-SYS connection failed: {0}", result);   // Log failure with the socket error code.
                    SetBoolFeedback(FB_QSYS_ONLINE, false);   // Turn off the "Q-SYS Online" indicator.
                    UpdateStatusText("Q-SYS offline");         // Show "Q-SYS offline" in the status text field.
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeQsys error: {0}", ex.Message);  // Log any unexpected exception.
                SetBoolFeedback(FB_QSYS_ONLINE, false);                   // Ensure the indicator is off on exception.
            }
        }
```

---

## InitializeA20 (Lines 376–401)

```
        private void InitializeA20()
        {
            try
            {
                _a20Client = new TCPClient(A20_IP, A20_PORT, 4096);  // Create a TCP client aimed at the Yealink A20, with a 4 KB receive buffer.
                var result  = _a20Client.ConnectToServer();            // Attempt the TCP connection.

                if (result == SocketErrorCodes.SOCKET_OK)
                {
                    ErrorLog.Notice("Connected to Yealink A20 at {0}:{1}", A20_IP, A20_PORT);  // Log success.
                    SetBoolFeedback(FB_A20_ONLINE, true);    // Light up the "A20 Online" indicator on the touchpanel.
                    UpdateStatusText("A20 online");           // Show "A20 online" in the status text field.
                }
                else
                {
                    ErrorLog.Error("A20 connection failed: {0}", result);  // Log failure with the socket error code.
                    SetBoolFeedback(FB_A20_ONLINE, false);   // Turn off the "A20 Online" indicator.
                    UpdateStatusText("A20 offline");          // Show "A20 offline" in the status text field.
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("InitializeA20 error: {0}", ex.Message);  // Log any unexpected exception.
                SetBoolFeedback(FB_A20_ONLINE, false);                   // Ensure the indicator is off on exception.
            }
        }
```

---

## Touchpanel Signal Handler — Ts770SigChange (Lines 406–488)

```
        private void Ts770SigChange(BasicTriList currentDevice, SigEventArgs args)
        // Called automatically every time any signal on the touchpanel changes (button press, release, or analog change).
        {
            try
            {
                if (args.Sig.Type != eSigType.Bool)  // Ignore non-digital signals (analog sliders, serial strings); only care about digital on/off signals.
                    return;

                if (!args.Sig.BoolValue)             // Ignore the release (falling edge); only act when the button is pressed (rising edge, value = true).
                    return;

                switch (args.Sig.Number)             // Identify which join number fired and route to the correct handler method.
                {
                    // ---- Original room controls
                    case JOIN_ROOM_ON:               RoomOn();              break; // Join 1  → turn the room on.
                    case JOIN_ROOM_OFF:              RoomOff();             break; // Join 2  → turn the room off.
                    case JOIN_VOL_UP:                VolumeUp();            break; // Join 3  → step volume up.
                    case JOIN_VOL_DOWN:              VolumeDown();          break; // Join 4  → step volume down.
                    case JOIN_MUTE_TOGGLE:           MuteToggle();          break; // Join 5  → toggle mute.
                    case JOIN_TEAMS_HOME:            TeamsHome();           break; // Join 6  → send A20 to home screen.

                    // ---- Occupancy
                    case JOIN_OCCUPANCY_ON:          OnOccupancyOn();       break; // Join 10 → room occupied.
                    case JOIN_OCCUPANCY_OFF:         OnOccupancyOff();      break; // Join 11 → room empty.

                    // ---- BYOD
                    case JOIN_BYOD_ENTER:            OnByodEnter();         break; // Join 12 → BYOD session started.
                    case JOIN_BYOD_EXIT:             OnByodExit();          break; // Join 13 → BYOD session ended.

                    // ---- PC Teams peripheral takeover
                    case JOIN_PC_TEAMS_ENTER:        OnPCTeamsEnter();      break; // Join 25 → PC takes room peripherals.
                    case JOIN_PC_TEAMS_EXIT:         OnPCTeamsExit();       break; // Join 26 → room reclaims peripherals.

                    // ---- USB-C hub/dock
                    case JOIN_USBC_CONNECTED:        OnUsbCConnected();     break; // Join 27 → USB-C dock plugged in.
                    case JOIN_USBC_DISCONNECTED:     OnUsbCDisconnected();  break; // Join 28 → USB-C dock unplugged.

                    // ---- USB
                    case JOIN_USB_CONNECTED:         OnUsbConnected();      break; // Join 14 → USB device connected.
                    case JOIN_USB_DISCONNECTED:      OnUsbDisconnected();   break; // Join 15 → USB device disconnected.

                    // ---- Input sync
                    case JOIN_INPUT_SYNC_ON:         OnInputSyncOn();       break; // Join 16 → input sync active.
                    case JOIN_INPUT_SYNC_OFF:        OnInputSyncOff();      break; // Join 17 → input sync lost.

                    // ---- Display
                    case JOIN_DISPLAY_ON:            OnDisplayOn();         break; // Join 18 → turn display on.
                    case JOIN_DISPLAY_OFF:           OnDisplayOff();        break; // Join 19 → turn display off.

                    // ---- Source routing
                    case JOIN_SOURCE_ROUTE_ACTIVE:   OnSourceRouteActive(); break; // Join 20 → source route activated.
                    case JOIN_SOURCE_ROUTE_INACTIVE: OnSourceRouteInactive(); break; // Join 21 → source route deactivated.

                    // ---- Presentation
                    case JOIN_PRESENTATION_START:    OnPresentationStart(); break; // Join 22 → presentation started.
                    case JOIN_PRESENTATION_STOP:     OnPresentationStop();  break; // Join 23 → presentation stopped.

                    // ---- Manual heartbeat
                    case JOIN_HEARTBEAT_TRIGGER:     OnHeartbeatSnapshot(); break; // Join 24 → manually fire a health snapshot.

                    // ---- Device health signals
                    case JOIN_PROCESSOR_ONLINE:      OnProcessorOnline();   break; // Join 30 → processor back online.
                    case JOIN_PROCESSOR_OFFLINE:     OnProcessorOffline();  break; // Join 31 → processor offline.
                    case JOIN_DISPLAY_ONLINE:        OnDisplayOnline();     break; // Join 32 → display back online.
                    case JOIN_DISPLAY_OFFLINE:       OnDisplayOffline();    break; // Join 33 → display offline.
                    case JOIN_CAMERA_ONLINE:         OnCameraOnline();      break; // Join 34 → camera back online.
                    case JOIN_CAMERA_OFFLINE:        OnCameraOffline();     break; // Join 35 → camera offline.
                    case JOIN_MIC_ONLINE:            OnMicOnline();         break; // Join 36 → microphone back online.
                    case JOIN_MIC_OFFLINE:           OnMicOffline();        break; // Join 37 → microphone offline.
                    case JOIN_SPEAKER_ONLINE:        OnSpeakerOnline();     break; // Join 38 → speaker back online.
                    case JOIN_SPEAKER_OFFLINE:       OnSpeakerOffline();    break; // Join 39 → speaker offline.
                    case JOIN_SWITCHER_ONLINE:       OnSwitcherOnline();    break; // Join 40 → switcher back online.
                    case JOIN_SWITCHER_OFFLINE:      OnSwitcherOffline();   break; // Join 41 → switcher offline.
                    case JOIN_USB_BRIDGE_ONLINE:     OnUsbBridgeOnline();   break; // Join 42 → USB bridge back online.
                    case JOIN_USB_BRIDGE_OFFLINE:    OnUsbBridgeOffline();  break; // Join 43 → USB bridge offline.
                }

                ResetIdleTimer();  // Every button press resets the 5-minute inactivity countdown to prevent auto power-off.
            }
            catch (Exception ex)
            {
                ErrorLog.Error("Ts770SigChange error: {0}", ex.Message);  // Log any exception so the handler never crashes silently.
            }
        }
```

---

## Room On / Off (Lines 493–527)

```
        private void RoomOn()
        {
            SystemOn        = true;           // Mark the room system as powered on.
            SystemStartTime = UtcNow();       // Record the exact UTC time the room came on.

            UpdateStatusText("Room On");      // Show "Room On" in the touchpanel status field.

            SendQsys("csp \"RoomPower\" 1\n"); // Tell Q-SYS to set the "RoomPower" control to 1 (on).
            SendQsys("csv \"MainMute\" 0\n");  // Tell Q-SYS to set "MainMute" to 0 (unmuted).
            SendA20("ROOM_ON\r\n");            // Send the "ROOM_ON" command to the Yealink A20.

            SetBoolFeedback(FB_SYSTEM_ON, true);  // Light up the "System On" indicator on the touchpanel.
            UpdateSystemReady();                   // Refresh the "System Ready" indicator.

            StartSession();                        // Generate a new session ID and record the session start time.
            FireEvent("system_on", eventValue: "1", userAction: true);  // Log a telemetry event for the room powering on.
        }

        private void RoomOff()
        {
            SystemOn      = false;            // Mark the room system as powered off.
            SystemEndTime = UtcNow();         // Record the exact UTC time the room went off.

            UpdateStatusText("Room Off");     // Show "Room Off" in the touchpanel status field.

            SendQsys("csp \"RoomPower\" 0\n"); // Tell Q-SYS to set "RoomPower" to 0 (off).
            SendQsys("csv \"MainMute\" 1\n");  // Tell Q-SYS to set "MainMute" to 1 (muted).
            SendA20("ROOM_OFF\r\n");            // Send the "ROOM_OFF" command to the Yealink A20.

            SetBoolFeedback(FB_SYSTEM_ON, false); // Turn off the "System On" indicator.
            UpdateSystemReady();                   // Refresh the "System Ready" indicator.

            EndSession();                          // Close the current session and clear the session ID.
            FireEvent("system_off", eventValue: "0", userAction: true);  // Log a telemetry event for the room powering off.
        }
```

---

## Volume / Mute / Teams Home (Lines 529–551)

```
        private void VolumeUp()
        {
            UpdateStatusText("Volume Up");               // Show "Volume Up" in the touchpanel status field.
            SendQsys("css \"MainLevel\" 1 1\n");         // Tell Q-SYS to step the "MainLevel" control up by 1 unit.
        }

        private void VolumeDown()
        {
            UpdateStatusText("Volume Down");             // Show "Volume Down" in the touchpanel status field.
            SendQsys("css \"MainLevel\" -1 1\n");        // Tell Q-SYS to step "MainLevel" down by 1 unit.
        }

        private void MuteToggle()
        {
            UpdateStatusText("Mute Toggle");             // Show "Mute Toggle" in the touchpanel status field.
            SendQsys("ct \"MainMute\"\n");               // Tell Q-SYS to toggle the "MainMute" control (on↔off).
        }

        private void TeamsHome()
        {
            UpdateStatusText("Teams Home");              // Show "Teams Home" in the touchpanel status field.
            SendA20("TEAMS_HOME\r\n");                   // Send the "TEAMS_HOME" command to the Yealink A20 (navigates to Teams home screen).
        }
```

---

## Occupancy (Lines 556–579)

```
        private void OnOccupancyOn()
        {
            if (RoomOccupied) return;          // Guard: if already marked occupied, do nothing (prevent duplicate events).
            RoomOccupied       = true;         // Mark room as occupied.
            OccupancyStartTime = UtcNow();     // Record when occupancy began.
            OccupancyEndTime   = string.Empty; // Clear any previous end time.

            SetBoolFeedback(FB_ROOM_OCCUPIED, true);   // Light up the "Room Occupied" indicator.
            UpdateStatusText("Room Occupied");           // Show "Room Occupied" in the status field.
            FireEvent("occupancy_on", eventValue: "1", userAction: false);  // Log the occupancy event.
        }

        private void OnOccupancyOff()
        {
            if (!RoomOccupied) return;         // Guard: if already marked vacant, do nothing.
            RoomOccupied     = false;          // Mark room as vacant.
            OccupancyEndTime = UtcNow();       // Record when the room became empty.

            SetBoolFeedback(FB_ROOM_OCCUPIED, false);  // Turn off the "Room Occupied" indicator.
            UpdateStatusText("Room Vacant");             // Show "Room Vacant" in the status field.

            StartSessionGraceTimer();   // Start the 30-second countdown; if the room stays empty, the session will end.
            FireEvent("occupancy_off", eventValue: "0", userAction: false);  // Log the vacancy event.
        }
```

---

## BYOD (Lines 584–607)

```
        private void OnByodEnter()
        {
            if (BYODActive) return;            // Guard: if already in BYOD mode, do nothing.
            BYODActive    = true;              // Mark BYOD mode as active.
            BYODStartTime = UtcNow();          // Record when BYOD mode started.
            BYODEndTime   = string.Empty;      // Clear any previous end time.

            SetBoolFeedback(FB_BYOD_ACTIVE, true);     // Light up the "BYOD Active" indicator.
            UpdateStatusText("BYOD Active");             // Show "BYOD Active" in the status field.
            SendQsys("csp \"BYODMode\" 1\n");           // Tell Q-SYS to switch audio/video routing to BYOD mode.
            FireEvent("byod_enter", eventValue: "1", userAction: true);  // Log the BYOD start event.
        }

        private void OnByodExit()
        {
            if (!BYODActive) return;           // Guard: if not in BYOD mode, do nothing.
            BYODActive  = false;               // Mark BYOD mode as inactive.
            BYODEndTime = UtcNow();            // Record when BYOD mode ended.

            SetBoolFeedback(FB_BYOD_ACTIVE, false);    // Turn off the "BYOD Active" indicator.
            UpdateStatusText("BYOD Inactive");           // Show "BYOD Inactive" in the status field.
            SendQsys("csp \"BYODMode\" 0\n");           // Tell Q-SYS to return to normal audio/video routing.
            FireEvent("byod_exit", eventValue: "0", userAction: true);  // Log the BYOD end event.
        }
```

---

## PC Teams Peripheral Takeover (Lines 629–665)

> **What this does:** This code does NOT talk to the Teams application
> directly. It instructs Q-SYS — which controls physical USB and HDMI
> switchers — to hand the room's camera, mic, and speaker to the PC so
> the user can run Teams on their laptop using room peripherals.
>
> Q-SYS Named Controls:
> - `"USBSwitch"` 1 = room system, 2 = PC  
> - `"DisplayInput"` 1 = room default, 2 = PC HDMI  
> - `"PCTeamsMode"` 0 = off, 1 = on

```
        private void OnPCTeamsEnter()
        {
            if (PCTeamsActive) return;             // Guard: if already in PC Teams mode, do nothing.
            PCTeamsActive = true;                  // Mark PC Teams mode as active.

            UpdateStatusText("PC Teams Active");             // Show "PC Teams Active" in the status field.
            SetBoolFeedback(FB_PC_TEAMS_ACTIVE, true);       // Light up the "PC Teams Active" indicator.

            SendQsys("csp \"USBSwitch\" 2\n");     // Route USB peripherals (camera, mic, speaker) to the PC.
            SendQsys("csp \"DisplayInput\" 2\n");  // Switch the room display input to the PC's HDMI source.
            SendQsys("csp \"PCTeamsMode\" 1\n");   // Notify Q-SYS that PC Teams mode is on (for audio routing etc.).

            FireEvent("pc_teams_enter", eventValue: "1", userAction: true,
                      notes: "USB peripherals and display routed to PC for Teams call");  // Log the event.
        }

        private void OnPCTeamsExit()
        {
            if (!PCTeamsActive) return;            // Guard: if not in PC Teams mode, do nothing.
            PCTeamsActive = false;                 // Mark PC Teams mode as inactive.

            UpdateStatusText("PC Teams Inactive");           // Show "PC Teams Inactive" in the status field.
            SetBoolFeedback(FB_PC_TEAMS_ACTIVE, false);      // Turn off the "PC Teams Active" indicator.

            SendQsys("csp \"USBSwitch\" 1\n");     // Return USB peripherals to the room system.
            SendQsys("csp \"DisplayInput\" 1\n");  // Return the display to the default room input.
            SendQsys("csp \"PCTeamsMode\" 0\n");   // Clear PC Teams mode in Q-SYS.

            FireEvent("pc_teams_exit", eventValue: "0", userAction: true,
                      notes: "USB peripherals and display returned to room system");  // Log the event.
        }
```

---

## USB-C Hub / Dock (Lines 675–700)

```
        private void OnUsbCConnected()
        {
            if (USBCConnected) return;             // Guard: if already marked connected, do nothing.
            USBCConnected = true;                  // Mark USB-C hub as connected.

            SetBoolFeedback(FB_USBC_CONNECTED, true);        // Light up the "USB-C Connected" indicator.
            UpdateStatusText("USB-C Hub Connected");          // Show "USB-C Hub Connected" in the status field.
            FireEvent("usbc_connected", eventValue: "1", userAction: false,
                      notes: "USB-C hub/dock physically connected to PC");  // Log the event.
        }

        private void OnUsbCDisconnected()
        {
            if (!USBCConnected) return;            // Guard: if already marked disconnected, do nothing.
            USBCConnected = false;                 // Mark USB-C hub as disconnected.

            SetBoolFeedback(FB_USBC_CONNECTED, false);       // Turn off the "USB-C Connected" indicator.
            UpdateStatusText("USB-C Hub Disconnected");       // Show "USB-C Hub Disconnected" in the status field.

            if (PCTeamsActive)                     // If the USB-C cable was pulled while in PC Teams mode...
                OnPCTeamsExit();                   //   ...safely return peripherals to the room system.

            FireEvent("usbc_disconnected", eventValue: "0", userAction: false,
                      notes: "USB-C hub/dock disconnected from PC");  // Log the event.
        }
```

---

## USB Connected / Disconnected (Lines 703–724)

```
        private void OnUsbConnected()
        {
            if (USBConnected) return;              // Guard: already connected, do nothing.
            USBConnected      = true;              // Mark USB as connected.
            USBConnectTime    = UtcNow();          // Record the connection time.
            USBDisconnectTime = string.Empty;      // Clear any previous disconnect time.

            SetBoolFeedback(FB_USB_CONNECTED, true);  // Light up "USB Connected" indicator.
            UpdateStatusText("USB Connected");         // Show "USB Connected" in the status field.
            FireEvent("usb_connected", eventValue: "1", userAction: false);  // Log the event.
        }

        private void OnUsbDisconnected()
        {
            if (!USBConnected) return;             // Guard: already disconnected, do nothing.
            USBConnected      = false;             // Mark USB as disconnected.
            USBDisconnectTime = UtcNow();          // Record the disconnection time.

            SetBoolFeedback(FB_USB_CONNECTED, false); // Turn off "USB Connected" indicator.
            UpdateStatusText("USB Disconnected");      // Show "USB Disconnected" in the status field.
            FireEvent("usb_disconnected", eventValue: "0", userAction: false);  // Log the event.
        }
```

---

## Input Sync (Lines 729–749)

```
        private void OnInputSyncOn()
        {
            if (InputSyncActive) return;           // Guard: already active, do nothing.
            InputSyncActive = true;                // Mark input sync as active.

            SetBoolFeedback(FB_INPUT_SYNC, true);  // Light up "Input Sync" indicator.
            UpdateStatusText("Input Sync Active");  // Show "Input Sync Active" in the status field.
            SendQsys("csp \"InputSync\" 1\n");     // Tell Q-SYS to enable input sync.
            FireEvent("input_sync_on", eventValue: "1", userAction: false);  // Log the event.
        }

        private void OnInputSyncOff()
        {
            if (!InputSyncActive) return;          // Guard: already off, do nothing.
            InputSyncActive = false;               // Mark input sync as inactive.

            SetBoolFeedback(FB_INPUT_SYNC, false); // Turn off "Input Sync" indicator.
            UpdateStatusText("Input Sync Off");     // Show "Input Sync Off" in the status field.
            SendQsys("csp \"InputSync\" 0\n");     // Tell Q-SYS to disable input sync.
            FireEvent("input_sync_off", eventValue: "0", userAction: false);  // Log the event.
        }
```

---

## Display On / Off (Lines 754–774)

```
        private void OnDisplayOn()
        {
            if (DisplayOn) return;                 // Guard: display already on, do nothing.
            DisplayOn = true;                      // Mark display as on.

            SetBoolFeedback(FB_DISPLAY_ON, true);  // Light up "Display On" indicator.
            UpdateStatusText("Display On");         // Show "Display On" in the status field.
            SendQsys("csp \"DisplayPower\" 1\n");  // Tell Q-SYS to power on the display.
            FireEvent("display_on", eventValue: "1", userAction: true);  // Log the event.
        }

        private void OnDisplayOff()
        {
            if (!DisplayOn) return;                // Guard: display already off, do nothing.
            DisplayOn = false;                     // Mark display as off.

            SetBoolFeedback(FB_DISPLAY_ON, false); // Turn off "Display On" indicator.
            UpdateStatusText("Display Off");        // Show "Display Off" in the status field.
            SendQsys("csp \"DisplayPower\" 0\n");  // Tell Q-SYS to power off the display.
            FireEvent("display_off", eventValue: "0", userAction: true);  // Log the event.
        }
```

---

## Source Routing (Lines 779–799)

```
        private void OnSourceRouteActive()
        {
            if (SourceRouteActive) return;                  // Guard: already active, do nothing.
            SourceRouteActive = true;                       // Mark source route as active.

            SetBoolFeedback(FB_SOURCE_ROUTE_ACTIVE, true);  // Light up "Source Route Active" indicator.
            UpdateStatusText("Source Route Active");         // Show "Source Route Active" in the status field.
            SendQsys("csp \"SourceRoute\" 1\n");            // Tell Q-SYS to activate the source route.
            FireEvent("source_route_active", eventValue: "1", userAction: true);  // Log the event.
        }

        private void OnSourceRouteInactive()
        {
            if (!SourceRouteActive) return;                 // Guard: already inactive, do nothing.
            SourceRouteActive = false;                      // Mark source route as inactive.

            SetBoolFeedback(FB_SOURCE_ROUTE_ACTIVE, false); // Turn off "Source Route Active" indicator.
            UpdateStatusText("Source Route Inactive");       // Show "Source Route Inactive" in the status field.
            SendQsys("csp \"SourceRoute\" 0\n");            // Tell Q-SYS to deactivate the source route.
            FireEvent("source_route_inactive", eventValue: "0", userAction: true);  // Log the event.
        }
```

---

## Presentation (Lines 804–824)

```
        private void OnPresentationStart()
        {
            if (PresentationActive) return;                  // Guard: already presenting, do nothing.
            PresentationActive = true;                       // Mark presentation as active.

            SetBoolFeedback(FB_PRESENTATION_ACTIVE, true);   // Light up "Presentation Active" indicator.
            UpdateStatusText("Presentation Started");         // Show "Presentation Started" in the status field.
            SendQsys("csp \"Presentation\" 1\n");            // Tell Q-SYS to enable presentation mode.
            FireEvent("presentation_start", eventValue: "1", userAction: true);  // Log the event.
        }

        private void OnPresentationStop()
        {
            if (!PresentationActive) return;                 // Guard: not presenting, do nothing.
            PresentationActive = false;                      // Mark presentation as inactive.

            SetBoolFeedback(FB_PRESENTATION_ACTIVE, false);  // Turn off "Presentation Active" indicator.
            UpdateStatusText("Presentation Stopped");         // Show "Presentation Stopped" in the status field.
            SendQsys("csp \"Presentation\" 0\n");            // Tell Q-SYS to disable presentation mode.
            FireEvent("presentation_stop", eventValue: "0", userAction: true);  // Log the event.
        }
```

---

## Device Health Events (Lines 829–969)

Each device follows the same pattern. The "Online" method clears the
outage record (for the processor) and marks the device healthy; the
"Offline" method increments the incident counter and marks it unhealthy.

```
        // ---- Processor
        private void OnProcessorOnline()
        {
            if (ProcessorHealthy) return;          // Guard: already healthy, do nothing.
            RecordOutageEnd();                     // Clear the outage start time (outage is over).
            ProcessorHealthy = true;               // Mark processor as healthy.
            SetBoolFeedback(FB_PROCESSOR_HEALTHY, true);  // Light up "Processor Healthy" indicator.
            UpdateDeviceHealthText();              // Refresh the health summary text on the touchpanel.
            FireEvent("processor_online", eventValue: "1", userAction: false);  // Log the event.
        }

        private void OnProcessorOffline()
        {
            if (!ProcessorHealthy) return;         // Guard: already offline, do nothing.
            RecordOutageStart();                   // Record the UTC time this outage began.
            ProcessorHealthy = false;              // Mark processor as unhealthy.
            _OfflineIncidentCount++;               // Increment the running total of offline incidents.
            SetBoolFeedback(FB_PROCESSOR_HEALTHY, false); // Turn off "Processor Healthy" indicator.
            UpdateDeviceHealthText();              // Refresh the health summary text.
            FireEvent("processor_offline", eventValue: "0", userAction: false,
                      notes: "Processor connectivity lost");  // Log the event with a note.
        }

        // ---- Display (same pattern, no outage tracking)
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
            _OfflineIncidentCount++;               // Increment incident counter.
            SetBoolFeedback(FB_DISPLAY_HEALTHY, false);
            UpdateDeviceHealthText();
            FireEvent("display_offline", eventValue: "0", userAction: false,
                      notes: "Display connectivity lost");
        }

        // ---- Camera (identical pattern)
        private void OnCameraOnline()  { ... CameraHealthy = true;  ... "camera_online"  }
        private void OnCameraOffline() { ... CameraHealthy = false; _OfflineIncidentCount++; ... "camera_offline"  }

        // ---- Microphone (identical pattern)
        private void OnMicOnline()     { ... MicHealthy = true;     ... "mic_online"     }
        private void OnMicOffline()    { ... MicHealthy = false;    _OfflineIncidentCount++; ... "mic_offline"     }

        // ---- Speaker (identical pattern)
        private void OnSpeakerOnline()  { ... SpeakerHealthy = true;  ... "speaker_online"  }
        private void OnSpeakerOffline() { ... SpeakerHealthy = false; _OfflineIncidentCount++; ... "speaker_offline" }

        // ---- Switcher (identical pattern)
        private void OnSwitcherOnline()  { ... SwitcherHealthy = true;  ... "switcher_online"  }
        private void OnSwitcherOffline() { ... SwitcherHealthy = false; _OfflineIncidentCount++; ... "switcher_offline" }

        // ---- USB Bridge (identical pattern)
        private void OnUsbBridgeOnline()  { ... USBBridgeHealthy = true;  ... "usb_bridge_online"  }
        private void OnUsbBridgeOffline() { ... USBBridgeHealthy = false; _OfflineIncidentCount++; ... "usb_bridge_offline" }
```

---

## Heartbeat (Lines 974–980)

```
        private void OnHeartbeatSnapshot()
        {
            FireEvent("heartbeat_snapshot",                        // Fire a telemetry event of type "heartbeat_snapshot".
                      eventValue: "periodic",                      // The value is literally "periodic" (no on/off meaning).
                      userAction: false,                           // This is a system-initiated event, not a user button press.
                      notes: "Scheduled system health snapshot");  // Descriptive note.
        }
```

---

## Timer Callbacks (Lines 985–1013)

```
        private void HeartbeatTimerCallback(object notUsed)
        {
            OnHeartbeatSnapshot();  // Called every 60 seconds by the HeartbeatTimer; fires a health-snapshot event.
        }

        private void SessionGraceTimerCallback(object notUsed)
        {
            if (!RoomOccupied)                         // Only act if the room is still empty when the 30-second grace expires.
            {
                FireEvent("session_timeout",
                          eventValue: "grace_expired",
                          userAction: false,
                          notes: "Session grace period expired with no occupancy");  // Log the timeout event.
                EndSession();                          // Close the session.
            }
        }

        private void IdleTimerCallback(object notUsed)
        {
            FireEvent("idle_timeout",
                      eventValue: "idle",
                      userAction: false,
                      notes: "No user interaction within idle timeout window");  // Log the idle timeout event.

            RoomOff();  // Automatically power off the room after 5 minutes of no button presses.
        }
```

---

## Session Management (Lines 1018–1076)

```
        private void StartSession()
        {
            CurrentSessionID    = GenerateSessionId();  // Create a unique ID, e.g. "20260410-220705-A3F1".
            CurrentSessionStart = UtcNow();             // Record the session start time.
            CurrentSessionEnd   = string.Empty;         // Clear any previous end time.
            CurrentSessionValid = true;                 // Mark the session as active.

            StopTimer(ref SessionGraceTimer);           // Cancel any pending grace timer (we have a fresh session now).
            SetBoolFeedback(FB_SESSION_VALID, true);    // Light up the "Session Valid" indicator.
            UpdateSerialFeedback(TXT_SESSION_ID,    CurrentSessionID);     // Show the session ID on the touchpanel.
            UpdateSerialFeedback(TXT_SESSION_START, CurrentSessionStart);  // Show the start time on the touchpanel.

            ErrorLog.Notice("Session started: {0}", CurrentSessionID);     // Log the session start.
        }

        private void EndSession()
        {
            CurrentSessionEnd   = UtcNow();             // Record the session end time.
            CurrentSessionValid = false;                // Mark the session as inactive.

            SetBoolFeedback(FB_SESSION_VALID, false);   // Turn off the "Session Valid" indicator.
            UpdateSerialFeedback(TXT_SESSION_ID, string.Empty);  // Clear the session ID from the touchpanel.

            StopTimer(ref SessionGraceTimer);           // Cancel the grace timer if it was still running.
            StopTimer(ref IdleTimeoutTimer);            // Cancel the idle timer if it was still running.

            ErrorLog.Notice("Session ended: {0} (duration {1}s)",
                            CurrentSessionID, CurrentSessionDurationSeconds());  // Log the session end and duration.
        }

        private void StartSessionGraceTimer()
        {
            StopTimer(ref SessionGraceTimer);           // Cancel any existing grace timer first.
            SessionGraceTimer = new CTimer(SessionGraceTimerCallback, null, SESSION_GRACE_MS);
            // ↑ Create a new one-shot timer that fires SessionGraceTimerCallback after 30 seconds.
        }

        private void StartHeartbeatTimer()
        {
            StopTimer(ref HeartbeatTimer);              // Cancel any existing heartbeat timer first.
            HeartbeatTimer = new CTimer(HeartbeatTimerCallback, null,
                                        HEARTBEAT_INTERVAL_MS, HEARTBEAT_INTERVAL_MS);
            // ↑ Create a repeating timer: fires HeartbeatTimerCallback every 60 seconds indefinitely.
        }

        private void ResetIdleTimer()
        {
            if (!SystemOn) return;                      // Only run the idle timer while the system is on.
            StopTimer(ref IdleTimeoutTimer);            // Cancel the existing idle countdown.
            IdleTimeoutTimer = new CTimer(IdleTimerCallback, null, IDLE_TIMEOUT_MS);
            // ↑ Restart a fresh 5-minute countdown.
        }

        private static void StopTimer(ref CTimer timer)
        {
            if (timer != null)              // Only act if a timer object actually exists.
            {
                timer.Stop();              // Stop the countdown immediately.
                timer.Dispose();           // Release the timer's resources (memory / OS handle).
                timer = null;             // Set the reference to null so the guard above works next time.
            }
        }
```

---

## Outage Tracking (Lines 1081–1097)

```
        private void RecordOutageStart()
        {
            if (string.IsNullOrEmpty(_OutageStartTime))   // Only record if there isn't already an active outage recorded.
                _OutageStartTime = UtcNow();              // Save the UTC time this outage began.
        }

        private void RecordOutageEnd()
        {
            _OutageStartTime = string.Empty;              // Clear the outage start time — the outage is over.
        }

        private int OutageDurationSeconds()
        {
            if (string.IsNullOrEmpty(_OutageStartTime))   // If there is no recorded outage start, return 0.
                return 0;
            return (int)(DateTime.UtcNow - ParseUtc(_OutageStartTime)).TotalSeconds;
            // ↑ Calculate how many seconds have elapsed since the outage started.
        }
```

---

## FireEvent — Core Telemetry Dispatcher (Lines 1106–1203)

```
        private void FireEvent(string eventType,
                               string eventValue  = "",   // Default: empty string.
                               bool   userAction  = false, // Default: system-generated.
                               string notes       = "")    // Default: no notes.
        {
            try
            {
                string nowUtc   = UtcNow();    // Capture the current UTC time once for this event.
                string nowLocal = LocalNow();  // Capture the current local time once for this event.
                int    seqId;

                lock (_EventLock)              // Acquire the thread-safety lock before touching the counter.
                {
                    seqId = ++_EventCounter;   // Atomically increment and capture the event sequence number.
                }

                string outageEnd = string.Empty;  // Default outage end to empty.
                int    outageDur = 0;             // Default outage duration to zero.

                if (!string.IsNullOrEmpty(_OutageStartTime) && eventType.EndsWith("_online"))
                // ↑ If there is an active outage AND this event is a device coming back online...
                {
                    outageEnd = nowUtc;                   // Record the outage end time.
                    outageDur = OutageDurationSeconds();   // Calculate how long the outage lasted.
                }

                var ev = new RoomEvent
                {
                    // ---- Core identity
                    event_id       = string.Format("{0}-{1}", ROOM_ID, seqId),
                    // ↑ e.g. "CR-001-42" — room code hyphen sequence number.
                    event_type     = eventType,        // e.g. "system_on"
                    event_value    = eventValue,       // e.g. "1"
                    timestamp_utc  = nowUtc,           // e.g. "2026-04-10T22:07:05Z"
                    timestamp_local = nowLocal,        // e.g. "2026-04-10 22:07:05"

                    // ---- Processor / site context (read live from the hardware)
                    processor_name = InitialParametersClass.ControllerPromptName,
                    // ↑ The name configured on the Crestron processor itself.
                    processor_ip   = CrestronEthernetHelper.GetEthernetParameter(
                                       CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, 0),
                    // ↑ The processor's current IP address, read from the network adapter at the moment of the event.
                    room_name      = ROOM_NAME,        // Constant: "Conference Room"
                    room_id        = ROOM_ID,          // Constant: "CR-001"
                    site_name      = SITE_NAME,        // Constant: "Main Campus"
                    building_name  = BUILDING_NAME,    // Constant: "Building A"

                    // ---- Source context (caller can populate these; left empty by default)
                    source_device      = string.Empty,
                    source_device_type = string.Empty,
                    source_device_ip   = string.Empty,
                    source_input       = string.Empty,
                    source_output      = string.Empty,

                    // ---- Session context
                    session_id               = CurrentSessionID    ?? string.Empty,
                    // ↑ ?? means "use the right side if the left side is null".
                    session_state            = CurrentSessionValid ? "active" : "inactive",
                    session_start_time       = CurrentSessionStart ?? string.Empty,
                    session_end_time         = CurrentSessionEnd   ?? string.Empty,
                    session_duration_seconds = CurrentSessionDurationSeconds(),  // Seconds from start to now (or end).
                    session_valid            = CurrentSessionValid,

                    // ---- Room state snapshot (all current booleans captured at event time)
                    occupancy_state    = RoomOccupied,
                    byod_state         = BYODActive,
                    pc_teams_state     = PCTeamsActive,
                    usb_state          = USBConnected,
                    input_sync_state   = InputSyncActive,
                    display_state      = DisplayOn,
                    system_power_state = SystemOn,
                    source_route_state = SourceRouteActive,

                    // ---- Device health snapshot
                    device_health_state    = BuildHealthSummary(),     // e.g. "P:1|D:1|C:0|M:1|S:1|SW:1|UB:1"
                    offline_incident_count = _OfflineIncidentCount,    // Total offline events to date.
                    outage_start_time      = _OutageStartTime ?? string.Empty,
                    outage_end_time        = outageEnd,                // Empty unless this is a device-online event.
                    outage_duration_seconds = outageDur,               // 0 unless this is a device-online event.

                    // ---- Metadata
                    user_action_detected = userAction,
                    notes                = notes,
                    event_origin         = userAction ? "touchpanel" : "system",
                    // ↑ "touchpanel" if a person pressed a button; "system" if software triggered it.
                    firmware_version     = InitialParametersClass.FirmwareVersion,  // Read from the hardware.
                    program_version      = PROGRAM_VERSION             // Constant: "2.0.0"
                };

                string json = SerializeEvent(ev);  // Convert the RoomEvent object to a JSON string.

                ErrorLog.Notice("EVENT: {0}", json);  // Write the full JSON to the Crestron error log.
                UpdateSerialFeedback(TXT_LAST_EVENT,
                    string.Format("{0}: {1}", eventType, nowLocal));  // Show the event type and time on the touchpanel.

                // Send the JSON to Q-SYS as a serial-value command so it can be logged/forwarded upstream.
                SendQsys(string.Format("csv \"RMC4Event\" \"{0}\"\n",
                         json.Replace("\"", "\\\"").Replace("\n", "")));
                // ↑ json.Replace("\"","\\\"") — escape any double-quotes inside the JSON so they don't break the Q-SYS command syntax.
                // ↑ .Replace("\n","")         — strip newlines so the command is a single line.
            }
            catch (Exception ex)
            {
                ErrorLog.Error("FireEvent error ({0}): {1}", eventType, ex.Message);  // Log errors without crashing.
            }
        }
```

---

## SendQsys / SendA20 (Lines 1208–1258)

```
        private void SendQsys(string command)
        {
            try
            {
                if (_qsysClient != null &&
                    _qsysClient.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                // ↑ Only send if the TCP client object exists AND the socket reports it is connected.
                {
                    byte[] data = Encoding.ASCII.GetBytes(command);  // Convert the command string to a byte array using ASCII encoding.
                    _qsysClient.SendData(data, data.Length);         // Transmit the bytes over the TCP connection to Q-SYS.
                    ErrorLog.Notice("Q-SYS >> {0}", command.Trim()); // Log the sent command (trimmed of whitespace).
                    SetBoolFeedback(FB_QSYS_ONLINE, true);           // Confirm Q-SYS is online on the touchpanel.
                }
                else
                {
                    ErrorLog.Warn("Q-SYS not connected. Command not sent.");  // Log a warning.
                    SetBoolFeedback(FB_QSYS_ONLINE, false);                   // Show Q-SYS as offline on the touchpanel.
                    UpdateStatusText("Q-SYS offline");                         // Update the status text field.
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SendQsys error: {0}", ex.Message);  // Log any unexpected exception.
                SetBoolFeedback(FB_QSYS_ONLINE, false);             // Show Q-SYS as offline on exception.
            }
        }

        private void SendA20(string command)
        // Identical logic to SendQsys but uses _a20Client and FB_A20_ONLINE instead.
        {
            try
            {
                if (_a20Client != null &&
                    _a20Client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    byte[] data = Encoding.ASCII.GetBytes(command);  // Convert command to ASCII bytes.
                    _a20Client.SendData(data, data.Length);           // Transmit over TCP to the Yealink A20.
                    ErrorLog.Notice("A20 >> {0}", command.Trim());    // Log the sent command.
                    SetBoolFeedback(FB_A20_ONLINE, true);             // Confirm A20 is online on the touchpanel.
                }
                else
                {
                    ErrorLog.Warn("A20 not connected. Command not sent.");  // Log a warning.
                    SetBoolFeedback(FB_A20_ONLINE, false);                  // Show A20 as offline.
                    UpdateStatusText("A20 offline");                         // Update the status text field.
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SendA20 error: {0}", ex.Message);   // Log any unexpected exception.
                SetBoolFeedback(FB_A20_ONLINE, false);              // Show A20 as offline on exception.
            }
        }
```

---

## Touchpanel Feedback Helpers (Lines 1263–1307)

```
        private void SetBoolFeedback(uint join, bool value)
        {
            try
            {
                if (_ts770 != null)
                    _ts770.BooleanInput[join].BoolValue = value;
                // ↑ Write the true/false value to the specified digital join on the touchpanel,
                //   turning an indicator light on (true) or off (false).
            }
            catch (Exception ex)
            {
                ErrorLog.Error("SetBoolFeedback error: {0}", ex.Message);
            }
        }

        private void UpdateStatusText(string message)
        {
            UpdateSerialFeedback(TXT_STATUS, message);  // Convenience wrapper: writes to serial join 1 (the status text field).
        }

        private void UpdateSerialFeedback(uint join, string value)
        {
            try
            {
                if (_ts770 != null)
                    _ts770.StringInput[join].StringValue = value ?? string.Empty;
                // ↑ Write the string to the specified serial join on the touchpanel.
                //   ?? string.Empty ensures null is never sent (which would throw on Crestron hardware).
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
            // ↑ true only if the Q-SYS TCP client exists AND the socket is connected.
            bool a20Online  = _a20Client  != null &&
                              _a20Client.ClientStatus  == SocketStatus.SOCKET_STATUS_CONNECTED;
            // ↑ true only if the A20 TCP client exists AND the socket is connected.

            SetBoolFeedback(FB_SYSTEM_READY, qsysOnline && a20Online);
            // ↑ "System Ready" lights up only when BOTH devices are online simultaneously.
        }

        private void UpdateDeviceHealthText()
        {
            UpdateSerialFeedback(TXT_DEVICE_HEALTH, BuildHealthSummary());
            // ↑ Writes the compact health summary string to serial join 5 on the touchpanel.
        }
```

---

## Utility Helpers (Lines 1312–1360)

```
        private static string UtcNow()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            // ↑ Returns the current UTC time as an ISO-8601 string, e.g. "2026-04-10T22:07:05Z".
        }

        private static string LocalNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // ↑ Returns the current local time as a readable string, e.g. "2026-04-10 22:07:05".
        }

        private static DateTime ParseUtc(string s)
        {
            DateTime dt;
            DateTime.TryParse(s, out dt);   // Attempt to parse the stored timestamp string back to a DateTime.
            return dt;                       // Returns DateTime.MinValue (year 0001) if parsing fails.
        }

        private static string GenerateSessionId()
        {
            var now = DateTime.UtcNow;
            return string.Format("{0:yyyyMMdd}-{0:HHmmss}-{1:X4}",
                                 now, (now.Ticks & 0xFFFF));
            // ↑ Builds e.g. "20260410-220705-A3F1":
            //   yyyyMMdd  = date portion      (8 digits)
            //   HHmmss    = time portion      (6 digits)
            //   X4        = last 4 hex digits of the tick count (pseudo-random suffix for uniqueness)
        }

        private int CurrentSessionDurationSeconds()
        {
            if (string.IsNullOrEmpty(CurrentSessionStart))
                return 0;  // If there is no session start recorded, duration is zero.

            DateTime end = string.IsNullOrEmpty(CurrentSessionEnd)
                           ? DateTime.UtcNow               // Session still running: use "now" as the end.
                           : ParseUtc(CurrentSessionEnd);  // Session ended: use the recorded end time.

            return (int)(end - ParseUtc(CurrentSessionStart)).TotalSeconds;
            // ↑ Subtract start from end and return the difference in whole seconds.
        }

        private string BuildHealthSummary()
        {
            return string.Format("P:{0}|D:{1}|C:{2}|M:{3}|S:{4}|SW:{5}|UB:{6}",
                ProcessorHealthy  ? 1 : 0,   // P  = Processor  (1=online, 0=offline)
                DisplayHealthy    ? 1 : 0,   // D  = Display
                CameraHealthy     ? 1 : 0,   // C  = Camera
                MicHealthy        ? 1 : 0,   // M  = Microphone
                SpeakerHealthy    ? 1 : 0,   // S  = Speaker
                SwitcherHealthy   ? 1 : 0,   // SW = Switcher
                USBBridgeHealthy  ? 1 : 0);  // UB = USB Bridge
            // ↑ Example output: "P:1|D:1|C:0|M:1|S:1|SW:1|UB:1"
        }
```

---

## SerializeEvent — Custom JSON Builder (Lines 1365–1411)

```
        private static string SerializeEvent(RoomEvent e)
        // Converts a RoomEvent object to a JSON string without using any external JSON library.
        {
            var sb = new StringBuilder();  // Create a mutable string builder.
            sb.Append("{");                // Open the JSON object.

            // Each AppendStr / AppendInt / AppendBool call writes one JSON field.
            // All except the last end with a comma separator.
            AppendStr(sb,  "event_id",                 e.event_id);
            AppendStr(sb,  "event_type",               e.event_type);
            AppendStr(sb,  "event_value",              e.event_value);
            AppendStr(sb,  "timestamp_utc",            e.timestamp_utc);
            AppendStr(sb,  "timestamp_local",          e.timestamp_local);
            AppendStr(sb,  "processor_name",           e.processor_name);
            AppendStr(sb,  "processor_ip",             e.processor_ip);
            AppendStr(sb,  "room_name",                e.room_name);
            AppendStr(sb,  "room_id",                  e.room_id);
            AppendStr(sb,  "site_name",                e.site_name);
            AppendStr(sb,  "building_name",            e.building_name);
            AppendStr(sb,  "source_device",            e.source_device);
            AppendStr(sb,  "source_device_type",       e.source_device_type);
            AppendStr(sb,  "source_device_ip",         e.source_device_ip);
            AppendStr(sb,  "source_input",             e.source_input);
            AppendStr(sb,  "source_output",            e.source_output);
            AppendStr(sb,  "session_id",               e.session_id);
            AppendStr(sb,  "session_state",            e.session_state);
            AppendStr(sb,  "session_start_time",       e.session_start_time);
            AppendStr(sb,  "session_end_time",         e.session_end_time);
            AppendInt(sb,  "session_duration_seconds", e.session_duration_seconds);  // Written as a bare number, not quoted.
            AppendBool(sb, "session_valid",            e.session_valid);             // Written as true/false (not quoted).
            AppendBool(sb, "occupancy_state",          e.occupancy_state);
            AppendBool(sb, "byod_state",               e.byod_state);
            AppendBool(sb, "pc_teams_state",           e.pc_teams_state);
            AppendBool(sb, "usb_state",                e.usb_state);
            AppendBool(sb, "input_sync_state",         e.input_sync_state);
            AppendBool(sb, "display_state",            e.display_state);
            AppendBool(sb, "system_power_state",       e.system_power_state);
            AppendBool(sb, "source_route_state",       e.source_route_state);
            AppendStr(sb,  "device_health_state",      e.device_health_state);
            AppendInt(sb,  "offline_incident_count",   e.offline_incident_count);
            AppendStr(sb,  "outage_start_time",        e.outage_start_time);
            AppendStr(sb,  "outage_end_time",          e.outage_end_time);
            AppendInt(sb,  "outage_duration_seconds",  e.outage_duration_seconds);
            AppendBool(sb, "user_action_detected",     e.user_action_detected);
            AppendStr(sb,  "notes",                    e.notes);
            AppendStr(sb,  "event_origin",             e.event_origin);
            AppendStr(sb,  "firmware_version",         e.firmware_version);
            AppendStrLast(sb, "program_version",       e.program_version);  // Last field — no trailing comma.

            sb.Append("}");        // Close the JSON object.
            return sb.ToString();  // Return the completed JSON string.
        }
```

---

## JSON Helper Methods (Lines 1416–1445)

```
        private static string JsonEscape(string val)
        // Makes a string safe for embedding inside a JSON double-quoted value.
        {
            if (val == null) return string.Empty;  // Treat null as an empty string.
            return val
                .Replace("\\", "\\\\")   // Escape backslashes first (must be first to avoid double-escaping).
                .Replace("\"", "\\\"")   // Escape double-quote characters.
                .Replace("\n",  "\\n")   // Escape newline characters.
                .Replace("\r",  "\\r")   // Escape carriage-return characters.
                .Replace("\t",  "\\t");  // Escape tab characters.
        }

        private static void AppendStr(StringBuilder sb, string key, string val)
        {
            sb.AppendFormat("\"{0}\":\"{1}\",", key, JsonEscape(val));
            // ↑ Writes: "key":"escaped_value",    (note trailing comma)
        }

        private static void AppendStrLast(StringBuilder sb, string key, string val)
        {
            sb.AppendFormat("\"{0}\":\"{1}\"", key, JsonEscape(val));
            // ↑ Writes: "key":"escaped_value"     (no trailing comma — used for the final field)
        }

        private static void AppendInt(StringBuilder sb, string key, int val)
        {
            sb.AppendFormat("\"{0}\":{1},", key, val);
            // ↑ Writes: "key":123,                (integer is unquoted in JSON)
        }

        private static void AppendBool(StringBuilder sb, string key, bool val)
        {
            sb.AppendFormat("\"{0}\":{1},", key, val ? "true" : "false");
            // ↑ Writes: "key":true,  or  "key":false,   (boolean is unquoted and lowercase in JSON)
        }
    }   // end class ControlSystem
}       // end namespace RoomController
```
