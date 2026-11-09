namespace VolumeScroller;

public static class AudioControllerNative
{
    const byte MuteCode = 0xAD;
    const byte VolumeDownCode = 0xAE;
    const byte VolumeUpCode = 0xAF;
    const uint ExtendedKey = 0x0001;
    const uint KeyUp = 0x0002;

    private static (string DeviceId, float Level)? volumeBeforeMute;

    private static readonly Guid DeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, uint context, IntPtr parameters, out IAudioEndpointVolume endpoint);

        [PreserveSig]
        int OpenPropertyStore(uint access, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IntPtr callback);

        [PreserveSig]
        int UnregisterControlChangeNotify(IntPtr callback);

        [PreserveSig]
        int GetChannelCount(out uint count);

        [PreserveSig]
        int SetMasterVolumeLevel(float level, IntPtr context);

        [PreserveSig]
        int SetMasterVolumeLevelScalar(float level, IntPtr context);

        [PreserveSig]
        int GetMasterVolumeLevel(out float level);

        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig]
        int SetChannelVolumeLevel(uint channel, float level, IntPtr context);

        [PreserveSig]
        int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);

        [PreserveSig]
        int GetChannelVolumeLevel(uint channel, out float level);

        [PreserveSig]
        int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

    [DllImport("user32.dll")]
    static extern byte MapVirtualKey(uint uCode, uint uMapType);

    public static void VolumeUp()
    {
        keybd_event(VolumeUpCode, MapVirtualKey(VolumeUpCode, 0), ExtendedKey, 0);
        keybd_event(VolumeUpCode, MapVirtualKey(VolumeUpCode, 0), ExtendedKey | KeyUp, 0);
    }

    public static void VolumeDown()
    {
        keybd_event(VolumeDownCode, MapVirtualKey(VolumeDownCode, 0), ExtendedKey, 0);
        keybd_event(VolumeDownCode, MapVirtualKey(VolumeDownCode, 0), ExtendedKey | KeyUp, 0);
    }

    private static void SetMuteWithFlyout(IAudioEndpointVolume endpoint, bool muted)
    {
        Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool previousMute));
        keybd_event(MuteCode, MapVirtualKey(MuteCode, 0), ExtendedKey, 0);
        keybd_event(MuteCode, MapVirtualKey(MuteCode, 0), ExtendedKey | KeyUp, 0);

        // The media key shows the native flyout and is processed asynchronously.
        // Wait for its toggle before applying the requested final mute state.
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 250)
        {
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out bool currentMute));
            if (currentMute != previousMute) break;
            System.Threading.Thread.Sleep(1);
        }

        Marshal.ThrowExceptionForHR(endpoint.SetMute(muted, IntPtr.Zero));
    }

    public static bool ToggleMute()
    {
        IMMDeviceEnumerator enumerator = null;
        IMMDevice device = null;
        IAudioEndpointVolume endpoint = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(DeviceEnumeratorClassId));
            // Use the default playback device (eRender, eMultimedia).
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
            Marshal.ThrowExceptionForHR(device.GetId(out string deviceId));
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out endpoint));
            Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out float level));

            if (level > 0)
            {
                Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(0, IntPtr.Zero));
                volumeBeforeMute = (deviceId, level);
                SetMuteWithFlyout(endpoint, true);
            }
            else if (volumeBeforeMute is { } saved && saved.DeviceId == deviceId)
            {
                Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(saved.Level, IntPtr.Zero));
                SetMuteWithFlyout(endpoint, false);
                volumeBeforeMute = null;
            }
            else
            {
                SetMuteWithFlyout(endpoint, true);
            }

            return true;
        }
        catch (COMException ex)
        {
            Debug.Print($"Failed to toggle system volume: {ex.Message}");
            return false;
        }
        finally
        {
            if (endpoint != null) Marshal.ReleaseComObject(endpoint);
            if (device != null) Marshal.ReleaseComObject(device);
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
        }
    }
}
