using System.IO;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace DungeonOnline.EditorTools
{
    /// <summary>
    /// Run-and-observe capture for the editor while it stays open (a player build needs the editor closed).
    /// Does nothing unless <c>Temp/play-capture.txt</c> exists when Play starts. Then it saves the HUD, opens the
    /// Inventory panel, saves again, deletes the request and leaves Play mode.
    /// </summary>
    /// <remarks>
    /// The request file holds one line: the absolute output folder, for example
    /// <c>F:/WC/Agen/production/qa/evidence</c>. Files are named <c>core-ui-hud.png</c> and <c>core-ui-inventory.png</c>.
    /// </remarks>
    [InitializeOnLoad]
    public static class PlayModeCapture
    {
        private const string RequestPath = "Temp/play-capture.txt";
        private const int HudFrame = 90;        // let panels build and the Menu settle
        private const int InventoryFrame = 150; // after the panel opens

        private static int _frame;
        private static string _folder;

        static PlayModeCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !File.Exists(RequestPath)) return;
            _folder = File.ReadAllText(RequestPath).Trim();
            File.Delete(RequestPath);
            Directory.CreateDirectory(_folder);
            _frame = 0;
            EditorApplication.update += Step;
        }

        private static void Step()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Step;
                return;
            }

            _frame++;
            if (_frame == HudFrame)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, "core-ui-hud.png"));
                if (UIManager.Instance != null) UIManager.Instance.Show(UIID.Inventory);
                else Debug.LogWarning("PlayModeCapture: no UIManager in the scene, Inventory not opened.");
            }
            else if (_frame == InventoryFrame)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, "core-ui-inventory.png"));
            }
            else if (_frame == InventoryFrame + 10)
            {
                EditorApplication.update -= Step;
                EditorApplication.isPlaying = false;
                Debug.Log("PlayModeCapture: saved core-ui-hud.png and core-ui-inventory.png to " + _folder);
            }
        }
    }
}
