using CoinHop.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace CoinHop.UI
{
    /// <summary>Shows coin progress and the win message. Display only.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private GameSession _session;

        private Label _coinCount;
        private Label _winMessage;

        private void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _coinCount = root.Q<Label>("coin-count");
            _winMessage = root.Q<Label>("win-message");
            _winMessage.style.display = DisplayStyle.None;

            var counter = _session.Counter;
            counter.OnChanged += ShowCoins;
            counter.OnCompleted += ShowWin;
            ShowCoins(counter.Collected, counter.Total);
        }

        private void OnDestroy()
        {
            if (_session == null || _session.Counter == null) return;

            _session.Counter.OnChanged -= ShowCoins;
            _session.Counter.OnCompleted -= ShowWin;
        }

        private void ShowCoins(int collected, int total)
        {
            _coinCount.text = $"{collected} / {total}";
        }

        private void ShowWin()
        {
            _winMessage.style.display = DisplayStyle.Flex;
        }
    }
}
