using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Những việc người chơi làm với các hệ thống Core (NPC, nhiệm vụ, túi đồ, skill, nâng cấp vũ khí, cây nhân vật) mà server
    /// phải biết khi chơi online.
    /// Lớp gameplay gọi qua <see cref="NetSync.Send"/>; phía mạng (OnlineCore, Assembly-CSharp) cài đặt bằng gói tin.
    /// Định danh gửi đi là mã cố định trong dữ liệu (NpcTemplate.name, QuestTemplate.name, NpcOption.name, StoryChoice.name,
    /// QuestOutcome.name, ItemTemplate.id), không gửi chữ hiển thị.
    /// </summary>
    public interface ICoreRemote
    {
        /// <summary>Bắt chuyện với NPC (thân thiện lần đầu mỗi phiên).</summary>
        void Talk(string npc);
        /// <summary>Lựa chọn Heal / BuyItem của NPC.</summary>
        void NpcOption(string npc, string option);
        /// <summary>
        /// Chọn một câu đáp. owner: "npc:&lt;npc&gt;/&lt;option&gt;" (câu đáp sau lời kể của NPC) hoặc "quest:&lt;quest&gt;"
        /// (câu đáp lúc được giao việc).
        /// </summary>
        void Choice(string owner, string choice);
        void Gift(string npc, string item);
        void Accept(string quest);
        void Abandon(string quest);
        /// <summary>Trả nhiệm vụ; outcome trống khi nhiệm vụ không có nhánh.</summary>
        void TurnIn(string quest, string outcome);
        /// <summary>Đã nghe xong lời NPC cho mục tiêu Talk thứ objective của nhiệm vụ.</summary>
        void TalkDone(string quest, int objective);
        /// <summary>Theo dõi / chỉ đường nhiệm vụ (trống = thôi theo).</summary>
        void Track(string quest);
        void UseItem(int itemId);
        void Equip(int bagSlot);
        void Unequip(int slot);
        void SetQuickItem(int slot, int itemId);
        /// <summary>Sắp xếp hành trang.</summary>
        void SortBag();
        /// <summary>Bỏ count cái ở ô hành trang bagSlot.</summary>
        void Discard(int bagSlot, int count);
        /// <summary>Gắn skill templateId vào ô slot (0 = gỡ ô). Cấp skill do server giữ.</summary>
        void SetSkill(int slot, int templateId);
        /// <summary>Nâng một cấp nút upgradeId trên cây nâng cấp của vũ khí weaponId.</summary>
        void UpgradeWeapon(int weaponId, int upgradeId);
        /// <summary>Đưa cây nâng cấp của vũ khí weaponId về 0.</summary>
        void ResetWeapon(int weaponId);
        /// <summary>Nâng một cấp nút nodeId của cây nhân vật.</summary>
        void UpgradeNode(int nodeId);
        /// <summary>Tẩy cây nhân vật, hoàn điểm.</summary>
        void ResetTree();
        /// <summary>Cường hóa món ở spot (PlayerProfile.GearAt) một lần. Server quay tỉ lệ rồi báo kết quả về.</summary>
        void UpgradeGear(int spot, bool lucky, bool protect);
        /// <summary>Khoét thêm một lỗ trên món ở spot. Server quay tỉ lệ rồi báo kết quả về.</summary>
        void OpenSocket(int spot);
        /// <summary>Khảm đá gemId vào lỗ index của món ở spot.</summary>
        void InlayGem(int spot, int index, int gemId);
        /// <summary>Gỡ đá ở lỗ index của món ở spot về túi.</summary>
        void RemoveGem(int spot, int index);
    }

    /// <summary>
    /// Nơi lớp gameplay Core biết mình đang ở phía nào khi có server:
    /// <list type="bullet">
    /// <item>Client online (<see cref="Online"/>): các hệ thống vẫn chạy luật tại máy để UI phản hồi ngay, đồng thời gửi
    /// yêu cầu lên server qua <see cref="Send"/>. Server áp dụng rồi gửi trạng thái chuẩn về, ghi đè bản ở máy.</item>
    /// <item>Server giả lập trong Editor (<see cref="InServer"/>, xem <see cref="ServerScope"/>): cùng code đó chạy trên hồ sơ
    /// người chơi của server; không gửi gì đi, thông báo (toast) được gom lại để server tự quyết có gửi về client không.</item>
    /// </list>
    /// </summary>
    public static class NetSync
    {
        static int serverDepth;
        static int suppress;
        static List<string> captured;

        /// <summary>Phía mạng của client online gán; null khi chưa kết nối.</summary>
        public static ICoreRemote Remote { get; set; }

        /// <summary>Server giả lập đang xử lý (trên main thread): mọi thay đổi là của server.</summary>
        public static bool InServer => serverDepth > 0;

        /// <summary>Client online: thay đổi tại máy chỉ là dự đoán, phải gửi lên server.</summary>
        public static bool Online => !InServer && Remote != null;

        /// <summary>Trong lúc này không gửi gì lên server: client đang áp trạng thái server vừa gửi về (ProfileSync).</summary>
        public static void Suppress(bool on) => suppress += on ? 1 : -1;

        /// <summary>Kênh gửi lên server; null khi không cần gửi (server, chưa kết nối). Dùng: NetSync.Send?.Accept(...).</summary>
        public static ICoreRemote Send => Online && suppress == 0 ? Remote : null;

        /// <summary>Luật Core được chạy lúc này không: client online (dự đoán) hoặc server đang xử lý.</summary>
        public static bool CanAct => InServer || Remote != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            serverDepth = 0;
            suppress = 0;
            captured = null;
            Remote = null;
        }

        /// <summary>
        /// Vào phía server: <paramref name="toasts"/> nhận các thông báo bắn ra trong lúc này (null = bỏ). Dùng qua
        /// <see cref="ServerScope"/>, đừng gọi lẻ.
        /// </summary>
        internal static List<string> Enter(List<string> toasts)
        {
            serverDepth++;
            List<string> old = captured;
            captured = toasts;
            return old;
        }

        internal static void Exit(List<string> previous)
        {
            serverDepth--;
            captured = previous;
        }

        /// <summary>Notify gọi: đang ở phía server thì giữ thông báo lại (true), không hiện lên UI của client cùng máy.</summary>
        internal static bool Capture(string content)
        {
            if (serverDepth <= 0) return false;
            captured?.Add(content);
            return true;
        }
    }
}
