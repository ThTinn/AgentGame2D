using System.Collections;
using DungeonOnline.Entities;
using DungeonOnline.Net;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DungeonOnline.Tests
{
    public class MainSceneTests
    {
        private const float SettleSeconds = 0.5f;
        private const float InputSeconds = 0.2f;

        private Keyboard _keyboard;
        private Rigidbody2D _player;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInputBehavior;
        private InputSettings.BackgroundBehavior _backgroundBehavior;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The batch-mode editor has no focused Game view, so keyboard events are dropped by default.
            _editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            _keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;   // GameFlow.Start builds the panels and freezes the player
            MapInfo town = TownMap();
            Object.FindFirstObjectByType<GameFlow>().PlayOffline(town, town.spawn.ToVector());
            FindSceneObjects();
            yield return new WaitForSeconds(SettleSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInputBehavior;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
        }

        [Test]
        public void Start_PlayerRestsOnGround()
        {
            // Assert
            Assert.That(_player.linearVelocity.y, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_player.position.y, Is.EqualTo(-3.5f).Within(0.05f));
        }

        [UnityTest]
        public IEnumerator HoldRight_MovesPlayerRight()
        {
            // Arrange
            var startX = _player.position.x;

            // Act
            yield return Press(Key.D);

            // Assert
            Assert.Greater(_player.position.x, startX);
        }

        [UnityTest]
        public IEnumerator PressJump_LiftsPlayer()
        {
            // Arrange
            var startY = _player.position.y;

            // Act
            yield return Press(Key.Space);

            // Assert
            Assert.Greater(_player.position.y, startY);
        }

        [UnityTest]
        public IEnumerator FallBelowLimit_ReturnsToStart()
        {
            // Arrange
            var start = _player.position;

            // Act
            _player.position = new Vector2(0.5f, -20f);
            yield return new WaitForSeconds(SettleSeconds);

            // Assert
            Assert.That(_player.position.x, Is.EqualTo(start.x).Within(0.01f));
            Assert.That(_player.position.y, Is.EqualTo(start.y).Within(0.05f));
        }

        private void FindSceneObjects()
        {
            _player = Object.FindFirstObjectByType<Player>().GetComponent<Rigidbody2D>();
        }

        /// <summary>Fixture: the town map's ground and spawn as in ServerJava/data/templates/maps.json.</summary>
        private static MapInfo TownMap()
        {
            return new MapInfo
            {
                id = 0,
                name = "Town",
                xMin = -8.6f,
                xMax = 8.6f,
                fallLimitY = -8f,
                spawn = new Point { x = -7f, y = -3.5f },
                solids = new[]
                {
                    new Solid { name = "Ground Left", x = -5f, y = -4.5f, w = 8f, h = 1f, visible = true },
                    new Solid { name = "Ground Right", x = 5.5f, y = -4.5f, w = 7f, h = 1f, visible = true },
                    new Solid { name = "Wall Left", x = -9.4f, y = 2f, w = 1f, h = 24f, visible = false },
                    new Solid { name = "Wall Right", x = 9.4f, y = 2f, w = 1f, h = 24f, visible = false },
                },
            };
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return new WaitForSeconds(InputSeconds);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }
    }
}
