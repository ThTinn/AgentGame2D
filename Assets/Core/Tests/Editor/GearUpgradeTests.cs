using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Game.Items;
using Game.Players;
using Game.Skills;

namespace Game.Tests
{
    /// <summary>
    /// Cường hóa, khoét lỗ, khảm đá trên bảng nhỏ dựng trong test: cấp và đá đi theo món khi sắp xếp / mặc / tháo / lưu / đồng bộ,
    /// luật tốn đá và vàng, kết quả theo tỉ lệ 0% và 100% (để không phụ thuộc số ngẫu nhiên), chỉ số theo cấp và theo đá.
    /// </summary>
    public class GearUpgradeTests
    {
        const int Helmet = 1, Sword = 2, Skin = 3, Upgrade = 4, Lucky = 5, Protect = 6, Potion = 7, Drill = 8, Ruby = 9, Jade = 10;

        readonly List<KeyValuePair<FieldInfo, object>> previous = new List<KeyValuePair<FieldInfo, object>>();
        ItemDatabase items;
        GearUpgradeTable rules;
        PlayerProfile me;
        CoreSystems.ServerScope scope;

        [SetUp]
        public void SetUp()
        {
            items = ScriptableObject.CreateInstance<ItemDatabase>();
            items.items.Add(new ItemTemplate
            {
                id = Helmet, name = "helmet", kind = ItemKind.Armor, value = (int)EquipSlot.Head, maxStack = 1,
                options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.MaxHp, 100f) },
            });
            items.items.Add(new ItemTemplate { id = Sword, name = "sword", kind = ItemKind.Weapon, value = 1, maxStack = 1 });
            items.items.Add(new ItemTemplate { id = Skin, name = "skin", kind = ItemKind.Skin, value = 1, maxStack = 1 });
            items.items.Add(new ItemTemplate { id = Upgrade, name = "up", kind = ItemKind.Stone, value = (int)GearStone.Upgrade, maxStack = 99 });
            items.items.Add(new ItemTemplate { id = Lucky, name = "lucky", kind = ItemKind.Stone, value = (int)GearStone.Lucky, maxStack = 99 });
            items.items.Add(new ItemTemplate { id = Protect, name = "protect", kind = ItemKind.Stone, value = (int)GearStone.Protect, maxStack = 99 });
            items.items.Add(new ItemTemplate { id = Potion, name = "potion", kind = ItemKind.HpPotion, maxStack = 99,
                options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.RestoreHp, 10f) } });
            items.items.Add(new ItemTemplate { id = Drill, name = "drill", kind = ItemKind.Stone, value = (int)GearStone.Drill, maxStack = 99 });
            items.items.Add(new ItemTemplate
            {
                id = Ruby, name = "ruby", kind = ItemKind.Jewel, value = 1, maxStack = 2,
                options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.MaxHp, 50f) },
            });
            items.items.Add(new ItemTemplate
            {
                id = Jade, name = "jade", kind = ItemKind.Jewel, value = 1, maxStack = 99,
                options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.Crit, 4f) },
            });

            rules = ScriptableObject.CreateInstance<GearUpgradeTable>();
            rules.optionPerLevel = 10f;
            rules.weaponPowerPerLevel = 3f;
            rules.luckyBonus = 100f;
            rules.removeGemGold = 200;

            Swap(typeof(ItemDatabase), items);
            Swap(typeof(GearUpgradeTable), rules);
            me = new PlayerProfile();
            me.SetGold(1000);
            scope = CoreSystems.AsServer(me);
        }

        [TearDown]
        public void TearDown()
        {
            scope.Dispose();
            for (int i = previous.Count - 1; i >= 0; i--) previous[i].Key.SetValue(null, previous[i].Value);
            previous.Clear();
            Object.DestroyImmediate(items);
            Object.DestroyImmediate(rules);
        }

        // X.Use() cảnh báo khi thay bảng: test gán thẳng trường tĩnh `current`, như QuestWorld
        void Swap(System.Type table, object value)
        {
            FieldInfo f = table.GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
            previous.Add(new KeyValuePair<FieldInfo, object>(f, f.GetValue(null)));
            f.SetValue(null, value);
        }

        void Steps(params (float chance, bool drop)[] steps)
        {
            rules.steps.Clear();
            foreach (var s in steps) rules.steps.Add(new GearUpgradeStep { chance = s.chance, stones = 2, gold = 100, dropOnFail = s.drop });
        }

        void Sockets(params float[] chances)
        {
            rules.sockets.Clear();
            foreach (float c in chances) rules.sockets.Add(new GearSocketStep { chance = c, stones = 2, gold = 100 });
        }

        int AddHelmet(int level = 0)
        {
            me.Inventory.AddGear(new ItemStack(Helmet, 1, level));
            for (int i = 0; i < me.Inventory.Capacity; i++)
                if (me.Inventory[i].itemId == Helmet && me.Inventory[i].level == level) return i;
            return -1;
        }

        // ---------------- chỉ số theo option ----------------

        [Test]
        public void PotionRestoresFromOption()
        {
            ItemTemplate potion = ItemDatabase.Current.Get(Potion);
            Assert.AreEqual(10, potion.RestoreHp);
            Assert.AreEqual(0, potion.RestoreMp);
            Assert.AreEqual(0f, potion.OptionValue(PlayerStat.RestoreMp));
        }

        [Test]
        public void RestoreOptionsStayOffCharacterStats()
        {
            var stats = new PlayerStats();
            stats.Add(new PlayerStatEffect(PlayerStat.RestoreHp, 100f), 1);
            Assert.AreEqual(0, stats.maxHp);
        }

        [Test]
        public void BasicAttackReadsWeaponItemOptions()
        {
            var weapons = ScriptableObject.CreateInstance<WeaponDatabase>();
            weapons.weapons.Add(new WeaponTemplate { id = 1, name = "sword", basicSkill = 6 });
            Swap(typeof(WeaponDatabase), weapons);
            items.Get(Sword).options = new List<PlayerStatEffect>
            {
                new PlayerStatEffect(PlayerStat.AttackDamage, 100f),
                new PlayerStatEffect(PlayerStat.AttackCooldown, 0.25f),
                new PlayerStatEffect(PlayerStat.AttackRange, 2f),
            };
            var skill = new SkillTemplate { id = 6, kind = SkillKind.Basic };
            skill.levels.Add(new SkillInfo { damage = 0, cooldown = 0f, range = 0f, radius = 0.4f });
            skill.levels.Add(new SkillInfo { damage = 10, cooldown = 0f, range = 0.5f, radius = 0.4f });

            SkillInfo level2 = skill.Stats(2);
            Assert.AreEqual(110, level2.damage);
            Assert.AreEqual(0.25f, level2.cooldown, 0.001f);
            Assert.AreEqual(2.5f, level2.range, 0.001f);
            Assert.AreEqual(0.4f, level2.radius, 0.001f);
            Assert.AreEqual(0, level2.maxTarget);
            Assert.AreEqual(10, skill.GetLevel(2).damage);   // bảng skill không bị ghi đè

            var active = new SkillTemplate { id = 6, kind = SkillKind.Active };
            active.levels.Add(new SkillInfo { damage = 130 });
            Assert.AreEqual(130, active.Stats(1).damage);
            Object.DestroyImmediate(weapons);
        }

        [Test]
        public void WeaponBaseOptionsStayOffEnhancement()
        {
            items.Get(Sword).options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.AttackDamage, 100f) };
            var effects = new List<PlayerStatEffect>();
            GearUpgradeTable.EffectsOf(items.Get(Sword), 2, effects);
            Assert.AreEqual(1, effects.Count);
            Assert.AreEqual(PlayerStat.WeaponPower, effects[0].stat);
            Assert.AreEqual(6f, effects[0].value, 0.001f);
        }

        [Test]
        public void WeaponStatsReadUpgradeOptions()
        {
            var stats = new WeaponStats();
            stats.Add(new PlayerStatEffect(PlayerStat.WeaponDamage, 8f), 2);
            stats.Add(new PlayerStatEffect(PlayerStat.AttackRange, 0.4f), 1);
            stats.Add(new PlayerStatEffect(PlayerStat.AttackSpeed, 40f), 2);
            stats.Add(new PlayerStatEffect(PlayerStat.MaxHp, 100f), 1);
            Assert.AreEqual(16, stats.damage);
            Assert.AreEqual(0.4f, stats.range, 0.001f);
            Assert.AreEqual(WeaponStats.MaxCooldownCut, stats.attackSpeed, 0.001f);
        }

        // ---------------- cấp đi theo món ----------------

        [Test]
        public void SortKeepsLevels()
        {
            me.Inventory.Add(Potion, 5);
            me.Inventory.AddGear(new ItemStack(Helmet, 1, 3));
            me.Inventory.Add(Helmet);
            me.Inventory.AddGear(new ItemStack(Helmet, 1, 7));

            me.Inventory.Sort();

            var levels = new List<int>();
            foreach (ItemStack s in me.Inventory.Slots) if (s.itemId == Helmet) levels.Add(s.level);
            CollectionAssert.AreEqual(new[] { 7, 3, 0 }, levels);
            Assert.AreEqual(3, me.Inventory.Count(Helmet));
            Assert.AreEqual(5, me.Inventory.Count(Potion));
        }

        [Test]
        public void RemoveTakesPlainItemFirst()
        {
            me.Inventory.Add(Helmet);
            me.Inventory.AddGear(new ItemStack(Helmet, 1, 5));

            Assert.IsTrue(me.Inventory.Remove(Helmet));

            Assert.AreEqual(1, me.Inventory.Count(Helmet));
            foreach (ItemStack s in me.Inventory.Slots) if (s.itemId == Helmet) Assert.AreEqual(5, s.level);
        }

        [Test]
        public void EquipAndUnequipCarryLevel()
        {
            int slot = AddHelmet(4);
            Assert.IsTrue(me.Equip(slot));
            Assert.AreEqual(4, me.Equipment.Level(EquipSlot.Head));
            Assert.IsTrue(me.Inventory[slot].IsEmpty);

            Assert.IsTrue(me.Unequip(EquipSlot.Head));
            Assert.AreEqual(0, me.Equipment.Level(EquipSlot.Head));
            Assert.AreEqual(4, me.Inventory[0].level);
        }

        [Test]
        public void SwappingGearKeepsBothLevels()
        {
            int first = AddHelmet(2);
            me.Equip(first);
            int second = AddHelmet(6);

            Assert.IsTrue(me.Equip(second));

            Assert.AreEqual(6, me.Equipment.Level(EquipSlot.Head));
            Assert.AreEqual(2, me.Inventory[second].level);
        }

        [Test]
        public void ProfileSyncCarriesLevels()
        {
            me.Equip(AddHelmet(3));
            me.Inventory.AddGear(new ItemStack(Helmet, 1, 5));
            byte[] block = ProfileSync.Write(me, ProfileSync.Bag);

            var client = new PlayerProfile();
            Assert.IsTrue(ProfileSync.Apply(client, block, out _, out string error), error);

            Assert.AreEqual(3, client.Equipment.Level(EquipSlot.Head));
            Assert.AreEqual(5, client.Inventory[0].level);

            // cùng món, chỉ đổi cấp: bản đồng bộ vẫn phải áp
            me.Equipment.Set(EquipSlot.Head, new ItemStack(Helmet, 1, 4));
            Assert.IsTrue(ProfileSync.Apply(client, ProfileSync.Write(me, ProfileSync.Bag), out _, out error), error);
            Assert.AreEqual(4, client.Equipment.Level(EquipSlot.Head));
        }

        [Test]
        public void SaveAndLoadKeepLevels()
        {
            me.Equip(AddHelmet(3));
            me.Inventory.AddGear(new ItemStack(Helmet, 1, 5));
            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            try
            {
                data.SaveEquipment(me.Equipment);
                data.SaveInventory(me.Inventory);
                PlayerData loaded = Object.Instantiate(data);
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), loaded);

                var fresh = new PlayerProfile();
                loaded.FillInventory(fresh.Inventory);
                fresh.LoadEquipment(loaded.equipped, removeFromBag: false, gear: loaded.equipGear);
                Object.DestroyImmediate(loaded);

                Assert.AreEqual(3, fresh.Equipment.Level(EquipSlot.Head));
                Assert.AreEqual(5, fresh.Inventory[0].level);
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        // ---------------- luật ----------------

        [Test]
        public void SuccessRaisesLevelAndPays()
        {
            Steps((100f, false));
            int slot = AddHelmet();
            me.Inventory.Add(Upgrade, 3);
            GearOutcome heard = GearOutcome.Refused;
            me.OnGearUpgraded += (o, _) => heard = o;

            Assert.AreEqual(GearOutcome.Success, me.UpgradeGear(slot, false, false));

            Assert.AreEqual(1, me.Inventory[slot].level);
            Assert.AreEqual(1, me.Inventory.Count(Upgrade));
            Assert.AreEqual(900, me.Gold);
            Assert.AreEqual(GearOutcome.Success, heard);
        }

        [Test]
        public void FailureKeepsLevelButStillPays()
        {
            Steps((100f, false), (0f, false));
            int slot = AddHelmet(1);
            me.Inventory.Add(Upgrade, 2);

            Assert.AreEqual(GearOutcome.Failed, me.UpgradeGear(slot, false, false));

            Assert.AreEqual(1, me.Inventory[slot].level);
            Assert.AreEqual(0, me.Inventory.Count(Upgrade));
            Assert.AreEqual(900, me.Gold);
        }

        [Test]
        public void FailureDropsLevelWithoutProtect()
        {
            Steps((100f, false), (0f, true));
            int slot = AddHelmet(1);
            me.Inventory.Add(Upgrade, 2);

            Assert.AreEqual(GearOutcome.Dropped, me.UpgradeGear(slot, false, false));

            Assert.AreEqual(0, me.Inventory[slot].level);
        }

        [Test]
        public void ProtectStoneIsSpentOnlyWhenItSavesTheLevel()
        {
            Steps((100f, false), (0f, true), (100f, true));
            int slot = AddHelmet(1);
            me.Inventory.Add(Upgrade, 4);
            me.Inventory.Add(Protect, 1);

            Assert.AreEqual(GearOutcome.Protected, me.UpgradeGear(slot, false, true));
            Assert.AreEqual(1, me.Inventory[slot].level);
            Assert.AreEqual(0, me.Inventory.Count(Protect));

            ItemStack stack = me.Inventory[slot];
            stack.level = 2;
            me.Inventory.SetGear(slot, stack);
            me.Inventory.Add(Protect, 1);
            Assert.AreEqual(GearOutcome.Success, me.UpgradeGear(slot, false, true));
            Assert.AreEqual(1, me.Inventory.Count(Protect));
        }

        [Test]
        public void ProtectNeedsTheStepsStoneCount()
        {
            Steps((100f, false), (0f, true));
            rules.steps[1].protectStones = 3;
            int slot = AddHelmet(1);
            me.Inventory.Add(Upgrade, 2);
            me.Inventory.Add(Protect, 2);
            Assert.AreEqual(GearBlock.NeedProtect, me.CheckGearUpgrade(slot, false, true));

            me.Inventory.Add(Protect, 2);
            Assert.AreEqual(GearOutcome.Protected, me.UpgradeGear(slot, false, true));

            Assert.AreEqual(1, me.Inventory.Count(Protect));
            Assert.AreEqual(1, me.Inventory[slot].level);
        }

        [Test]
        public void LuckyStoneRaisesChanceAndIsSpent()
        {
            Steps((0f, false));
            int slot = AddHelmet();
            me.Inventory.Add(Upgrade, 2);
            me.Inventory.Add(Lucky, 2);

            Assert.AreEqual(GearOutcome.Success, me.UpgradeGear(slot, true, false));

            Assert.AreEqual(1, me.Inventory.Count(Lucky));
        }

        [Test]
        public void RefusedCostsNothing()
        {
            Steps((100f, false));
            int slot = AddHelmet();

            Assert.AreEqual(GearBlock.NeedStone, me.CheckGearUpgrade(slot, false, false));
            Assert.AreEqual(GearOutcome.Refused, me.UpgradeGear(slot, false, false));

            me.Inventory.Add(Upgrade, 2);
            Assert.AreEqual(GearBlock.NeedLucky, me.CheckGearUpgrade(slot, true, false));
            me.SetGold(50);
            Assert.AreEqual(GearBlock.NeedGold, me.CheckGearUpgrade(slot, false, false));
            Assert.AreEqual(GearOutcome.Refused, me.UpgradeGear(slot, false, false));

            Assert.AreEqual(2, me.Inventory.Count(Upgrade));
            Assert.AreEqual(50, me.Gold);
            Assert.AreEqual(0, me.Inventory[slot].level);
        }

        [Test]
        public void CannotPassMaxLevelOrUpgradeNonGear()
        {
            Steps((100f, false));
            me.Inventory.Add(Upgrade, 9);
            Assert.AreEqual(GearBlock.Maxed, me.CheckGearUpgrade(AddHelmet(1), false, false));

            me.Inventory.Add(Skin);
            int skin = -1;
            for (int i = 0; i < me.Inventory.Capacity; i++) if (me.Inventory[i].itemId == Skin) skin = i;
            Assert.AreEqual(GearBlock.NotGear, me.CheckGearUpgrade(skin, false, false));
            Assert.AreEqual(GearBlock.NoItem, me.CheckGearUpgrade(40, false, false));
        }

        [Test]
        public void EquippedGearUpgradesInPlaceAndRaisesStats()
        {
            Steps((100f, false), (100f, false));
            me.Equip(AddHelmet());
            me.Inventory.Add(Upgrade, 4);
            Assert.AreEqual(100, me.GearBonus.maxHp);

            int spot = PlayerProfile.GearSpot(EquipSlot.Head);
            Assert.AreEqual(GearOutcome.Success, me.UpgradeGear(spot, false, false));
            Assert.AreEqual(GearOutcome.Success, me.UpgradeGear(spot, false, false));

            Assert.AreEqual(2, me.Equipment.Level(EquipSlot.Head));
            Assert.AreEqual(120, me.GearBonus.maxHp);   // +10% mỗi cấp trên option gốc
        }

        [Test]
        public void WeaponLevelAddsWeaponPower()
        {
            Steps((100f, false));
            me.Inventory.Add(Sword);
            me.Equip(0);
            me.Inventory.Add(Upgrade, 2);

            me.UpgradeGear(PlayerProfile.GearSpot(EquipSlot.Weapon), false, false);

            Assert.AreEqual(3f, me.GearBonus.weaponPower, 0.001f);
        }

        // ---------------- khoét lỗ, khảm đá ----------------

        static ItemStack Socketed(int gem0 = 0, int gem1 = 0) => new ItemStack(Helmet, 1) { sockets = 2, gem0 = gem0, gem1 = gem1 };

        [Test]
        public void DrillOpensSocketAndPays()
        {
            Sockets(100f, 0f);
            int slot = AddHelmet();
            me.Inventory.Add(Drill, 3);
            GearOutcome heard = GearOutcome.Refused;
            me.OnSocketOpened += (o, _) => heard = o;

            Assert.AreEqual(GearOutcome.Success, me.OpenSocket(slot));

            Assert.AreEqual(1, me.Inventory[slot].sockets);
            Assert.AreEqual(1, me.Inventory.Count(Drill));
            Assert.AreEqual(900, me.Gold);
            Assert.AreEqual(GearOutcome.Success, heard);
        }

        [Test]
        public void FailedDrillLosesStonesButKeepsItem()
        {
            Sockets(0f);
            int slot = AddHelmet(3);
            me.Inventory.Add(Drill, 2);

            Assert.AreEqual(GearOutcome.Failed, me.OpenSocket(slot));

            Assert.AreEqual(0, me.Inventory[slot].sockets);
            Assert.AreEqual(3, me.Inventory[slot].level);
            Assert.AreEqual(0, me.Inventory.Count(Drill));
        }

        [Test]
        public void CannotDrillPastTableOrWithoutStones()
        {
            Sockets(100f);
            int slot = AddHelmet();
            Assert.AreEqual(GearBlock.NeedStone, me.CheckOpenSocket(slot));

            me.Inventory.Add(Drill, 2);
            me.OpenSocket(slot);
            Assert.AreEqual(GearBlock.Maxed, me.CheckOpenSocket(slot));
        }

        [Test]
        public void SocketsNeverExceedStructLimit()
        {
            Sockets(100f, 100f, 100f, 100f, 100f);
            Assert.AreEqual(ItemStack.MaxSockets, rules.MaxSockets);
        }

        [Test]
        public void InlayMovesGemIntoSocket()
        {
            me.Inventory.AddGear(Socketed());
            me.Inventory.Add(Ruby, 2);

            Assert.IsTrue(me.InlayGem(0, 1, Ruby));

            Assert.AreEqual(Ruby, me.Inventory[0].Gem(1));
            Assert.AreEqual(1, me.Inventory.Count(Ruby));
        }

        [Test]
        public void InlayRefusesBadTargets()
        {
            me.Inventory.AddGear(Socketed(Ruby));
            me.Inventory.Add(Jade, 1);
            me.Inventory.Add(Potion, 1);

            Assert.AreEqual(GearBlock.SocketFull, me.CheckInlay(0, 0, Jade));
            Assert.AreEqual(GearBlock.NoSocket, me.CheckInlay(0, 2, Jade));
            Assert.AreEqual(GearBlock.NotJewel, me.CheckInlay(0, 1, Potion));
            Assert.AreEqual(GearBlock.NeedJewel, me.CheckInlay(0, 1, Ruby));
            Assert.IsFalse(me.InlayGem(0, 0, Jade));
            Assert.AreEqual(1, me.Inventory.Count(Jade));
        }

        [Test]
        public void OneGemOfEachKindPerItem()
        {
            items.items.Add(new ItemTemplate
            {
                id = 11, name = "ruby2", kind = ItemKind.Jewel, value = 2, maxStack = 99,
                options = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.MaxHp, 90f) },
            });
            items.ClearCache();
            me.Inventory.AddGear(Socketed(Ruby));
            me.Inventory.Add(11);
            me.Inventory.Add(Jade);

            Assert.AreEqual(GearBlock.SameJewel, me.CheckInlay(0, 1, 11));   // khác bậc, cùng chỉ số
            Assert.IsFalse(me.InlayGem(0, 1, 11));
            Assert.AreEqual(1, me.Inventory.Count(11));
            Assert.IsTrue(me.InlayGem(0, 1, Jade));

            Assert.IsTrue(me.RemoveGem(0, 0));
            ItemStack gear = me.Inventory[0];
            gear.gem1 = 0;
            me.Inventory.SetGear(0, gear);
            Assert.AreEqual(GearBlock.None, me.CheckInlay(0, 0, 11));        // gỡ ra rồi thì khảm được bậc khác
        }

        [Test]
        public void RemoveGemReturnsStoneForGold()
        {
            rules.removeGemGold = 300;
            me.Inventory.AddGear(Socketed(Ruby));

            Assert.IsTrue(me.RemoveGem(0, 0));

            Assert.AreEqual(0, me.Inventory[0].Gem(0));
            Assert.AreEqual(1, me.Inventory.Count(Ruby));
            Assert.AreEqual(700, me.Gold);
            Assert.AreEqual(2, me.Inventory[0].sockets);   // lỗ vẫn còn
        }

        [Test]
        public void RemoveGemRefusedWithoutGoldOrRoom()
        {
            me.Inventory.AddGear(Socketed(Ruby));
            me.SetGold(50);
            Assert.AreEqual(GearBlock.NeedGold, me.CheckRemoveGem(0, 0));

            me.SetGold(1000);
            for (int i = 1; i < me.Inventory.Capacity; i++) me.Inventory.Add(Sword);
            Assert.AreEqual(GearBlock.BagFull, me.CheckRemoveGem(0, 0));
            Assert.IsFalse(me.RemoveGem(0, 0));
            Assert.AreEqual(1000, me.Gold);
            Assert.AreEqual(Ruby, me.Inventory[0].Gem(0));
        }

        [Test]
        public void RemoveGemFailsWhenStackIsFullAndBagIsFull()
        {
            me.Inventory.AddGear(Socketed(Ruby));
            me.Inventory.Add(Ruby, 2);   // maxStack 2: chồng đã đầy
            for (int i = 2; i < me.Inventory.Capacity; i++) me.Inventory.Add(Sword);

            Assert.IsFalse(me.RemoveGem(0, 0));

            Assert.AreEqual(1000, me.Gold);
            Assert.AreEqual(Ruby, me.Inventory[0].Gem(0));
            Assert.AreEqual(2, me.Inventory.Count(Ruby));
        }

        [Test]
        public void GemStatsCountOnlyWhenWornAndIgnoreLevel()
        {
            me.Inventory.AddGear(Socketed(Ruby, Jade));
            Assert.AreEqual(0, me.GearBonus.maxHp);

            me.Equip(0);
            Assert.AreEqual(150, me.GearBonus.maxHp);          // 100 của nón + 50 của đá
            Assert.AreEqual(4f, me.GearBonus.crit, 0.001f);

            ItemStack worn = me.Equipment.Stack(EquipSlot.Head);
            worn.level = 5;
            me.Equipment.Set(EquipSlot.Head, worn);
            Assert.AreEqual(200, me.GearBonus.maxHp);          // +10% mỗi cấp chỉ nhân option của nón
        }

        [Test]
        public void GemsFollowTheItem()
        {
            me.Inventory.AddGear(Socketed(Ruby, Jade));
            me.Inventory.Add(Potion);
            me.Inventory.Sort();
            int slot = -1;
            for (int i = 0; i < me.Inventory.Capacity; i++) if (me.Inventory[i].itemId == Helmet) slot = i;
            Assert.AreEqual(Jade, me.Inventory[slot].Gem(1));

            Assert.IsTrue(me.Equip(slot));
            Assert.AreEqual(Ruby, me.Equipment.Stack(EquipSlot.Head).Gem(0));

            Assert.IsTrue(me.Unequip(EquipSlot.Head));
            ItemStack back = default;
            foreach (ItemStack s in me.Inventory.Slots) if (s.itemId == Helmet) back = s;
            Assert.AreEqual(2, back.sockets);
            Assert.AreEqual(Jade, back.Gem(1));
        }

        [Test]
        public void ProfileSyncAndSaveCarrySockets()
        {
            me.Inventory.AddGear(Socketed(Ruby));
            me.Equip(0);
            me.Inventory.AddGear(Socketed(0, Jade));
            byte[] block = ProfileSync.Write(me, ProfileSync.Bag);

            var client = new PlayerProfile();
            Assert.IsTrue(ProfileSync.Apply(client, block, out _, out string error), error);
            Assert.AreEqual(Ruby, client.Equipment.Stack(EquipSlot.Head).Gem(0));
            Assert.AreEqual(2, client.Inventory[0].sockets);
            Assert.AreEqual(Jade, client.Inventory[0].Gem(1));

            PlayerData data = ScriptableObject.CreateInstance<PlayerData>();
            try
            {
                data.SaveEquipment(me.Equipment);
                data.SaveInventory(me.Inventory);
                var fresh = new PlayerProfile();
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), data);
                data.FillInventory(fresh.Inventory);
                fresh.LoadEquipment(data.equipped, removeFromBag: false, gear: data.equipGear);
                Assert.AreEqual(Ruby, fresh.Equipment.Stack(EquipSlot.Head).Gem(0));
                Assert.AreEqual(Jade, fresh.Inventory[0].Gem(1));
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void GearTable_FileRoundTrip()
        {
            rules.steps.Add(new GearUpgradeStep { chance = 37.5f, stones = 4, gold = 123456789012L, dropOnFail = true, protectStones = 8 });
            rules.sockets.Add(new GearSocketStep { chance = 12.5f, stones = 3, gold = 4000 });
            rules.removeGemGold = 777;

            var back = ScriptableObject.CreateInstance<GearUpgradeTable>();
            try
            {
                Assert.IsTrue(GearUpgradeTable.Read(rules.ToBytes(), back, out string error), error);
                Assert.AreEqual(rules.steps.Count, back.steps.Count);
                Assert.AreEqual(rules.sockets.Count, back.sockets.Count);
                Assert.AreEqual(777, back.removeGemGold);
                Assert.AreEqual(rules.optionPerLevel, back.optionPerLevel, 0.01f);
                GearUpgradeStep last = back.steps[back.steps.Count - 1];
                Assert.AreEqual(37.5f, last.chance, 0.01f);
                Assert.AreEqual(123456789012L, last.gold);
                Assert.IsTrue(last.dropOnFail);
                Assert.AreEqual(8, last.protectStones);
                Assert.AreEqual(4000, back.sockets[back.sockets.Count - 1].gold);

                byte[] newer = rules.ToBytes();
                newer[1] = GearUpgradeTable.CurrentSchema + 1;
                Assert.IsFalse(GearUpgradeTable.Read(newer, back, out error));
            }
            finally
            {
                Object.DestroyImmediate(back);
            }
        }

        [Test]
        public void UpgradeTree_FileRoundTrip()
        {
            var tree = ScriptableObject.CreateInstance<PlayerUpgradeTree>();
            var back = ScriptableObject.CreateInstance<PlayerUpgradeTree>();
            try
            {
                tree.expGrowth = 1.35f;
                tree.nodes.Add(new PlayerUpgradeNode
                {
                    id = 12, name = "vitality", parent = 3, maxRank = 5, cost = 2, requiredLevel = 10, branch = 2,
                    effects = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.HpRegen, 0.5f) },
                });
                tree.masteries.Add(new BranchMastery
                {
                    branch = 1, points = 20,
                    effects = new List<PlayerStatEffect> { new PlayerStatEffect(PlayerStat.Crit, 2f) },
                });

                Assert.IsTrue(PlayerUpgradeTree.Read(tree.ToBytes(), back, out string error), error);
                Assert.AreEqual(tree.ExpToNext(20), back.ExpToNext(20));
                PlayerUpgradeNode node = back.Get(12);
                Assert.AreEqual("vitality", node.name);
                Assert.AreEqual(3, node.parent);
                Assert.AreEqual(2, node.branch);
                Assert.AreEqual(PlayerStat.HpRegen, node.effects[0].stat);
                Assert.AreEqual(0.5f, node.effects[0].value, 0.01f);
                Assert.AreEqual(20, back.masteries[0].points);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(back);
            }
        }
    }
}
