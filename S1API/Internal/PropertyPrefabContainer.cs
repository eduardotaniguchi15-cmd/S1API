using UnityEngine;

namespace S1API.Internal
{
    /// <summary>
    /// Manages Property prefab containers to keep the scene hierarchy organized and persistent
    /// across scene loads. Prefabs are parented under a dedicated root that is marked as
    /// DontDestroyOnLoad so host and clients share the same configured prefabs before the
    /// gameplay scene initializes.
    /// </summary>
    internal static class PropertyPrefabContainer
    {
        private const string RootName = "@S1API_PersistentPrefabs";
        private static GameObject _persistentRoot;

        /// <summary>
        /// Gets or creates the persistent prefab root that survives scene loads.
        /// </summary>
        public static GameObject GetOrCreatePrefabsContainer()
        {
            if (_persistentRoot != null)
                return _persistentRoot;

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                _persistentRoot = existing;
            }
            else
            {
                _persistentRoot = new GameObject(RootName);
            }

            Object.DontDestroyOnLoad(_persistentRoot);
            return _persistentRoot;
        }

        /// <summary>
        /// Gets or creates a specific Property prefab container.
        /// </summary>
        public static GameObject GetOrCreatePropertyPrefabContainer(string propertyTypeName)
        {
            if (string.IsNullOrEmpty(propertyTypeName))
                return null;

            var prefabsRoot = GetOrCreatePrefabsContainer();
            if (prefabsRoot == null)
                return null;

            var propertyContainer = prefabsRoot.transform.Find(propertyTypeName);
            if (propertyContainer == null)
            {
                var containerGO = new GameObject(propertyTypeName);
                containerGO.transform.SetParent(prefabsRoot.transform, false);
                return containerGO;
            }

            return propertyContainer.gameObject;
        }

        /// <summary>
        /// Places a prefab GameObject in the appropriate Property prefab container.
        /// </summary>
        public static GameObject OrganizePrefab(GameObject prefab, string propertyTypeName)
        {
            if (prefab == null || string.IsNullOrEmpty(propertyTypeName))
                return null;

            var container = GetOrCreatePropertyPrefabContainer(propertyTypeName);
            if (container != null)
            {
                prefab.transform.SetParent(container.transform, false);
                prefab.SetActive(false); // Keep prefabs inactive
            }

            return container;
        }
    }
}
