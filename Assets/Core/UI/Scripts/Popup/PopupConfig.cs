using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Bảng tra mã popup > prefab. PopupMN đọc bảng này lúc Awake rồi Instantiate theo yêu cầu (lần đầu mở mới tạo).
    /// Tạo asset: chuột phải trong Project > Create > Core > Popup Config (hoặc Tools/Core/UI/Build Popup Prefabs tự dựng sẵn).
    /// </summary>
    [CreateAssetMenu(fileName = "Popup_Config", menuName = "Core/Popup Config")]
    public class PopupConfig : ScriptableObject
    {
        public PopupEntry[] entries;
    }

    [Serializable]
    public class PopupEntry
    {
        public PopupID id;
        public PopupBase prefab;
    }
}
