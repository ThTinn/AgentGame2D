using System.Collections.Generic;
using UnityEngine;

namespace DungeonOnline.Net
{
    /// <summary>
    /// Builds the current 2D map in the scene from a <see cref="MapInfo"/>: one GameObject per solid, with a BoxCollider2D
    /// and, when visible, a tinted sprite. Building another map removes the previous one.
    /// </summary>
    /// <example>
    /// <code>
    /// mapView.Build(maps.Find(mapId));
    /// </code>
    /// </example>
    public sealed class MapView : MonoBehaviour
    {
        [Tooltip("1x1 unit sprite stretched to each solid's size.")]
        [SerializeField] private Sprite _solidSprite;
        [SerializeField] private Color _solidColor = new Color(0.27f, 0.65f, 0.42f, 1f);
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _sortingOrder;

        private readonly List<GameObject> _built = new List<GameObject>();

        /// <summary>The map built last, or null.</summary>
        public MapInfo Current { get; private set; }

        /// <summary>Replaces whatever map is built with <paramref name="map"/>. Null just clears.</summary>
        public void Build(MapInfo map)
        {
            Clear();
            Current = map;
            if (map == null) return;
            foreach (Solid solid in map.solids)
                if (solid != null) _built.Add(BuildSolid(solid));
        }

        /// <summary>Removes the built map.</summary>
        public void Clear()
        {
            foreach (GameObject go in _built)
                if (go != null) Destroy(go);
            _built.Clear();
            Current = null;
        }

        private GameObject BuildSolid(Solid solid)
        {
            var go = new GameObject(string.IsNullOrEmpty(solid.name) ? "Solid" : solid.name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(solid.x, solid.y, 0f);
            // same layout as the hand-placed level: unit sprite and collider, scaled to the size
            go.transform.localScale = new Vector3(solid.w, solid.h, 1f);
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            if (solid.visible && _solidSprite != null)
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _solidSprite;
                renderer.color = _solidColor;
                renderer.sortingLayerName = _sortingLayerName;
                renderer.sortingOrder = _sortingOrder;
            }
            return go;
        }
    }
}
