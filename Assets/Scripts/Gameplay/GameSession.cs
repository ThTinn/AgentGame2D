using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CoinHop.Gameplay
{
    /// <summary>
    /// Connects the coins to the counter, ends the level on a win and restarts it.
    /// Implements the goal in design/game-brief.md.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private Coin[] _coins;
        [SerializeField] private InputActionReference _restart;

        /// <summary>Coin progress for this level. Available from Awake.</summary>
        public CoinCounter Counter { get; private set; }

        private void Awake()
        {
            Counter = new CoinCounter(_coins.Length);
            Counter.OnCompleted += _player.Stop;
            foreach (var coin in _coins) coin.OnCollected += Counter.Collect;
        }

        private void OnEnable()
        {
            _restart.action.Enable();
        }

        private void Update()
        {
            if (Counter.IsComplete && _restart.action.WasPressedThisFrame())
            {
                SceneManager.LoadScene(gameObject.scene.buildIndex);
            }
        }
    }
}
