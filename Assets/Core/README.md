# Core — hệ thống chép từ GAME_2D

Code hệ thống của GAME_2D chép sang Game: bảng dữ liệu (database) + luật chơi đứng riêng được (túi đồ, trang bị, vàng,
cấp / EXP / cây nhân vật, nâng cấp vũ khí, nhiệm vụ / thân thiện / cốt truyện, NPC), kèm data (`Data/`) và khung UI
(`UI/`). Phần 2D (Player / Enemy / Npc entity 2D, map, đánh nhau 2D) không chép: nhân vật, quái, NPC, map là của Game.
Trước đây thư mục này tên `Game2D` (theo nguồn gốc code); đã đổi thành `Core` cho đúng là phần lõi của dự án.
Tài liệu gốc: `GAME_2D/Docs/` (03-Stats, 07-Items, 08-Monsters, 09-Skills, 10-Weapons, 11-Npc, 12-Story, 13-Drops).

## Assembly

`Core.Data.asmdef` (namespace `Game.*`, `autoReferenced`). Assembly-CSharp thấy được, nhưng muốn dùng thì phải `using`:

```csharp
using Game;          // CoreSystems
using Game.Players;  // PlayerProfile, PlayerData, OptionDatabase, PlayerUpgradeTree, PlayerProgress
using Game.Items;    // ItemDatabase, DropDatabase, Inventory, Equipment
using Game.Skills;   // SkillDatabase, WeaponDatabase, WeaponUpgrade, PlayerSkill
using Game.Story;    // QuestSystem, QuestDatabase, StoryState
using Game.Npcs;     // NpcSystem, NpcDatabase, INpcActor
using Game.Enemies;  // MonsterDatabase
using Game.Core;     // Notify (toast), GameMode, EntityData
```

Game cũng có các class global cùng tên (`Player`, `PlayerStats`, `MyReader`, `MyWriter`, `PlayerState`…). Trong file không
có namespace, tên global luôn thắng; khi cần bản Core thì viết đầy đủ (`Game.Net.MyWriter`).

## Dùng thế nào

1. Bảng (.asset) và `PlayerData` gán ở `Resources/LocalPlaySettings`. `OnlineCore` (`Src/Scripts/_Main/Online/`) đăng ký
   bảng (`CoreSystems.Use`) và dựng người chơi mình (`CoreSystems.Me`, kiểu `PlayerProfile`) từ hồ sơ server gửi
   (`CoreSystems.StartOnline`); mọi thay đổi gửi lên server qua `NetSync.Remote`, server gửi bản chuẩn về (`ProfileSync`).
   Game chỉ có chế độ online: chơi local (`LocalPlay`, `LocalCombat`, `LocalSave`) đã bỏ.
   Server giả lập cũ đã rời client, mã nguồn giữ ở `ServerJava/reference/LocalServer` để đối chiếu luật.
2. Nhân vật 3D của Game nghe `Me.StatChanged` (máu / mana), `Me.OnBonusChanged` (đọc `Me.Bonus`: tốc chạy, sát thương,
   giáp…), `Me.OnWeaponChanged`, `Me.OnGoldChanged`.
3. Thưởng hạ quái do server tính. `CoreSystems.GrantKillRewards(...)` (đếm nhiệm vụ, cộng EXP / vàng, đồ vào túi) còn
   là luật tham chiếu cho server giả lập, client không gọi.
4. NPC của Game gọi `NpcSystem.Talk(npcId, gameObject)`. Component NPC cài `INpcActor` để quay người khi nói chuyện;
   UI hội thoại nghe `NpcSystem.Opened`, rồi gọi `NpcSystem.Use` (hồi máu, mua đồ) và `QuestSystem.Accept / TurnIn / Choose`.
5. UI toast đăng ký `Notify.Toasted`. Muốn dịch chữ thì gán `StoryText.Localize` (mặc định lấy phần trước dấu `|`).

Nhiệm vụ / NPC / mua bán chạy được ở client online (tính trước tại máy rồi gửi gói `CORE`) và trong
server giả lập (`NetSync.CanAct`).

## Nguồn từng file

| File | Từ GAME_2D | Ghi chú |
|---|---|---|
| `Net/MyReader, MyWriter, NetLog` | `Net/Core/` | Chỉ phần đọc/ghi nhị phân mà `ToBytes` / `Read` cần. Session / Cmd không chép (giao thức khác server của Game). |
| `Map/SpriteData.cs` | `Map/SpriteData.cs` | Bảng id → sprite để tra icon. |
| `Core/EntityTypes, Notify, GameMode` | `Core/` | giữ nguyên |
| `Anim/AnimData.cs` | `Anim/AnimData.cs` | Chỉ script (`SkillDatabase` cần `AnimSlot`). |
| `Item, Skill, Player/*` (trừ PlayerProfile), `Story/*` (trừ QuestSystem) | giữ nguyên | |
| `Enemy/MonsterDatabase.cs`, `Npc/NpcDatabase.cs` | sửa 3 dòng | `prefab` / `defaultPrefab` đổi từ `Enemy` / `Npc` (component 2D) thành `GameObject`. |
| `Story/QuestSystem.cs` | sửa nhẹ | `Player` → `PlayerProfile`, `GameController.Instance.Me` → `CoreSystems.Me`, `OnMonsterKilled(Enemy)` → `OnMonsterKilled(int templateId)`. |
| `Npc/NpcSystem.cs` | sửa nhẹ | Như trên; `Talk(MapObject)` → `Talk(int npcId, GameObject o = null)`, event `Opened` mang `GameObject`, component Npc 2D → `INpcActor`. |
| `Player/PlayerProfile.cs` | **mới**, tách từ `Player/Player.cs` | Phần dữ liệu + luật của Player: túi, trang bị, vàng, Loot, skill slot, vũ khí + cây nâng cấp, Bonus / GearBonus, máu / mana. Code giữ như bản gốc, bỏ di chuyển / anim / đánh nhau 2D. |
| `CoreSystems.cs` | **mới**, tách từ `Game/GameController.cs` | Đăng ký bảng, `StartOnline` (dựng người chơi từ hồ sơ server), `GrantKillRewards` (đồ vào túi thay vì rơi đất), hồi máu, lên cấp hồi đầy. |
| `Npc/INpcActor.cs` | **mới** | Thay cho `Npc.OnTalk / OnShun` của entity 2D. |

## UI (`UI/`, asmdef `Core.UI`, namespace `Game.UI`)

Khung UI của GAME_2D: `UIManager` + `PanelBase` (panel theo `UIID`, tạo từ `UI/Data/Panel_Config`), `PopupMN` (popup theo
`PopupID`, `UI/Data/Popup_Config`, nhận toast từ `Notify`), `UIStack`, nút (`Btn/`), danh sách cuộn tái dùng (`Scroll/`),
chữ + đa ngôn ngữ (`Text/`, `Lang` đọc `UI/Resources/Lang/LangTable`). Không đụng UI cũ của Game (PopupMN / Lang global).

Giao diện theo Space Exploration GUI Kit (`Assets/Space_Exploration_GUI_Kit`). `Tools/Core/UI/Apply UI Kit` (`Editor/UIKitSetup.cs`) cắt
9-slice cho sprite kit, tạo font `UI/Fonts/Asap-Bold SDF` (Asap có tiếng Việt; Righteous của kit không có nên không dùng), tạo
`UI/Resources/UITheme` cho UI dựng bằng code, rồi áp vào prefab nền, panel Core, prefab UI cũ. Chạy lại được; prefab thêm mới hoặc dựng
lại thì chạy lại menu này (Build Quest UI, Build Gear Upgrade UI đã tự áp). Nút mặc định là nút phẳng tím (chữ trắng, code tô màu chữ
được), nút màu của kit chỉ dành cho hành động chính.

Panel đã chép: Hành trang (+ popup thông tin vật phẩm), Cây nâng cấp nhân vật, Nâng cấp vũ khí, Nhật ký + theo dõi nhiệm vụ,
Cài đặt, Hộp thoại NPC (`NpcDialog`, tự nghe `NpcSystem.Opened`). Prefab `UI/Prefabs/GameCanvas` = canvas + HudMenu (nút Menu
mở các panel) + gốc Popup; chế độ local tạo nó lúc vào map (`LocalPlaySettings.uiCanvas`).

| Sửa so với GAME_2D | |
|---|---|
| `InventoryPanel`, `ItemInfoPopup`, `PlayerUpgradePanel`, `WeaponUpgradePanel`, `SkillEquipPanel`, `SkillText` | `Player` (entity 2D) → `PlayerProfile`, `AnimEquipPanel.FindPlayer()` → `CoreSystems.Me`. Hình nhân vật / tư thế skill (flipbook của CharacterAnimator 2D) để trống. |
| `NpcDialog.Show` | nhận `GameObject` thay cho `MapObject` (ảnh mặt NPC: model 3D không có thì để trống). |
| `GameUIManager` | bỏ phần camera 2D, MapSelect, InfoPlayer. |
| `GameCanvas.prefab` | gỡ HUD điều khiển 2D (joystick, nút đánh / skill / nhảy / lướt / tương tác, GameUI, CameraDebug). Menu HUD mở được mọi panel (Bản đồ, Hành trang, Kỹ năng, Nâng cấp, Tư thế, Nhiệm vụ, Cài đặt). |
| `Panel_Config.asset` | chỉ giữ các panel dùng trong Game (11 panel, gồm Nhiệm vụ + khung theo dõi nhiệm vụ). |
| `Core/InputGate.cs`, `Core/NumberCache.cs` | chép thêm vào `Core.Data` (UI cần). `InputGate.IsBlocked` = UI Core đang mở popup / panel chặn điều khiển. |

### Panel viết lại nguồn dữ liệu cho Game (`Assets/_Project/Src/Scripts/_Main/CoreUI/`, Assembly-CSharp)

Năm panel gắn với nhân vật / map nên phải đọc thẳng dữ liệu của Game (Player, GameController, Map_Template, PlayerAnimData);
asmdef `Core.UI` không tham chiếu được Assembly-CSharp nên chúng nằm ở đây. Giữ nguyên tên lớp, namespace `Game.UI`,
tên field và GUID (.meta) của bản GAME_2D để prefab gốc (`UI/Prefabs/Panel/`) nối đúng.

| Panel | Nguồn bên Game |
|---|---|
| `InfoPlayerPanel` (HUD) | `Player` của Game: tên, HP / MP, chết, cờ PK, vị trí, trạng thái, mục tiêu. `PlayerProfile`: cấp / EXP / điểm, vàng, chỉ số cộng, vũ khí, túi. Chân dung = icon tư thế đứng yên trong PlayerAnimData. |
| `MiniMapPanel` (HUD) | Ảnh chụp map từ trên xuống bằng camera trực giao (một lần mỗi khi map dựng xong, `GameController.OnMapLoaded`). Chấm: quái (`ListEnemy`), người chơi khác (`ListPlayer`), NPC (`LocalNpc.All`). |
| `MapSelectPanel` | `Map_Template.List_Map_Template`; Dịch chuyển = `Service.Map_Travel` (server phải có bundle của map). |
| `AnimEquipPanel` | `PlayerAnimData` + `HumanEntity.SetAnimVariant / SetAnimSet` (dòng "Bộ" đổi skin). Xem trước = icon tư thế. |
| `FocusTargetPanel` (HUD) | `Player.objFocus` (dò mỗi frame), khóa mục tiêu qua `Player.ActionOnChangeStatusLockTarget`. |

`Text/PlayerStatText` đổi `internal` → `public` để InfoPlayerPanel dùng được.

Chưa chép: GameUI (HUD điều khiển 2D), WorldUI / EntityGUI (tên, máu trên đầu entity 2D), CameraDebug.
`SkillEquipPanel.prefab` (GAME_2D không có) dựng từ `AnimEquipPanel.prefab`: cùng bố cục, thêm ô `Info` (mô tả + chỉ số) dưới tên skill.

## Data (`Data/`) và công cụ Editor (`Editor/`, asmdef `Core.Editor`)

Data vật phẩm / nâng cấp chép từ `GAME_2D/Assets/Data` (giữ GUID), cùng cấu trúc thư mục, gán sẵn vào
`Resources/LocalPlaySettings`:

| Asset | Nội dung |
|---|---|
| `Items/ItemDatabase`, `Items/DropDatabase` | 27 vật phẩm (ngọc, chìa, 6 vũ khí, 7 đồ mặc, skin), 6 bảng rơi đồ |
| `Player/OptionDatabase`, `Player/PlayerUpgradeTree` | 29 option (18 chỉ số nhân vật, 2 của bình, 9 của vũ khí), cây nâng cấp nhân vật 91 nút (cấp tối đa 50) |
| `Player/PlayerData` | hồ sơ khởi đầu: 17 đồ, vũ khí 1, skill; `Export Server Package` xuất thành `core/start_profile.json` của server |
| `Skills/SkillDatabase`, `Skills/WeaponDatabase` | 23 skill: 17 skill ô 1-4 là skill của Game (nhập bằng Import Game Skills, xem mục Skill), 6 đòn đánh thường theo vũ khí giữ từ GAME_2D; 6 vũ khí + cây nâng cấp vũ khí |
| `Sprites/SpriteData` + `Sprites/ItemIcons`, `../Art/` | 30 icon / hiệu ứng (đã lọc bỏ sprite map), ảnh của gói HyperLuminal / COPY SPRIGHT mà icon trỏ tới |

Excel cho game design ở `Docs/Core/` (cạnh `Assets/`): `Items_Data.xlsx`, `PlayerUpgradeTree_Nodes.xlsx`.

Công cụ Editor chép sang (đổi đường dẫn `Assets/Data` → `Assets/_Project/Core/Data`, `Docs/` → `Docs/Core/`):

| Menu | |
|---|---|
| Tools/Core/Items/Export Items Excel | ghi `Docs/Core/Items_Data.xlsx` từ data hiện có |
| Tools/Core/Items/Build Data From Excel | đọc Excel → ItemDatabase / WeaponDatabase / DropDatabase / option, rồi xuất `.bin` server |
| Tools/Core/Items/Export Server Data | `.bin` cho server vào `StreamingAssets/Core` (không lẫn với bundle gốc ở `Game/GameData`) |
| Tools/Core/Export All Server Data | xuất mọi thứ server cần vào `StreamingAssets/Core`: các `.bin` của Items, Quests, thêm `skills.bin`, `mapobjects.bin`, `gear.bin`, `tree.bin`, `world.bin`, `start_profile.json`. Sửa bảng cường hóa, skill, cây nhân vật hay bãi quái thì chạy lại |
| Tools/Core/Items/Build Drop Tables | bảng rơi đồ |
| Tools/Core/Player/Build Option Database, Validate Options | bảng option |
| Tools/Core/Player/Build Upgrade Tree, Rebuild Upgrade Tree | cây nâng cấp nhân vật |
| Tools/Core/Player/Test/... | cheat: lên cấp + điểm, xoá tiến trình (Play: áp lên CoreSystems.Me; không Play: ghi vào PlayerData) |
| Tools/Core/Items/Test/... | **mới**: thêm mọi vật phẩm, ngọc, vàng vào túi lúc Play |

Chỗ "gán vào GameController 2D" đổi thành gán vào `Resources/LocalPlaySettings` (`CoreHostAsset`). `ChestTools`,
`MonsterTools`, `MapExport`, `AnimSetup` (công cụ map / anim 2D) thay bằng bản nhỏ chỉ giữ đường dẫn / danh sách mà các
công cụ trên cần (`CoreEditorShims.cs`, `AnimSetup.cs`). Không chép: `WeaponSetup`, `SkillSetup`, `WeaponItemSetup`,
`ArmorItemSetup`, `SkinItemSetup` (dựng data từ sprite gói COPY SPRIGHT 2D; sửa data thì qua Excel).

Nối vào nhân vật Game (`LocalPlayHud`): máu / mana tối đa theo cây + đồ, tốc chạy = 4 × MoveSpeedScale, mặc vật phẩm Skin
thì đổi bộ anim PlayerAnimData theo `ItemTemplate.SkinSetId` (bảng chưa có bộ đó thì báo toast). Sát thương, chí mạng,
giáp... mới nằm trong `PlayerProfile.Bonus`, chưa có combat local để dùng.

## Vật phẩm dùng được, mana, đồ rơi, ô dùng nhanh

| | |
|---|---|
| `ItemKind` | thêm `HpPotion` (value = máu hồi), `MpPotion` (value = mana hồi), `Buff` (value = số giây, `options` = chỉ số cộng trong lúc đó). `ItemTemplate.Usable` |
| Data | 9 vật phẩm id 28-36: bình máu / mana nhỏ - vừa - lớn (50 / 150 / 400 máu, 30 / 80 / 200 mana), thuốc cuồng nộ (+25% tốc đánh), thuốc gió (+25% tốc chạy), thuốc sức mạnh (+20% sát thương), 60 giây. Dựng bằng **Tools/Core/Items/Build Potion Items** (vẽ icon, SpriteData, ItemDatabase, rơi đồ, PlayerData; chạy lại được), sửa số về sau qua Excel |
| `PlayerProfile` | `UseItem` (chờ 1 giây: bình máu chung, bình mana chung, thuốc theo từng loại), thuốc đang có tác dụng (`Buffs`, cộng vào `Bonus`, hết giờ / bị hạ thì mất), `TrySpendMana`, 2 ô dùng nhanh (`Get/SetQuickItem`, lưu ở `PlayerData.quickItems`) |
| Mana | skill ô 1-4 tốn `SkillInfo.manaUse` (đánh thường không tốn), không đủ thì không tung. Hồi `LocalPlaySettings.manaRegen` / giây (`CoreSystems.BaseMpRegen`) + bình mana |
| Rơi đồ | bảng chung `monster` (`DropDatabase.MonsterDefaultTable`) cho quái chưa có bảng riêng. Đồ nằm trên đất (`GroundLoot`, mỗi món một id trong map), tới gần thì hút về và nhặt; túi đầy thì nằm lại; 60 giây (`dropLifetime`) biến mất; tối đa 40 món (`maxDrops`); viền sáng tắt trên mobile (`dropGlow`). Gói `GROUND_ITEM` (60, số tạm) sub `SubAdd / SubRemove / SubPick / SubList`, gửi `Service.GroundItemPick(id)`, nhận `GroundLoot.ReadMessage` |
| Atlas icon | **Tools/Core/Items/Build Icon Atlas**: mọi sprite loại Icon của SpriteData vào `Data/Sprites/ItemIcons.spriteatlasv2` (Sprite Packer = Sprite Atlas V2). Mở hành trang 27 ô: 51 → 20 lượt vẽ. Build Potion Items tự chạy lại |
| Skill của Game | **Tools/Core/Skills/Import Game Skills** (`Editor/GameSkillSetup.cs`): 17 skill của Game (`Skill_Template.LoadData()`, prefab `SkillPref/Skill_<idModel>_...`) thành skill ô 1-4 của SkillDatabase, thay 6 skill ô chép từ GAME_2D. Lấy từ bảng Game: hiệu ứng (`model3D` = idModel), loại (Bullet_Nor → Projectile, Melee, Cast → Area, Cast2 → Area tầm 0 = tại chỗ), tầm / bán kính (mét / 2 = ô). Bảng Game không có nên đặt theo loại: % sát thương, mana, hồi chiêu, mô tả, tư thế tung, 5 cấp. Id 1-5, 8 dùng lại cho skill Game có cùng hiệu ứng mà skill cũ đang mượn (file lưu cũ vẫn ra cùng hình đánh), còn lại id 13-23. Chạy lại được, xong thì chạy Build Skill Icons |
| Icon skill | **Tools/Core/Skills/Build Skill Icons** (`Editor/SkillIconSetup.cs`): vẽ huy hiệu 128x128 cho mọi skill vào `Data/Sprites/SkillIcons` (skill ô vẽ theo hiệu ứng 3D của Game bằng SDF, đánh thường là ảnh vũ khí của ItemIcons; icon của skill đã bỏ khỏi bảng bị xóa), thêm vào SpriteData loại Icon, gán `SkillTemplate.idIcon`, dựng lại atlas. Có ảnh vẽ tay thì ghi đè png cùng tên. Hiện ở lưới bảng Kỹ năng và nút skill 1-4 trên HUD (`SkillButton.SetIcon`: có icon thì nút chỉ hiện hình, không chữ). Nút Đánh hiện icon đòn đánh thường theo vũ khí đang cầm (`InputActionMN.RefreshAttackLabel`) |
| UI | `QuickItemBar` (HUD, cạnh cụm nút skill): 2 ô, bấm / phím Z, X để dùng; ô trống bấm (hoặc giữ ô) mở bảng icon để gán / bỏ. Dòng thuốc đang có tác dụng phía trên. `ItemInfoPopup`: nút Dùng, Gán ô nhanh. Hành trang thêm tab Tiêu hao. Nút skill hiện số MP |

## Cường hóa, khoét lỗ, khảm đá

Làm thêm cho trang bị, khác cây nâng cấp vũ khí và cây nhân vật. Giao thức và bảng số đầy đủ cho server: doc "Giao thức lệnh mới cho server", mục Cmd 63 ITEM và mục Cường hóa, khoét lỗ, khảm đá.

| | |
|---|---|
| Dữ liệu | Cấp và lỗ nằm ở từng món: `ItemStack` (`level`, `sockets`, `gem0..2`), `Equipment.Stack(slot)`, lưu ở `PlayerData.bag` và `equipGear`. Chỉ vũ khí và đồ mặc không xếp chồng làm được (`ItemTemplate.Upgradable`), skin thì không |
| Luật | `PlayerProfile`: `UpgradeGear`, `OpenSocket`, `InlayGem`, `RemoveGem`, mỗi hàm có `Check...` tương ứng. Chỗ của món là `spot`: >= 0 là ô hành trang, âm là ô trang bị (`GearSpot`) |
| Số | `Data/Items/GearUpgradeTable.asset`, sửa trong Inspector: 10 cấp +1..+10, 3 lỗ, đá may mắn +10%, gỡ đá 200 vàng. Mỗi cấp cộng 8% option sẵn có; vũ khí cộng 3% sát thương đòn thường. Từ +6 trượt thì tụt cấp, đá bảo vệ giữ được (1 viên ở +6, 16 viên ở +10). Món không bao giờ mất |
| Cân bằng | Đo bằng bot chơi thật: 11,7 quái / phút, giả định người chơi đạt 7 quái / phút. Trung bình mỗi món: +5 tốn 3.800 vàng; +10 tốn 405.000 vàng nếu liều, 127.000 vàng nếu dùng đủ đá từ +5 (khoảng 70 phút ở Lv42). Sửa bảng thì mô phỏng lại, vì đá bảo vệ quá rẻ làm đường an toàn rẻ hơn hẳn đường liều |
| Đá | `ItemKind.Stone` (id 37-40, `GearStone`), `ItemKind.Jewel` (id 41-58: 6 loại × 3 bậc, `options` là chỉ số). Mỗi món một viên mỗi loại (`ItemTemplate.SameJewel`). Chỉ số của đá chỉ tính khi món đang mặc, cấp cường hóa không nhân lên đá. Khảm đủ 8 món: bộ đánh +13 / 26 / 49% sát thương theo bậc, bộ thủ +17 / 35 / 54% sống sót (so với nhân vật Lv50 705 máu) |
| Nguồn đá | Rơi từ quái và rương (sheet Rơi đồ của `Items_Data.xlsx`). Ông Hỏa bán 4 loại đá, Thầy bói bán đá khảm bậc I. Hàng bán của NPC gom vào nút Cửa hàng có chia trang (`NpcDialog.ShopMenu`) nên không chiếm chỗ trong `NpcDatabase.MaxOptions` |
| Mạng | Cường hóa và khoét lỗ có tỉ lệ nên client không làm trước: trả `GearOutcome.Pending`, khóa nút, server gửi `PROFILE.SYNC` rồi `UPGRADE_RESULT` / `SOCKET_RESULT` (`OnGearUpgraded`, `OnSocketOpened`). Khảm và gỡ đá chạy như lệnh hồ sơ thường |
| UI | `ItemInfoPopup` có nút Cường hóa, mở `GearUpgradePanel`; nút Khảm đá chuyển sang `GearSocketPanel`, chọn đá bằng danh sách cuộn. Ô đồ hiện +N, popup hiện từng lỗ |
| Dựng lại | **Tools/Core/Items/Build Gear Upgrade** (đá, icon, bảng luật, rơi đồ, hàng bán; số đã chỉnh được giữ), **Tools/Core/UI/Build Gear Upgrade UI** (hai prefab, Panel_Config, nút ở popup). Thêm đồ thử: **Tools/Core/Items/Test/Thêm đá cường hóa và 20000 vàng** |

## Object của map (bàn ghế), ngồi ghế

| | |
|---|---|
| Data | `Game.Map.MapObjectDatabase` (`Data/MapObjects/MapObjectDatabase.asset`): loại object (id, tên, `MapObjectKind` Decor / Seat, prefab, số chỗ ngồi) + chỗ đặt theo map (`uid` duy nhất trong map, mapId, x/z, góc). `ToBytes` / `Read` / `ReadPlacements` cho server. Gán ở `LocalPlaySettings.mapObjects` |
| Mẫu | **Tools/Core/Map Objects/Build Sample Objects + Portals** (`Assets/_Project/Editor/MapObjectSetup.cs`): ghế gỗ, ghế dài 2 chỗ, bàn (ghép khối hộp, chưa có model thật) + góc nghỉ cạnh chỗ xuất hiện ở map 0-6; cổng dịch chuyển. Có model thật thì thay prefab trong bảng, giữ component `Seat` + các `SeatPoint` |
| Cổng dịch chuyển | `MapObjectKind.Portal`, component `Portal`; chỗ đặt mang `targetMapId` + `targetUid` (cổng nối bên kia). Chuỗi **0 ⇄ 2 ⇄ 3 ⇄ 4 ⇄ 5 ⇄ 6** (map 1 trùng map 0 nên không nối): cổng xanh (40, -45) đi map sau, cổng vàng (-40, -45) về map trước, mặt nhìn bắc. Bấm F "Đi tới Grass Field (F)": `Service.PortalEnter(uid)` (lệnh `PORTAL` 57), điều kiện qua cổng do server xét. Hiệu ứng vòng (Hovl Studio Portal) quay về phía camera |
| Mốc chuyển khu | `MapObjectKind.Zone`, component `ZoneBoard`, mỗi map một cột đá ở (0, -30), dựng bằng **Tools/Core/Map Objects/Build Zone Boards**. Bấm F "Chuyển khu (F)" mở `ZonePopup`: online `Service.ZoneList()` (`MAP` / `ZONE_LIST`), bấm một khu thì `Service.ZoneChange(zone)` (`MAP` / `ZONE_CHANGE`), server trả `MAP_INFO` cùng map, khu mới, giữ chỗ đứng. Khu đang đứng ở byte zone của `MAP_INFO` (`GameController.chanel`) |
| Runtime (Assembly-CSharp, `Src/Scripts/_Main/MapObject/`) | `MapObject` (uid), `MapInteractable` (bấm F, cài `IInteractable`), `Seat` (chỗ ngồi = Transform con ở chỗ chân, forward = hướng mặt; nhớ ai ngồi chỗ nào), `MapObjectSystem` (dựng / dọn theo map, ngồi / đứng, gói mạng) |
| Ngồi | `PlayerStateEnum.SIT` + `PlayerSitState`: trượt vào chỗ ngồi, `SIT_DOWN` → `SITTING` → `SIT_UP` (act gửi server qua gói MOVE). Đi / nhảy / lướt / F thì đứng dậy; bị đánh, đánh, chết, đổi map thì rời ghế ngay. HUD (`LocalPlayHud`): "Ngồi (F): Ghế gỗ", đang ngồi thì "Đứng dậy (F)" |
| Anim | `AnimSlot.Sit` (3 clip `sit_down`, `sit_idle`, `sit_up`, bool `sit` + int `indexSit` trong `PlayerAC`). Chưa có anim ngồi nên `PlayerAnimSetup` mượn tạm `hurt_block` / `idle_block`; thêm bundle tên `sit_down` / `sit_idle` / `sit_up` rồi chạy Tools/Anim/Rebuild Anim Data + Controller là tự dùng |
| Online (chờ server) | `Cmd_Message.MAP_OBJECT` (59, số tạm) + sub `MapObjectSystem.SubList / SubSit / SubStand`. Gửi: `Service.MapObjectSit(uid, point)`, `MapObjectStand()` (đã gọi sẵn khi không phải local). Nhận: `GlobalMessageHandler` → `MapObjectSystem.ReadMessage` → `Load` / `ApplySit` / `ApplyStand`; người rời map tự trả ghế. Người khác ngồi: anim + vị trí đi theo gói MOVE như cũ (`OtherPlayer` đặt `indexSit`) |

## Tải dữ liệu về máy (`GameData`, `LoadingScene`, `ServerJava/src/game/data`)

Bản build không mang data game: template và bundle luôn tải từ server về `persistentDataPath` rồi nạp từ đó. Nguồn của gói là `Game/GameData`, chép sang `Builds/ServerData` bằng `Tools/Core/Export Server Package`.

| Bước | Gói | Việc |
|---|---|---|
| 1 | `CLIENT_INFO` → `CHECK_DATA_VERSION` | Phiên bản 3 loại template, rồi danh sách mọi file server có (type, id, phiên bản, số byte). Không có danh sách: client báo máy chủ chưa có dữ liệu game và dừng |
| 2 | `UPDATE_DATA` | Template lệch phiên bản hoặc mất file thì xin lại, lưu `GameData_{loại}.bytes`, đọc được rồi mới ghi phiên bản |
| 3 | `REQUEST_DATA` | `BuildDownloadList` đưa mọi file lệch phiên bản hoặc chưa có vào `LoadingScene.ListDownLoad`, tải lần lượt trước màn đăng nhập. Server trả theo khối vì bundle map lớn hơn giới hạn một gói (23 bit, khoảng 8 MB) |
| 4 | | 10 giây không nhận được khối nào thì hỏi lại, 3 lần vẫn không được hoặc server trả `total = -1` thì hiện popup, bấm OK để thử lại |

Phiên bản từng file nằm ở PlayerPrefs (khóa `type,id`, `VerData_{loại}`). `version` trong template vẫn còn trong định dạng nhưng không quyết định tải nữa. Bảng Core (vật phẩm, skill, cường hóa, cây nâng cấp...) đi bằng template loại 3 (`GameData.Type_Core_Template`) từ các table `tpl_*` trong MySQL: `CoreTemplates` lưu bản tải về, rồi `OnlineCore.ApplyTemplates` nhân bản từng bảng trong build và ghi đè theo khóa, nên đổi số liệu không cần build lại client, icon và prefab vẫn lấy từ build. Đẩy lên MySQL bằng `Tools/Core/Push Templates To MySQL`, xem `ServerJava/README.md`. Mọi chỉ số vật phẩm là `ItemTemplate.options` (id option + giá trị), kể cả lượng hồi của bình (`PlayerStat.RestoreHp`, `RestoreMp`); trong MySQL chúng là các hàng `tpl_item_options` có khóa ngoại tới `tpl_options`. Vũ khí cũng vậy: sát thương, hồi, tầm, mục tiêu gốc của đòn đánh thường là option 20-23 của item vũ khí, cấp của skill loại Basic chỉ ghi phần tăng thêm, đọc chỉ số đòn bằng `SkillTemplate.Stats`. Nút nâng cấp vũ khí (`WeaponUpgrade.effects`) dùng id option, `WeaponStats.Add` gộp lại, server (`game.combat`) áp vào đòn đánh thường (tầm, góc chém, số mục tiêu, đạn xuyên, số đạn, vụ nổ). Cường hóa không nhân option gốc của đòn. Dữ liệu cho server: `Tools/Core/Export Server Package` ghi bảng Core, template và bundle ở dạng gốc cùng `manifest.csv` vào `Builds/ServerData` (có `.zip`), `GameServer` đọc thư mục đó lúc khởi động. File gửi ở dạng gốc vì client tự mã hóa khi ghi.

## Test (`Tests/Editor/`, asmdef `Core.Tests`)

Test EditMode (Window > General > Test Runner > EditMode > Run All), chạy trong bộ nhớ, không cần scene / mạng:

| | |
|---|---|
| `QuestSystemTests` | luật nhiệm vụ trên bảng nhỏ dựng trong test: nhận / trả, Kill / Collect / Reach / Talk, hằng ngày, lặp lại, bậc thân thiện, kẻ thù, cách giải quyết, lựa chọn, theo dõi, đọc / ghi, `ProfileSync` |
| `QuestContentTests` | data thật: `QuestDatabase` hợp lệ, `StreamingAssets/Core/quests.bin` khớp asset, vật phẩm có trong `ItemDatabase`, người chơi mới làm hết mọi nhiệm vụ (log danh sách ra Editor.log) |
| `QuestWorld` | dựng thế giới thử: đăng ký bảng, một `PlayerProfile` mới, chạy QuestSystem qua `CoreSystems.AsServer` |

## Chưa có

- **Online:** server tin vị trí, chỗ tung đòn và khoảng cách tới cổng / NPC client gửi; qua cổng chưa quay đúng hướng (gói MAP_INFO không có hướng). Chiến đấu chỉ còn ở `ServerJava/src`. Luật cốt truyện có hai bản, sửa cùng lúc: C# (client tính trước) và `ServerJava/src`.
- **Dấu nhiệm vụ trên đầu NPC:** "có việc mới" và "chờ trả" dùng chung icon `GUI_QuestMark` (`Npc.RefreshMark` đã phân
  biệt được, chỉ thiếu hình).
- **Skill của Game:** chỉ có sát thương. Vầng sáng (hiệu ứng `Skill_10_AOE_Healing`) chưa hồi máu, Đóng băng / Băng giá chưa làm chậm; gắn skill chưa xét `levelRequire`.
- **Đánh nhau 2D** của GAME_2D (SkillCombat, SkillProjectile...): không chép, Game dùng combat 3D của mình.
