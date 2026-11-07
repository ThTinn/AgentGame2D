using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Players;

namespace Game.EditorTools
{
    /// <summary>
    /// Menu TẠM để test cây nâng cấp nhân vật (Tools/Core/Player/Test): lên cấp, cộng điểm tiềm năng, xoá tiến trình.
    /// Đang Play thì áp ngay lên nhân vật (và CoreSystems tự ghi vào PlayerData như lúc chơi); không Play thì ghi thẳng
    /// vào PlayerData.asset để lần Play sau vào game đã có sẵn. Chỉ có trong Editor, không vào bản build.
    /// </summary>
    /// <remarks>
    /// Full một hướng = 143 điểm (thân 10 + Tinh thông 40 + 3 nhánh con x 31) + 1 điểm nút gốc, cần cấp 40 (nút Đỉnh).
    /// Nhớ chạy "Xoá tiến trình" trước khi commit: PlayerData.asset lưu cấp / điểm / nút đã nâng.
    /// </remarks>
    public static class PlayerProgressCheats
    {
        const string PlayerDataPath = "Assets/_Project/Core/Data/Player/PlayerData.asset";
        const string Menu = "Tools/Core/Player/Test/";

        /// <summary>Điểm để nâng tối đa một hướng (mọi nút của hướng, kể cả Tinh thông) + nút gốc.</summary>
        public const int FullDirectionPoints = 144;

        [MenuItem(Menu + "Cấp 40 + 144 điểm (full 1 hướng)")]
        static void FullDirection() => Apply(40, FullDirectionPoints, "cấp 40, +" + FullDirectionPoints + " điểm");

        [MenuItem(Menu + "Cấp 50 + 400 điểm (thử nhiều hướng)")]
        static void Plenty() => Apply(50, 400, "cấp 50, +400 điểm");

        [MenuItem(Menu + "+50 điểm tiềm năng")]
        static void AddPoints() => Apply(0, 50, "+50 điểm");

        [MenuItem(Menu + "Xoá tiến trình (về cấp 1, 0 điểm, 0 vàng)")]
        static void ResetAll()
        {
            if (Application.isPlaying && Me() is PlayerProfile me)
            {
                me.LoadProgress(1, 0, 0, null);
                me.SetGold(0);
            }
            PlayerData data = LoadData();
            if (data != null)
            {
                data.level = 1;
                data.exp = 0;
                data.potentialPoints = 0;
                data.gold = 0;
                data.playerUpgrades.Clear();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[Test] Đã xoá tiến trình: cấp 1, 0 điểm, 0 vàng, cây trống.");
        }

        /// <param name="level">Cấp đặt vào (0 = giữ cấp hiện tại). Không hạ cấp đang có.</param>
        /// <param name="points">Điểm tiềm năng cộng thêm vào số đang có (cây đã nâng giữ nguyên).</param>
        static void Apply(int level, int points, string what)
        {
            if (Application.isPlaying)
            {
                PlayerProfile me = Me();
                if (me == null)
                {
                    Debug.LogWarning("[Test] Đang Play nhưng chưa có nhân vật (CoreSystems.Me).");
                    return;
                }
                PlayerProgress p = me.Progress;
                var ranks = new List<NodeRank>();
                p.Save(ranks);
                int lv = Mathf.Max(p.Level, level);
                me.LoadProgress(lv, level > p.Level ? 0 : p.Exp, p.Points + points, ranks);
                me.Refill();
                Debug.Log($"[Test] {what}: nhân vật giờ cấp {p.Level}, {p.Points} điểm. Mở bảng Cây nâng cấp để thử.");
                return;
            }

            PlayerData data = LoadData();
            if (data == null)
            {
                Debug.LogWarning("[Test] Không thấy " + PlayerDataPath + ".");
                return;
            }
            if (level > data.level)
            {
                data.level = level;
                data.exp = 0;
            }
            data.potentialPoints += points;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Test] {what}: PlayerData giờ cấp {data.level}, {data.potentialPoints} điểm — bấm Play để thử.", data);
        }

        static PlayerProfile Me() => CoreSystems.Me;   // Game: bản GAME_2D lấy Player 2D qua GameController

        static PlayerData LoadData() => AssetDatabase.LoadAssetAtPath<PlayerData>(PlayerDataPath);
    }
}
