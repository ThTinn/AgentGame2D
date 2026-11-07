using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Bảng tra mã panel > prefab cho các panel không đặt sẵn trong GameCanvas. UIManager đọc lúc Awake rồi
    /// Instantiate theo yêu cầu (lần đầu mở mới tạo). Panel đã có sẵn trong scene thì kéo vào Scene Panels của
    /// UIManager thay vì khai ở đây.
    /// Tạo asset: chuột phải trong Project > Create > Core > Panel Config.
    /// </summary>
    [CreateAssetMenu(fileName = "Panel_Config", menuName = "Core/Panel Config")]
    public class PanelConfig : ScriptableObject
    {
        public PanelEntry[] entries;
    }

    [Serializable]
    public class PanelEntry
    {
        public UIID id;
        public PanelBase prefab;
    }
}
