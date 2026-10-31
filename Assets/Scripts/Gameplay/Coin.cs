using System;
using UnityEngine;

namespace CoinHop.Gameplay
{
    /// <summary>A pickup that disappears when the player touches it.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Coin : MonoBehaviour
    {
        /// <summary>Raised when the player collects this coin.</summary>
        public event Action OnCollected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            gameObject.SetActive(false);
            OnCollected?.Invoke();
        }
    }
}
