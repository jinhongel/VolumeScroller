namespace VolumeScroller;

using System;

public class AudioController : IDisposable
{
    private readonly MouseHook mouseHook;
    private readonly MainViewModel viewModel;

    public AudioController(MainViewModel viewModel)
    {
        this.viewModel = viewModel;
        mouseHook = new();
        mouseHook.MouseWheelUp += _ => ChangeVolume(() => AudioControllerNative.VolumeUp());
        mouseHook.MouseWheelDown += _ => ChangeVolume(() => AudioControllerNative.VolumeDown());
        
        mouseHook.MiddleButtonDown += HandleMuteControl;
        
        mouseHook.Initialize(Applies);
    }

    private bool HandleMuteControl(MouseHook.MSLLHOOKSTRUCT mouseStruct)
    {
        if (!viewModel.EnableMiddleClickMute) return false;

        return AudioControllerNative.ToggleMute();
    }

    private static void ChangeVolume(Action action)
    {
        bool applies = Applies();

        int increment = Properties.Settings.Default.Increment;

        if (applies)
        {
            for (int i = 1; i <= increment; i++)
            {
                action();
            }
        }
    }

    private static bool Applies() => Properties.Settings.Default.TriggerMode switch
    {
        0 => CursorInfoTaskbarAlways.IsOnTaskbar(),
        1 => CursorInfoTaskbarVisible.IsOnTaskbar(),
        2 => AppliesForBorders(),
        _ => false,
    };

    private static bool AppliesForBorders()
    {
        int tolerance = Properties.Settings.Default.EdgeTolerance;

        // Check edges (full sides)
        bool onEdge = CursorInfoEdges.IsOnScreenEdge(
            Properties.Settings.Default.EnableTopEdge,
            Properties.Settings.Default.EnableRightEdge,
            Properties.Settings.Default.EnableBottomEdge,
            Properties.Settings.Default.EnableLeftEdge,
            tolerance);

        if (onEdge) return true;

        // Check corners
        return CursorInfoEdges.IsOnScreenCorner(
            Properties.Settings.Default.EnableBottomLeft,
            Properties.Settings.Default.EnableTopLeft,
            Properties.Settings.Default.EnableTopRight,
            Properties.Settings.Default.EnableBottomRight,
            tolerance);
    }

    public void Dispose()
    {
        mouseHook.Terminate();
    }
}
