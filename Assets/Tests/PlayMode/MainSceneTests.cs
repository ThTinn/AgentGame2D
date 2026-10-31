using System.Collections;
using CoinHop.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CoinHop.Tests
{
    public class MainSceneTests
    {
        private const float SettleSeconds = 0.5f;
        private const float InputSeconds = 0.2f;

        private Keyboard _keyboard;
        private GameSession _session;
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

        [Test]
        public void Hud_IsAttachedToPanel()
        {
            // Act
            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;

            // Assert
            Assert.IsNotNull(root.panel);
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

        [UnityTest]
        public IEnumerator CollectEveryCoin_ShowsWinThenJumpRestarts()
        {
            // Act
            foreach (var coin in Object.FindObjectsByType<Coin>(FindObjectsSortMode.None))
            {
                _player.position = coin.transform.position;
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            yield return null;

            // Assert
            var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
            Assert.IsTrue(_session.Counter.IsComplete);
            Assert.AreEqual("5 / 5", root.Q<Label>("coin-count").text);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<Label>("win-message").resolvedStyle.display);

            // Act
            yield return Press(Key.Space);
            yield return null;
            FindSceneObjects();

            // Assert
            Assert.AreEqual(0, _session.Counter.Collected);
        }

        private void FindSceneObjects()
        {
            _session = Object.FindFirstObjectByType<GameSession>();
            _player = Object.FindFirstObjectByType<PlayerController>().GetComponent<Rigidbody2D>();
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
