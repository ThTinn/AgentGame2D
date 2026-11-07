using Game;
using Game.Enemies;
using Game.Items;
using Game.Npcs;
using Game.Players;
using Game.Skills;
using Game.Story;
using Game.UI;
using UnityEngine;

namespace DungeonOnline.UI
{
    /// <summary>
    /// Starts the Core UI copied from GameB (inventory, skills, upgrade tree, quests, settings) in this scene:
    /// registers the Core tables and builds the local player's profile. The GameCanvas prefab is placed in the
    /// scene itself; this adds the quick-use bar to it.
    /// </summary>
    /// <remarks>
    /// Offline only for now: the profile comes from <see cref="_playerData"/> and nothing is saved or sent.
    /// <c>NetSync.Remote</c> stays null, so NPC talk and quest accept / turn-in are refused until a server
    /// connection sets it. Replace the local profile with the server's (<c>CoreSystems.StartOnline</c>) then.
    /// </remarks>
    public sealed class CoreUiBootstrap : MonoBehaviour
    {
        [Header("Core tables (null = table not used)")]
        [SerializeField] private ItemDatabase _items;
        [SerializeField] private SkillDatabase _skills;
        [SerializeField] private WeaponDatabase _weapons;
        [SerializeField] private MonsterDatabase _monsters;
        [SerializeField] private PlayerUpgradeTree _upgradeTree;
        [SerializeField] private OptionDatabase _options;
        [SerializeField] private NpcDatabase _npcs;
        [SerializeField] private QuestDatabase _quests;
        [SerializeField] private DropDatabase _drops;
        [SerializeField] private GearUpgradeTable _gearUpgrades;

        [Header("Local player")]
        [Tooltip("Starting profile: bag, equipment, skills, level, gold.")]
        [SerializeField] private PlayerData _playerData;

        // Awake: tables and Me must exist before the panels' Start reads them
        private void Awake()
        {
            CoreSystems.Use(_items, _skills, _weapons, _monsters, _upgradeTree, _options, _npcs, _quests, _drops, _gearUpgrades);

            if (_playerData != null) CoreSystems.Begin(CoreSystems.BuildProfile(_playerData));
            else Debug.LogWarning("CoreUiBootstrap: no PlayerData assigned, panels will show no player.", this);
        }

        // Start: GameUIManager.Instance is set in its own Awake, whose order against ours is not fixed
        private void Start()
        {
            if (GameUIManager.Instance == null)
                Debug.LogWarning("CoreUiBootstrap: no GameCanvas in the scene, Core UI will not show.", this);
            QuickItemBar.Ensure();
        }

        private void OnDestroy()
        {
            CoreSystems.End();
        }
    }
}
