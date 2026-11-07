using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;
using Game.Map;
using Game.Npcs;
using Game.Story;

namespace Game.UI
{
    /// <summary>
    /// Một cuộc nói chuyện với NPC: nghe <see cref="NpcSystem.Opened"/> (lớp gameplay không được gọi thẳng lên UI), mở
    /// <see cref="NpcDialogPopup"/> rồi dựng từng màn:
    ///  - Menu chính: câu chào theo bậc thân thiện, các lựa chọn riêng của NPC, Cửa hàng, Nhiệm vụ, Tặng quà, Tạm biệt.
    ///  - Cửa hàng: mọi lựa chọn BuyItem của NPC theo trang (giá đã tính thân thiện).
    ///  - Nhiệm vụ: [Mới] nhận được, [Khóa] chưa đủ thân thiện (NPC từ chối), [Đang làm] xem tiến độ / bỏ, [Xong] trả và nhận thưởng.
    ///  - Tặng quà: các món NPC thích đang có trong túi.
    /// Luật (thân thiện, thưởng, điều kiện) nằm ở <see cref="QuestSystem"/>; lớp này chỉ ghép chữ và nút.
    /// Chữ: dữ liệu NPC / nhiệm vụ qua <see cref="T(string)"/> (bản dịch trong Lang_Data.xlsx), chữ cố định viết song ngữ
    /// "Tiếng Việt|English" qua Lang.GetSplitText / Lang.Format để Tools/Core/Lang gom được.
    /// </summary>
    public class NpcDialog
    {
        // SubsystemRegistration xóa sạch NpcSystem.Opened, nên đăng ký ở bước sau (BeforeSceneLoad)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            NpcSystem.Opened -= Show;
            NpcSystem.Opened += Show;
            // chữ sinh ở lớp gameplay (tiến độ, thông báo, tên bậc) cũng dịch qua Lang
            StoryText.Localize = Lang.GetSplitText;
        }

        /// <summary>Mở hộp thoại của NPC. o là object của NPC trên map (lấy ảnh), null được.</summary>
        public static void Show(NpcTemplate npc, GameObject o)
        {
            if (npc == null) return;

            if (PopupMN.Instance == null || !PopupMN.Instance.Has(PopupID.NpcDialog))
            {
                Debug.LogError("NpcDialog: Popup_Config chưa có NpcDialog. Chạy Tools/Core/UI/Build Npc Dialog Popup.");
                Notify.Toast(T(npc.Label) + ": " + QuestSystem.GreetingOf(npc), 3f);
                return;
            }

            // NPC entity: ảnh nằm ở object con (prefab: Body; dựng tạm: Body)
            SpriteRenderer sr = o != null ? o.GetComponentInChildren<SpriteRenderer>() : null;
            Sprite face = sr != null ? sr.sprite : null;
            PopupMN.Open<NpcDialogPopup>(PopupID.NpcDialog, p => new NpcDialog(npc, p, face).Start());
        }

        /// <summary>Mở panel cho lựa chọn OpenPanel (param = UIID).</summary>
        public static void OpenPanel(int id)
        {
            if (UIManager.Instance != null) UIManager.Instance.SetVisible((UIID)id, true);
            else Debug.LogWarning("NpcDialog: chưa có UIManager, không mở được panel " + (UIID)id + ".");
        }

        /// <summary>Chữ trong dữ liệu (tên, lời thoại...) ở ngôn ngữ đang chọn.</summary>
        static string T(string s) => string.IsNullOrEmpty(s) ? s : Lang.GetSplitText(s);

        static string[] T(string[] pages)
        {
            if (pages == null) return null;
            var copy = new string[pages.Length];
            for (int i = 0; i < pages.Length; i++) copy[i] = T(pages[i]);
            return copy;
        }

        readonly NpcTemplate npc;
        readonly NpcDialogPopup view;
        readonly List<DialogChoice> buttons = new List<DialogChoice>(8);
        readonly List<QuestTemplate> quests = new List<QuestTemplate>(8);
        readonly List<NpcGift> gifts = new List<NpcGift>(4);
        readonly List<NpcOption> shop = new List<NpcOption>(8);
        readonly StringBuilder text = new StringBuilder(256);

        const int ShopPageSize = 4;

        NpcDialog(NpcTemplate npc, NpcDialogPopup view, Sprite face)
        {
            this.npc = npc;
            this.view = view;
            string speaker = string.IsNullOrEmpty(npc.role) ? T(npc.Label) : T(npc.Label) + " - " + T(npc.role);
            view.SetSpeaker(speaker, face, AffinityLine());
        }

        string AffinityLine() =>
            Lang.Format("Thân thiện: {0} {1}/{2}|Affinity: {0} {1}/{2}", Affinity.NameOf(QuestSystem.TierOf(npc)), QuestSystem.AffinityOf(npc), Affinity.Max);

        void RefreshAffinity() => view.SetAffinity(AffinityLine());

        // ---------------- Mở hộp thoại ----------------

        readonly List<QuestStep> talks = new List<QuestStep>(4);

        /// <summary>
        /// Người chơi tới gặp NPC vì mục tiêu Talk của việc đang làm: NPC nói những lời đó trước (mỗi mục tiêu một lượt, nghe
        /// xong thì mục tiêu xong), rồi mới tới menu chính.
        /// </summary>
        void Start()
        {
            QuestSystem.TalksAt(npc, talks);
            NextTalk();
        }

        void NextTalk()
        {
            if (talks.Count == 0)
            {
                Root(false);
                return;
            }
            QuestStep step = talks[0];
            talks.RemoveAt(0);
            string[] lines = step.Objective.lines;
            if (lines == null || lines.Length == 0)
                lines = new[] { Lang.Format("Ta biết chuyện \"{0}\" rồi. Cứ làm tiếp đi.|I know about \"{0}\". Carry on.", T(step.quest.title)) };
            else lines = T(lines);
            view.Pages(lines, () =>
            {
                QuestSystem.CompleteTalk(step);
                NextTalk();
            });
        }

        // ---------------- Menu chính ----------------

        void Root(bool instant)
        {
            buttons.Clear();
            // hàng bán gom vào một nút Cửa hàng nên bán bao nhiêu món cũng không chiếm chỗ của nút khác
            shop.Clear();
            if (npc.options != null)
                foreach (NpcOption o in npc.options)
                    if (o != null && o.action == NpcAction.BuyItem) shop.Add(o);
            int left = NpcDatabase.MaxOptions - (shop.Count > 0 ? 1 : 0);
            if (npc.options != null)
                foreach (NpcOption o in npc.options)
                {
                    if (o == null || o.action == NpcAction.BuyItem || left-- <= 0) continue;
                    NpcOption option = o;
                    buttons.Add(new DialogChoice(OptionLabel(option), () => Choose(option)));
                }
            if (shop.Count > 0) buttons.Add(new DialogChoice(Lang.GetSplitText("Cửa hàng|Shop"), () => ShopMenu(0)));

            QuestSystem.QuestsAt(npc, quests);
            if (quests.Count > 0)
            {
                string mark = "";
                foreach (QuestTemplate q in quests)
                {
                    QuestStatus st = QuestSystem.StatusOf(q);
                    if (st == QuestStatus.Ready && q.TurnInNpc == npc.name) { mark = " (?)"; break; }
                    if (st == QuestStatus.Available && QuestSystem.TierAllows(q)) mark = " (!)";
                }
                buttons.Add(new DialogChoice(Lang.GetSplitText("Nhiệm vụ|Quests") + mark, () => QuestList(null)));
            }
            if (npc.gifts != null && npc.gifts.Count > 0) buttons.Add(new DialogChoice(Lang.GetSplitText("Tặng quà|Give a gift"), GiftMenu));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Tạm biệt|Goodbye"), view.Close));

            view.Say(QuestSystem.GreetingOf(npc), buttons, instant);
        }

        string OptionLabel(NpcOption option)
        {
            string label = T(option.label);
            if ((option.action != NpcAction.Heal && option.action != NpcAction.BuyItem) || option.price <= 0) return label;
            return label + " " + Lang.Format("({0} vàng)|({0} gold)", QuestSystem.PriceAt(npc, option.price));
        }

        void Choose(NpcOption option)
        {
            switch (option.action)
            {
                case NpcAction.Talk:
                    // kể xong: có câu đáp thì cho người chơi chọn, không thì về menu
                    view.Pages(T(option.pages), () =>
                    {
                        string last = option.pages != null && option.pages.Length > 0 ? T(option.pages[option.pages.Length - 1]) : QuestSystem.GreetingOf(npc);
                        Ask(last, option.choices, Lang.GetSplitText("Thôi|Never mind"), () => Root(true), () => Root(true));
                    });
                    break;
                case NpcAction.OpenPanel:
                    view.Close();
                    OpenPanel(option.param);
                    break;
                default:
                    NpcSystem.Use(npc, option);   // báo kết quả bằng toast; giữ menu để mua / dùng tiếp
                    break;
            }
        }

        // ---------------- Cửa hàng ----------------

        // hàng của NPC theo trang: cột nút chứa được 6 nút, chừa chỗ cho Trang sau và Quay lại
        void ShopMenu(int page)
        {
            buttons.Clear();
            int first = page * ShopPageSize;
            for (int i = first; i < shop.Count && i < first + ShopPageSize; i++)
            {
                NpcOption option = shop[i];
                buttons.Add(new DialogChoice(OptionLabel(option), () => Choose(option)));
            }
            if (first + ShopPageSize < shop.Count)
                buttons.Add(new DialogChoice(Lang.GetSplitText("Trang sau|Next page"), () => ShopMenu(page + 1)));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () =>
            {
                if (page > 0) ShopMenu(page - 1);
                else Root(true);
            }));
            view.Say(Lang.GetSplitText("Muốn mua gì?|What will it be?"), buttons, true);
        }

        // ---------------- Nhiệm vụ ----------------

        /// <summary>Danh sách việc ở NPC này. header: câu NPC nói (null = câu mặc định).</summary>
        void QuestList(string header)
        {
            QuestSystem.QuestsAt(npc, quests);
            buttons.Clear();
            foreach (QuestTemplate q in quests)
            {
                QuestTemplate quest = q;
                QuestStatus st = QuestSystem.StatusOf(q);
                string title = T(q.title);
                if (st == QuestStatus.Ready && q.TurnInNpc == npc.name)
                    buttons.Add(new DialogChoice(Lang.Format("[Xong] {0}|[Done] {0}", title), () => TurnIn(quest)));
                else if (st == QuestStatus.Active || st == QuestStatus.Ready)
                    buttons.Add(new DialogChoice(Lang.Format("[Đang làm] {0}|[Active] {0}", title), () => Progress(quest)));
                else if (QuestSystem.TierAllows(q))
                    buttons.Add(new DialogChoice(Lang.Format("[Mới] {0}|[New] {0}", title), () => Offer(quest)));
                else
                    buttons.Add(new DialogChoice(Lang.Format("[Khóa] {0}|[Locked] {0}", title), () => Refuse(quest)));
            }
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () => Root(true)));

            if (header == null)
                header = quests.Count == 0 ? Lang.GetSplitText("Hiện ta không có việc gì nhờ ngươi.|I have nothing for you right now.")
                                           : Lang.GetSplitText("Ngươi muốn hỏi việc nào?|Which task are you asking about?");
            view.Say(header, buttons);
        }

        void Offer(QuestTemplate q)
        {
            // giao việc xong: người chơi đáp (thái độ) nếu nhiệm vụ có offerChoices, rồi mới tới bước Nhận / Để sau
            view.Pages(T(q.offer), () =>
            {
                string last = q.offer != null && q.offer.Length > 0 ? T(q.offer[q.offer.Length - 1]) : T(q.title);
                Ask(last, q.offerChoices, Lang.GetSplitText("Để sau|Later"), () => QuestList(null), () => OfferSummary(q));
            });
        }

        void OfferSummary(QuestTemplate q)
        {
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Nhận việc|Accept"), () => Accept(q)));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Để sau|Later"), () => QuestList(null)));
            view.Say(Summary(q), buttons);
        }

        // ---------------- Lựa chọn ----------------

        /// <summary>
        /// Cho người chơi chọn câu đáp. Câu đã chọn rồi (cờ một lần) ẩn đi; chưa đủ điều kiện thì nút mờ kèm lý do.
        /// Không còn câu nào thì đi thẳng tới after. back: nút thoát (nhãn backLabel). Chọn xong: NPC đáp, hiện hậu quả, rồi after.
        /// </summary>
        void Ask(string prompt, List<StoryChoice> choices, string backLabel, System.Action back, System.Action after)
        {
            buttons.Clear();
            if (choices != null)
            {
                foreach (StoryChoice c in choices)
                {
                    if (!QuestSystem.IsShown(c)) continue;
                    StoryChoice choice = c;
                    string why = QuestSystem.Explain(c.condition);
                    buttons.Add(why == null
                        ? new DialogChoice(T(c.label), () => Pick(choice, after))
                        : new DialogChoice(T(c.label) + " (" + why + ")", null, false));
                }
            }
            if (buttons.Count == 0)
            {
                after();
                return;
            }
            buttons.Add(new DialogChoice(backLabel, () => back()));
            view.Say(prompt, buttons, true);
        }

        void Pick(StoryChoice c, System.Action after)
        {
            if (!QuestSystem.Choose(c, out string summary))
            {
                after();
                return;
            }
            RefreshAffinity();
            view.Pages(T(c.reply), () =>
            {
                if (QuestSystem.IsEnemy(npc.name))
                {
                    Shunned(summary);
                    return;
                }
                if (summary == null)
                {
                    after();
                    return;
                }
                buttons.Clear();
                buttons.Add(new DialogChoice(Lang.GetSplitText("Tiếp|Continue"), () => after()));
                view.Say("<size=85%>" + summary + "</size>", buttons);
            });
        }

        /// <summary>NPC vừa thành kẻ thù giữa cuộc nói chuyện: nói câu cuối rồi chỉ còn nút đi.</summary>
        void Shunned(string summary)
        {
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Rời đi|Leave"), view.Close));
            string say = QuestSystem.ShunLineOf(npc);
            if (summary != null) say += "\n<size=80%>" + summary + "</size>";
            view.Say(say, buttons);
        }

        void Accept(QuestTemplate q)
        {
            if (!QuestSystem.Accept(q))
            {
                QuestList(null);
                return;
            }
            QuestList(Lang.Format("Đã nhận \"{0}\".|Accepted \"{0}\".", T(q.title)) + "\n<size=85%>" + QuestSystem.ProgressText(q) + "</size>");
        }

        /// <summary>Chưa đủ thân thiện: NPC từ chối, kèm gợi ý cần bậc nào.</summary>
        void Refuse(QuestTemplate q)
        {
            text.Clear();
            text.Append(string.IsNullOrEmpty(npc.refuseLine)
                ? Lang.GetSplitText("Ta chưa đủ tin ngươi để nhờ việc này.|I don't trust you enough for this yet.")
                : T(npc.refuseLine));
            text.Append("\n<size=80%>").Append(Lang.Format("(Cần thân thiện: {0} - hiện là {1})|(Needs: {0} - currently {1})",
                Affinity.NameOf(q.minTier), Affinity.NameOf(QuestSystem.TierOf(npc)))).Append("</size>");
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () => QuestList(null)));
            view.Say(text.ToString(), buttons);
        }

        void Progress(QuestTemplate q)
        {
            text.Clear();
            if (!string.IsNullOrEmpty(q.inProgress)) text.Append(T(q.inProgress)).Append('\n');
            text.Append("<size=85%>").Append(QuestSystem.ProgressText(q)).Append("</size>");
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Bỏ nhiệm vụ|Abandon quest"), () => ConfirmAbandon(q)));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () => QuestList(null)));
            view.Say(text.ToString(), buttons);
        }

        void ConfirmAbandon(QuestTemplate q)
        {
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Bỏ|Abandon"), () =>
            {
                QuestSystem.Abandon(q);
                RefreshAffinity();
                // phạt bỏ việc làm thân thiện tụt tới mức kẻ thù: hết chuyện để nói
                if (QuestSystem.IsEnemy(npc.name)) Shunned(null);
                else QuestList(null);
            }));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Không|No"), () => Progress(q)));
            view.Say(Lang.Format("Bỏ ngang \"{0}\"? Ta sẽ phật lòng đấy.|Give up on \"{0}\"? I'll be disappointed.", T(q.title))
                     + "\n<size=80%>" + Lang.Format("(Thân thiện -{0})|(Affinity -{0})", Affinity.AbandonPenalty) + "</size>", buttons);
        }

        void TurnIn(QuestTemplate q)
        {
            if (!QuestSystem.HasOutcomes(q))
            {
                Complete(q, null);
                return;
            }

            // nhiều cách giải quyết: người chơi chọn; cách chưa đủ điều kiện thì mờ kèm lý do
            buttons.Clear();
            foreach (QuestOutcome o in q.outcomes)
            {
                if (o == null) continue;
                QuestOutcome outcome = o;
                string why = QuestSystem.Explain(o.condition);
                string label = string.IsNullOrEmpty(o.ending) ? T(o.label) : Lang.Format("[Kết cục] {0}|[Ending] {0}", T(o.label));
                buttons.Add(why == null
                    ? new DialogChoice(label, () => ConfirmOutcome(q, outcome))
                    : new DialogChoice(label + " (" + why + ")", null, false));
            }
            buttons.Add(new DialogChoice(Lang.GetSplitText("Để sau|Later"), () => QuestList(null)));
            view.Say(string.IsNullOrEmpty(q.turnInPrompt)
                ? Lang.GetSplitText("Ngươi định giải quyết thế nào?|How will you resolve this?")
                : T(q.turnInPrompt), buttons);
        }

        /// <summary>Kết cục chương không làm lại được: hỏi lại trước khi chốt. Cách giải quyết thường thì chốt luôn.</summary>
        void ConfirmOutcome(QuestTemplate q, QuestOutcome o)
        {
            if (string.IsNullOrEmpty(o.ending))
            {
                Complete(q, o);
                return;
            }
            buttons.Clear();
            buttons.Add(new DialogChoice(Lang.GetSplitText("Chốt lựa chọn|Confirm"), () => Complete(q, o)));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Nghĩ lại|Reconsider"), () => TurnIn(q)));
            view.Say("<b>" + T(o.label) + "</b>\n<size=85%>"
                     + Lang.GetSplitText("Đây là kết cục của chương: chọn rồi không đổi lại được.|This is the chapter ending: it can't be undone.")
                     + "</size>", buttons);
        }

        void Complete(QuestTemplate q, QuestOutcome o)
        {
            if (!QuestSystem.TurnIn(q, o, out QuestReward reward))
            {
                QuestList(null);
                return;
            }
            RefreshAffinity();
            string[] pages = o != null && o.complete != null && o.complete.Length > 0 ? o.complete : q.complete;
            view.Pages(T(pages), () =>
            {
                buttons.Clear();
                // lựa chọn vừa rồi biến chính người đang nói thành kẻ thù: hết chuyện để nói
                if (QuestSystem.IsEnemy(npc.name)) buttons.Add(new DialogChoice(Lang.GetSplitText("Rời đi|Leave"), view.Close));
                else buttons.Add(new DialogChoice(Lang.GetSplitText("Tiếp|Continue"), () => QuestList(null)));
                view.Say(RewardText(reward), buttons);
            });
        }

        /// <summary>Tóm tắt khi giao việc: mục tiêu và thưởng dự kiến theo bậc thân thiện hiện tại.</summary>
        string Summary(QuestTemplate q)
        {
            text.Clear();
            text.Append("<b>").Append(T(q.title)).Append("</b>\n<size=85%>").Append(QuestSystem.ProgressText(q));
            text.Append('\n').Append(Lang.Format("Thưởng: {0}|Reward: {0}", QuestSystem.RewardPreview(q))).Append("</size>");
            return text.ToString();
        }

        string RewardText(QuestReward r)
        {
            text.Clear();
            if (r.ending != null) text.Append("<b>").Append(Lang.Format("Kết cục: {0}|Ending: {0}", r.ending)).Append("</b>\n");
            var got = new StringBuilder();
            if (r.gold > 0) got.Append(Lang.Format("{0} vàng|{0} gold", r.gold));
            if (r.exp > 0) got.Append(got.Length > 0 ? ", " : "").Append(r.exp).Append(" EXP");
            if (r.items != null) got.Append(got.Length > 0 ? ", " : "").Append(r.items);
            if (got.Length == 0) got.Append(Lang.GetSplitText("lời cảm ơn|their thanks"));
            text.Append(Lang.Format("Nhận được: {0}|Received: {0}", got));
            text.Append("\n<size=80%>").Append(Lang.Format("Hệ số thưởng {0}: x{1}|Reward rate {0}: x{1}",
                Affinity.NameOf(r.tier), Affinity.RewardScaleOf(r.tier).ToString("0.##")));
            if (r.affinity != null) text.Append('\n').Append(Lang.Format("Thân thiện: {0}|Affinity: {0}", r.affinity));
            if (r.enemies != null) text.Append("\n<color=#FF8C8C>").Append(Lang.Format("Thành kẻ thù: {0}|Now enemies: {0}", r.enemies)).Append("</color>");
            if (r.missedBonus) text.Append('\n').Append(Lang.GetSplitText("Thân thiết hơn nữa thì còn có quà thêm.|Grow closer for a bonus gift."));
            text.Append("</size>");
            return text.ToString();
        }

        // ---------------- Tặng quà ----------------

        void GiftMenu()
        {
            QuestSystem.GiftsInBag(npc, gifts);
            buttons.Clear();
            foreach (NpcGift g in gifts)
            {
                NpcGift gift = g;
                buttons.Add(new DialogChoice(Lang.Format("Tặng {0} (+{1})|Give {0} (+{1})", ItemLabel(g.item), g.affinity), () => Give(gift)));
            }
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () => Root(true)));

            if (gifts.Count > 0)
            {
                view.Say(Lang.GetSplitText("Ồ, ngươi có quà cho ta sao?|Oh, you brought me a gift?"), buttons);
                return;
            }
            var likes = new StringBuilder();
            for (int i = 0; i < npc.gifts.Count; i++)
            {
                if (i > 0) likes.Append(", ");
                likes.Append(ItemLabel(npc.gifts[i].item));
            }
            view.Say(Lang.Format("Ta thích {0}. Ngươi chưa có món nào cả.|I like {0}. You don't have any of them.", likes), buttons);
        }

        void Give(NpcGift gift)
        {
            int gained = QuestSystem.GiveGift(npc, gift);
            RefreshAffinity();
            QuestSystem.GiftsInBag(npc, gifts);
            buttons.Clear();
            if (gifts.Count > 0) buttons.Add(new DialogChoice(Lang.GetSplitText("Tặng thêm|Give another"), GiftMenu));
            buttons.Add(new DialogChoice(Lang.GetSplitText("Quay lại|Back"), () => Root(true)));
            view.Say(gained > 0
                ? Lang.GetSplitText("Cảm ơn ngươi, ta rất quý món này!|Thank you, I treasure this!") + "\n<size=80%>"
                  + Lang.Format("(Thân thiện +{0})|(Affinity +{0})", gained) + "</size>"
                : Lang.GetSplitText("Ta đã quý ngươi hết mức rồi, giữ lại mà dùng.|We're already as close as can be. Keep it."), buttons);
        }

        static string ItemLabel(string item) => QuestSystem.ItemLabel(item);
    }
}
