#if (IL2CPPMELON)
using S1Property = Il2CppScheduleOne.Property;
using S1Tiles = Il2CppScheduleOne.Tiles;
using S1Entities = Il2CppScheduleOne.EntityFramework;
using S1Container = Il2CppScheduleOne.Property.PropertyContentsContainer;
using S1ObjectScripts = Il2CppScheduleOne.ObjectScripts;
using S1Map = Il2CppScheduleOne.Map;
using S1Delivery = Il2CppScheduleOne.Delivery;
using S1Misc = Il2CppScheduleOne.Misc;
using S1Interaction = Il2CppScheduleOne.Interaction;
using Il2CppFishNet;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using S1Property = ScheduleOne.Property;
using S1Tiles = ScheduleOne.Tiles;
using S1Entities = ScheduleOne.EntityFramework;
using S1Container = ScheduleOne.Property.PropertyContentsContainer;
using S1ObjectScripts = ScheduleOne.ObjectScripts;
using S1Map = ScheduleOne.Map;
using S1Delivery = ScheduleOne.Delivery;
using S1Misc = ScheduleOne.Misc;
using S1Interaction = ScheduleOne.Interaction;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using S1API.Internal;
using S1API.Internal.Utils;
using S1API.Logging;
using S1API.Properties;
using UnityEngine;

#if (IL2CPPBEPINEX || IL2CPPMELON)
using Il2CppSystem.Collections.Generic;
#else
#endif

namespace S1API.Property
{
    /// <summary>
    /// Factory for creating custom properties by cloning base game properties.
    /// Follows the same clone-and-register pattern used by NPC spawning for FishNet v3 compatibility.
    /// </summary>
    /// <remarks>
    /// Properties must be configured in <see cref="ConfigurePrefab"/> for proper save/load behavior.
    /// All configuration is applied to the prefab before network registration to ensure
    /// stable NetworkBehaviour indices across all clients.
    /// </remarks>
    public abstract class PropertyFactory
    {
        private static readonly Log Logger = new Log("PropertyFactory");
        private static readonly Dictionary<Type, GameObject> TypeToPrefab = new Dictionary<Type, GameObject>();
        private static readonly object TemplateLoadLock = new object();
        private static volatile bool _prefabsConfiguredForLocalProcess;
        internal static bool PrefabsConfiguredForLocalProcess => _prefabsConfiguredForLocalProcess;

        private const string DEFAULT_PROPERTY_NAME = "DocksWarehouse";

        #region Template Prefab Helpers

        private static GameObject GetOrCreatePerPropertyPrefab(Type propertyType, PropertyFactory? owner)
        {
            if (propertyType == null)
                throw new Exception("Property type is null for prefab resolution.");

            if (TypeToPrefab.TryGetValue(propertyType, out var cached) && cached != null)
            {
                MarkPrefabsConfigured();
                return cached;
            }

            lock (TemplateLoadLock)
            {
                if (TypeToPrefab.TryGetValue(propertyType, out cached) && cached != null)
                {
                    MarkPrefabsConfigured();
                    return cached;
                }

                var nm = InstanceFinder.NetworkManager;
                if (nm == null)
                    throw new Exception("NetworkManager not found when resolving property prefab.");

                PrefabObjects spawnablePrefabs = nm.SpawnablePrefabs;
                if (spawnablePrefabs == null)
                    throw new Exception("SpawnablePrefabs not available on NetworkManager.");

                Logger.Msg($"[PropertyFactory] Resolving prefab for {propertyType.Name}...");
                GameObject? chosenPropertyGO = FindBasePropertyPrefab(spawnablePrefabs, DEFAULT_PROPERTY_NAME);

                if (chosenPropertyGO == null)
                {
                    Logger.Warning($"[PropertyFactory] Base prefab '{DEFAULT_PROPERTY_NAME}' not found, exploring fallbacks...");
                    chosenPropertyGO = FindAnyPropertyPrefab(spawnablePrefabs);
                }

                if (chosenPropertyGO == null)
                {
                    throw new Exception($"Failed to locate a suitable property spawnable prefab ({DEFAULT_PROPERTY_NAME} or any with Property component).");
                }

                Logger.Msg($"[PropertyFactory] Selected base template: {chosenPropertyGO.name}");
                
                // IMPORTANT: We must ensure Awake() doesn't fire prematurely on the template clone
                bool wasTemplateActive = chosenPropertyGO.activeSelf;
                chosenPropertyGO.SetActive(false);
                
                GameObject clonedPropertyGO = UnityEngine.Object.Instantiate<GameObject>(chosenPropertyGO);
                clonedPropertyGO.SetActive(false); // Keep it off
                
                // Restore template
                chosenPropertyGO.SetActive(wasTemplateActive);
                
                string prefabName = GetPrefabNameForType(propertyType);
                clonedPropertyGO.name = prefabName;
                Logger.Msg($"[PropertyFactory] Created template clone: {prefabName}");
                
                NetworkObject prefabNO = clonedPropertyGO.GetComponent<NetworkObject>();
                if (prefabNO == null)
                {
                    prefabNO = clonedPropertyGO.AddComponent<NetworkObject>();
                }

                try
                {
                    if (prefabNO != null && prefabNO.gameObject != null)
                    {
                        prefabNO.gameObject.SetActive(false);

                        var propertyComp = prefabNO.gameObject.GetComponent<S1Property.Property>();
                        if (propertyComp != null)
                        {
                            var registry = S1Property.Property.Properties;
                            if (registry != null)
                            {
                                for (int i = registry.Count - 1; i >= 0; i--)
                                {
                                    if (registry[i] == propertyComp)
                                    {
                                        registry.RemoveAt(i);
                                        break;
                                    }
                                }
                            }

                            var unownedReg = S1Property.Property.UnownedProperties;
                            if (unownedReg != null)
                            {
                                for (int i = unownedReg.Count - 1; i >= 0; i--)
                                {
                                    if (unownedReg[i] == propertyComp)
                                    {
                                        unownedReg.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                var builder = new PropertyPrefabBuilder(prefabNO.gameObject, propertyType);
                if (owner != null)
                {
                    owner.ConfigurePrefab(builder);
                }
                else
                {
                    InvokeConfigurePrefabWithoutInstance(propertyType, builder);
                }

                CleanUnnecessaryChildren(prefabNO.gameObject);

                RegisterPrefabWithFishNet(prefabNO, spawnablePrefabs, prefabName);

                OrganizePrefab(prefabNO.gameObject, propertyType.Name);

                TypeToPrefab[propertyType] = prefabNO.gameObject;
                MarkPrefabsConfigured();
                return prefabNO.gameObject;
            }
        }

        private static GameObject? FindBasePropertyPrefab(PrefabObjects spawnablePrefabs, string targetName)
        {
            if (spawnablePrefabs == null)
                return null;

            int count = spawnablePrefabs.GetObjectCount();
            
            // Step 1: Find the @Properties NetworkObject prefab
            NetworkObject? propertiesPrefab = null;
            for (int i = 0; i < count; i++)
            {
                NetworkObject obj = spawnablePrefabs.GetObject(true, i);
                if (obj != null && obj.gameObject != null)
                {
                    string name = obj.gameObject.name;
                    if (name == "@Properties" || name == "Properties")
                    {
                        propertiesPrefab = obj;
                        Logger.Msg($"[PropertyFactory] Found properties root: {name} (Index: {i})");
                        break;
                    }
                }
            }
            
            if (propertiesPrefab == null)
            {
                Logger.Warning("[PropertyFactory] Could not find @Properties prefab in SpawnablePrefabs");
                return null;
            }
            
            // Step 2: Search for targetName (DocksWarehouse) as a child of @Properties
            Transform propertiesTransform = propertiesPrefab.gameObject.transform;
            Transform? targetChild = null;
            
            // First try exact match
            targetChild = propertiesTransform.Find(targetName);
            if (targetChild != null)
            {
                Logger.Msg($"[PropertyFactory] Found target child '{targetName}' by direct Find()");
                return targetChild.gameObject;
            }
            
            Logger.Msg($"[PropertyFactory] Direct Find('{targetName}') failed, scanning all children of {propertiesPrefab.name}...");
            // Second try: search all children for DocksWarehouse specifically
            foreach (Transform child in propertiesTransform)
            {
                if (child.name == targetName || child.name == "DocksWarehouse")
                {
                    Logger.Msg($"[PropertyFactory] Found target child by name scan: {child.name}");
                    return child.gameObject;
                }
            }
            
            // Third pass: find any child with Property component as fallback
            foreach (Transform child in propertiesTransform)
            {
                var propertyComp = child.GetComponent<S1Property.Property>();
                if (propertyComp != null)
                {
                    Logger.Warning($"[PropertyFactory] Using fallback property: {child.name}");
                    return child.gameObject;
                }
            }
            
            Logger.Warning($"[PropertyFactory] Could not find suitable property prefab in @Properties children");
            return null;
        }

        private static GameObject? FindAnyPropertyPrefab(PrefabObjects spawnablePrefabs)
        {
            if (spawnablePrefabs == null)
                return null;

            // Try to find @Properties prefab first
            int count = spawnablePrefabs.GetObjectCount();
            NetworkObject? propertiesPrefab = null;
            for (int i = 0; i < count; i++)
            {
                NetworkObject obj = spawnablePrefabs.GetObject(true, i);
                if (obj != null && obj.gameObject != null)
                {
                    string name = obj.gameObject.name;
                    if (name == "@Properties" || name == "Properties")
                    {
                        propertiesPrefab = obj;
                        break;
                    }
                }
            }
            
            // If @Properties found, use first property child
            if (propertiesPrefab != null)
            {
                foreach (Transform child in propertiesPrefab.gameObject.transform)
                {
                    var propertyComp = child.GetComponent<S1Property.Property>();
                    if (propertyComp != null)
                    {
                        Logger.Msg($"[PropertyFactory] Found fallback property in @Properties: {child.name}");
                        return child.gameObject;
                    }
                }
            }
            
            // Last resort: any GameObject with Property component
            for (int i = 0; i < count; i++)
            {
                NetworkObject obj = spawnablePrefabs.GetObject(true, i);
                if (obj != null && obj.gameObject != null && obj.gameObject.GetComponent<S1Property.Property>() != null)
                {
                    Logger.Msg($"[PropertyFactory] Found fallback property prefab: {obj.gameObject.name}");
                    return obj.gameObject;
                }
            }
            return null;
        }

        private static void CleanUnnecessaryChildren(GameObject prefabRoot)
        {
            if (prefabRoot == null)
                return;

            var childrenToRemove = new List<Transform>();

            foreach (Transform child in prefabRoot.transform)
            {
                bool shouldRemove = false;
                string lowerName = child.name.ToLower();

                // Only remove items that are clearly NOT property-related
                if (child.name == "IndustrialShed")
                {
                    shouldRemove = true;
                }
                else if (child.GetComponent<NetworkObject>() != null && child.name.StartsWith("S1API_"))
                {
                    shouldRemove = false; // Keep our custom prefab
                }
                else if (child.GetComponent<S1Property.Property>() != null)
                {
                    shouldRemove = false; // Keep the property component
                }
                else if (child.GetComponent<S1Container>() != null)
                {
                    shouldRemove = false; // Keep container
                }
                else if (child.GetComponent<S1Tiles.Grid>() != null)
                {
                    shouldRemove = false; // Keep grid
                }
                else if (child.GetComponent<S1Map.POI>() != null)
                {
                    shouldRemove = false; // Keep POI
                }
                else if (child.GetComponent<S1Delivery.LoadingDock>() != null)
                {
                    shouldRemove = false; // Keep loading docks
                }
                else if (child.GetComponent<S1Property.PropertyDisposalArea>() != null)
                {
                    shouldRemove = false; // Keep disposal area
                }
                else if (child.GetComponent<S1Misc.ModularSwitch>() != null)
                {
                    shouldRemove = false; // Keep switches
                }
                else if (child.GetComponent<S1Interaction.InteractableToggleable>() != null)
                {
                    shouldRemove = false; // Keep toggleables
                }
                else if (child.GetComponent<NetworkObject>() != null)
                {
                    // Keep any other NetworkObjects (they may be important)
                    shouldRemove = false;
                }
                else if (child.name.Contains("BoundingBox") || 
                         child.name.Contains("ForSaleSign") ||
                         child.name.Contains("Listing") ||
                         child.name.Contains("Spawn") ||
                         child.name.Contains("Idle") ||
                         child.name.Contains("Container") ||
                         child.name.Contains("Foundation") ||
                         child.name.Contains("Employee") ||
                         child.name.Contains("Grid") ||
                         child.name.Contains("Interior") ||
                         child.name.Contains("PoI") ||
                         child.name.Contains("Occlusion"))
                {
                    shouldRemove = false; // Keep by name pattern
                }
                else if (child.name.Contains("Shed") ||
                         child.name.Contains("Fence") ||
                         child.name.Contains("Bin") ||
                         child.name.Contains("Dock") && !child.name.Contains("Loading"))
                {
                    // These are likely removable - be more conservative
                    shouldRemove = false; // Keep for now to be safe
                }
                else
                {
                    // Default to keeping for safety
                    shouldRemove = false;
                }

                if (shouldRemove)
                {
                    childrenToRemove.Add(child);
                }
            }

            foreach (var child in childrenToRemove)
            {
                Logger.Msg($"[PropertyFactory] Removing unnecessary child: {child.name}");
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void RegisterPrefabWithFishNet(NetworkObject prefabNO, PrefabObjects spawnablePrefabs, string prefabName)
        {
            try
            {
                if (spawnablePrefabs != null)
                {
                    bool alreadyRegistered = false;
                    int existingCount = spawnablePrefabs.GetObjectCount();
                    for (int i = 0; i < existingCount; i++)
                    {
                        NetworkObject existing = spawnablePrefabs.GetObject(true, i);
                        if (existing != null && existing.gameObject != null && existing.gameObject.name == prefabName)
                        {
                            alreadyRegistered = true;
                            break;
                        }
                    }

                    if (!alreadyRegistered)
                    {
                        spawnablePrefabs.AddObject(prefabNO);
                        Logger.Msg($"[PropertyFactory] Registered prefab: {prefabName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyFactory] Failed to register {prefabName} in SpawnablePrefabs: {ex.Message}");
            }
        }

        private static void OrganizePrefab(GameObject prefabRoot, string typeName)
        {
            try
            {
                // Use DontDestroyOnLoad container like NPCs do
                PropertyPrefabContainer.OrganizePrefab(prefabRoot, typeName);
                Logger.Msg($"[PropertyFactory] Organized prefab '{typeName}' under @S1API_PersistentPrefabs/{typeName}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyFactory] Failed to organize prefab {typeName}: {ex.Message}");
            }
        }

        private static string GetPrefabNameForType(Type propertyType)
        {
            string typeName = propertyType != null ? propertyType.Name : "UnknownProperty";
            return $"S1API_{typeName}";
        }

        private static void InvokeConfigurePrefabWithoutInstance(Type propertyType, PropertyPrefabBuilder builder)
        {
            if (propertyType == null || builder == null)
                return;

            var configureMethod = propertyType.GetMethod("ConfigurePrefab",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (configureMethod == null || configureMethod.DeclaringType == typeof(PropertyFactory))
                return;

            PropertyFactory? tempInstance = null;
            try
            {
                tempInstance = (PropertyFactory)System.Runtime.Serialization.FormatterServices
                    .GetUninitializedObject(propertyType);
                configureMethod.Invoke(tempInstance, new object[] { builder });
            }
            finally
            {
                tempInstance = null;
            }
        }

        private static void MarkPrefabsConfigured()
        {
            _prefabsConfiguredForLocalProcess = true;
        }

        #endregion

        #region Pre-Registration

        /// <summary>
        /// Pre-registers a per-type property prefab into FishNet spawnables without creating a live instance.
        /// Should be called on both server and client before any property instances are spawned.
        /// </summary>
        public static void PreRegisterPrefabForType(Type propertyType)
        {
            try
            {
                GetOrCreatePerPropertyPrefab(propertyType, null);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[S1API] Failed to pre-register property prefab for {propertyType?.Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Scans loaded assemblies for subclasses of S1API.Properties.PropertyFactory and pre-registers their prefabs.
        /// </summary>
        public static void PreRegisterAllPropertyPrefabs()
        {
            try
            {
                var nm = InstanceFinder.NetworkManager;
                var spawnables = nm?.SpawnablePrefabs;
                if (spawnables == null)
                    return;

                var baseType = typeof(PropertyFactory);
                var baseAssembly = baseType.Assembly;
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int ai = 0; ai < asms.Length; ai++)
                {
                    var asm = asms[ai];
                    Type[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    for (int ti = 0; ti < types.Length; ti++)
                    {
                        var t = types[ti];
                        if (t == null || t.IsAbstract)
                            continue;
                        if (baseType.IsAssignableFrom(t))
                        {
                            if (t.Assembly == baseAssembly)
                                continue;

                            PreRegisterPrefabForType(t);
                        }
                    }
                }

                MarkPrefabsConfigured();
            }
            catch (Exception ex)
            {
                Logger.Error($"[S1API] PreRegisterAllPropertyPrefabs failed: {ex.Message}");
                Logger.Error($"[S1API] Stack Trace: {ex.StackTrace}");
            }
        }

        #endregion

        #region Spawn API

        /// <summary>
        /// Register a property spawn request. The PropertyNetworkBootstrap will handle timing and spawning
        /// when the network is ready. Must be called on server.
        /// </summary>
        /// <param name="position">World position to spawn the property.</param>
        /// <param name="rotation">Rotation for the property.</param>
        /// <param name="activationDelay">Delay before activating the property instance.</param>
        /// <param name="spawnDelay">Delay before spawning on network.</param>
        /// <returns>The spawned property wrapper, or null if registration failed.</returns>
        public static CustomProperty? Spawn(Type propertyType, Vector3 position, Quaternion rotation,
            float activationDelay = 0.5f, float spawnDelay = 1f)
        {
            if (propertyType == null)
            {
                Logger.Error("[PropertyFactory] Cannot spawn property with null type.");
                return null;
            }

            if (!typeof(PropertyFactory).IsAssignableFrom(propertyType))
            {
                Logger.Error($"[PropertyFactory] Type {propertyType.Name} does not inherit from PropertyFactory.");
                return null;
            }

            try
            {
                GameObject prefab = GetOrCreatePerPropertyPrefab(propertyType, null);

                var nm = InstanceFinder.NetworkManager;
                if (nm == null || !nm.IsServer)
                {
                    Logger.Warning("[PropertyFactory] Spawn can only be called on the server.");
                    return null;
                }

                NetworkObject? spawnablePrefab = null;
                try
                {
                    var spawnablePrefabs = nm.SpawnablePrefabs;
                    if (spawnablePrefabs != null)
                    {
                        int count = spawnablePrefabs.GetObjectCount();
                        for (int i = 0; i < count; i++)
                        {
                            NetworkObject obj = spawnablePrefabs.GetObject(true, i);
                            if (obj != null && obj.gameObject != null && obj.gameObject.name == prefab.name)
                            {
                                spawnablePrefab = obj;
                                break;
                            }
                        }
                    }
                }
                catch { }

                GameObject prefabToUse = spawnablePrefab?.gameObject ?? prefab;
                Logger.Msg($"[PropertyFactory] Spawning {propertyType.Name}. Using prefab source: {prefabToUse.name} (Source: {(spawnablePrefab != null ? "SpawnableList" : "DirectRef")})");
                
                // Find the scene's @Properties container to parent the instance
                Transform propertiesContainer = GetPropertiesContainer();
                
                // IMPORTANT: Set the prefab inactive BEFORE instantiation to prevent Awake() from 
                // running before we have a chance to wire up required references (BoundingBox, 
                // Container, PoI, ForSaleSign, etc.). Awake() in Property.cs accesses these
                // immediately, causing NullReferenceException if not configured first.
                bool wasActive = prefabToUse.activeSelf;
                prefabToUse.SetActive(false);
                
                Logger.Msg($"[PropertyFactory] Instantiating property instance at {position}...");
                GameObject instance = UnityEngine.Object.Instantiate<GameObject>(prefabToUse, position, rotation);
                instance.name = prefab.name.Replace("S1API_", "");
                Logger.Msg($"[PropertyFactory] Instance instantiated: {instance.name}");
                
                // Restore the prefab's original state
                prefabToUse.SetActive(wasActive);
                
                // Parent to @Properties - REMOVED (Moving to bootstrap after spawn to avoid "not root" error)
                if (propertiesContainer == null)
                {
                    Logger.Warning("[PropertyFactory] @Properties container not found in scene.");
                }

                var no = instance.GetComponent<NetworkObject>();
                if (no == null)
                {
                    no = instance.AddComponent<NetworkObject>();
                    Logger.Msg("[PropertyFactory] Added NetworkObject component to instance");
                }

                var propertyComp = instance.GetComponent<S1Property.Property>();
                if (propertyComp == null)
                {
                    Logger.Error("[PropertyFactory] Instance is missing Property component!");
                }
                
                var customProperty = new CustomProperty(propertyComp);
                
                // Rewire serialized field references to point to this instance's children
                RewirePropertyReferences(instance);
                
                // Wire container at spawn time (scene is guaranteed loaded)
                ConfigureContainerForInstance(instance);

                // NOW activate the instance so Awake() runs with all references properly configured.
                // This is safe because RewirePropertyReferences and ConfigureContainerForInstance
                // have already set up BoundingBox, Container, PoI, ForSaleSign, etc.
                instance.SetActive(true);
                
                // Then deactivate so the bootstrap can control activation timing
                instance.SetActive(false);

                PropertyNetworkBootstrap.RegisterPendingNetworkSpawn(customProperty, no, activationDelay, spawnDelay);
                
                Logger.Msg($"[PropertyFactory] Registered property spawn: {customProperty?.Name ?? instance.name} at {position}");

                return customProperty;
            }
            catch (Exception ex)
            {
                Logger.Error($"[PropertyFactory] Failed to spawn property {propertyType.Name}: {ex.Message}");
                Logger.Error($"[PropertyFactory] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Create and spawn a property instance with default rotation.
        /// </summary>
        public static CustomProperty? Spawn(Type propertyType, Vector3 position)
        {
            return Spawn(propertyType, position, Quaternion.identity);
        }

        private static Transform GetPropertiesContainer()
        {
            try
            {
                var propertiesObj = GameObject.Find("@Properties");
                if (propertiesObj != null)
                    return propertiesObj.transform;

                var allProperties = UnityEngine.Object.FindObjectsOfType<S1Property.Property>();
                if (allProperties.Length > 0)
                {
                    return allProperties[0].transform.parent;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static void ConfigureContainerForInstance(GameObject instance)
        {
            try
            {
                var propertyComp = instance.GetComponent<S1Property.Property>();
                if (propertyComp == null)
                {
                    Logger.Warning("[PropertyFactory] Cannot configure container - no Property component.");
                    return;
                }

                var propertyContentsRoot = GameObject.Find("Property Contents");
                if (propertyContentsRoot == null || propertyContentsRoot.transform.childCount == 0)
                {
                    Logger.Warning("[PropertyFactory] Property Contents scene container not found or empty.");
                    return;
                }

                var templateContainer = propertyContentsRoot.transform.GetChild(0).gameObject;
                var clonedContainer = UnityEngine.Object.Instantiate(templateContainer);
                clonedContainer.name = "Container";

                var childrenToRemove = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in clonedContainer.transform)
                {
                    childrenToRemove.Add(child);
                }
                foreach (var child in childrenToRemove)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                clonedContainer.transform.SetParent(instance.transform, false);
                clonedContainer.transform.localPosition = UnityEngine.Vector3.zero;

                var existingContainer = clonedContainer.GetComponent<S1Container>();
                if (existingContainer != null)
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "Container", existingContainer);
                    Logger.Msg($"[PropertyFactory] Wired PropertyContentsContainer '{clonedContainer.name}' to property instance.");
                }
                else
                {
                    Logger.Warning("[PropertyFactory] Cloned container missing PropertyContentsContainer component!");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyFactory] Failed to configure container for instance: {ex.Message}");
            }
        }

        private static void RewirePropertyReferences(GameObject instance)
        {
            try
            {
                var propertyComp = instance.GetComponent<S1Property.Property>();
                if (propertyComp == null)
                {
                    Logger.Error($"[PropertyFactory] Cannot rewire references: Property component not found on {instance.name}");
                    return;
                }

                Logger.Msg($"[PropertyFactory] Rewiring references for {instance.name}...");
                foreach (Transform child in instance.transform)
                {
                    var boundsCollider = child.GetComponent<BoxCollider>();
                    if (boundsCollider != null)
                    {
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "BoundingBox", child.gameObject);
                        Logger.Msg($"[PropertyFactory] Rewired BoundingBox field to child '{child.name}'");
                        continue;
                    }

                    if (child.name.Contains("ForSaleSign") || child.GetComponent<Canvas>() != null)
                    {
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "ForSaleSign", child.gameObject);
                        Logger.Msg($"[PropertyFactory] Rewired ForSaleSign field to child '{child.name}'");
                        continue;
                    }

                    if (child.name.Contains("Spawn"))
                    {
                        if (child.name.Contains("NPC") || child.name.Contains("EmployeeContainer"))
                        {
                            ReflectionUtils.TrySetFieldOrProperty(propertyComp, "EmployeeContainer", child);
                            Logger.Msg($"[PropertyFactory] Rewired EmployeeContainer field to child '{child.name}'");
                            continue;
                        }
                        else if (!child.name.Contains("Idle"))
                        {
                            ReflectionUtils.TrySetFieldOrProperty(propertyComp, "SpawnPoint", child);
                            Logger.Msg($"[PropertyFactory] Rewired SpawnPoint field to child '{child.name}'");
                            continue;
                        }
                        else
                        {
                            ReflectionUtils.TrySetFieldOrProperty(propertyComp, "InteriorSpawnPoint", child);
                            Logger.Msg($"[PropertyFactory] Rewired InteriorSpawnPoint field to child '{child.name}'");
                            continue;
                        }
                    }

                    if (child.name.Contains("Idle") || child.name.Contains("EmployeeIdle"))
                    {
                        var allIdlePoints = instance.GetComponentsInChildren<Transform>()
                            .Where(t => t.name.Contains("EmployeeIdlePoint") || t.name.Contains("Idle"))
                            .ToArray();
                        if (allIdlePoints != null && allIdlePoints.Length > 0)
                        {
                            ReflectionUtils.TrySetFieldOrProperty(propertyComp, "EmployeeIdlePoints", allIdlePoints);
                            Logger.Msg($"[PropertyFactory] Rewired EmployeeIdlePoints field, found {allIdlePoints.Length} points");
                            continue;
                        }
                    }

                    if (child.name.Contains("Listing"))
                    {
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "ListingPoster", child);
                        Logger.Msg($"[PropertyFactory] Rewired ListingPoster field to child '{child.name}'");
                        continue;
                    }

                    if (child.name.Contains("NPCSpawn") || child.name.Contains("NPC Spawn"))
                    {
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "NPCSpawnPoint", child);
                        Logger.Msg($"[PropertyFactory] Rewired NPCSpawnPoint field to child '{child.name}'");
                        continue;
                    }
                }

                // Fallback: scan for any remaining missing references
                var boundsBox = instance.transform.Find("BoundingBox") ?? instance.transform.Find("Bounds") ?? 
                    instance.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Bounding") || t.GetComponent<BoxCollider>() != null);
                if (boundsBox != null && !IsFieldSet(propertyComp, "BoundingBox"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "BoundingBox", boundsBox.gameObject);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired BoundingBox to '{boundsBox.name}'");
                }

                var forSale = instance.transform.Find("ForSaleSign") ?? 
                    instance.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("ForSale"));
                if (forSale != null && !IsFieldSet(propertyComp, "ForSaleSign"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "ForSaleSign", forSale.gameObject);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired ForSaleSign to '{forSale.name}'");
                }

                var listing = instance.transform.Find("ListingPoster") ?? instance.transform.Find("Listing") ??
                    instance.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("Listing"));
                if (listing != null && !IsFieldSet(propertyComp, "ListingPoster"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "ListingPoster", listing);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired ListingPoster to '{listing.name}'");
                }

                var poi = instance.GetComponentInChildren<S1Map.POI>(true);
                if (poi != null && !IsFieldSet(propertyComp, "PoI"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "PoI", poi);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired PoI to '{poi.name}'");
                }

                var container = instance.GetComponentInChildren<S1Container>(true);
                if (container != null && !IsFieldSet(propertyComp, "Container"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "Container", container);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired Container to '{container.name}'");
                }

                var employeeContainer = instance.transform.Find("Employees") ?? instance.transform.Find("EmployeeContainer");
                if (employeeContainer != null && !IsFieldSet(propertyComp, "EmployeeContainer"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "EmployeeContainer", employeeContainer);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired EmployeeContainer to '{employeeContainer.name}'");
                }

                var spawnPoint = instance.transform.Find("SpawnPoint");
                if (spawnPoint != null && !IsFieldSet(propertyComp, "SpawnPoint"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "SpawnPoint", spawnPoint);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired SpawnPoint to '{spawnPoint.name}'");
                }

                var interiorSpawn = instance.transform.Find("InteriorSpawnPoint");
                if (interiorSpawn != null && !IsFieldSet(propertyComp, "InteriorSpawnPoint"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "InteriorSpawnPoint", interiorSpawn);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired InteriorSpawnPoint to '{interiorSpawn.name}'");
                }

                var npcSpawn = instance.transform.Find("NPCSpawn") ?? instance.transform.Find("NPCSpawnPoint");
                if (npcSpawn != null && !IsFieldSet(propertyComp, "NPCSpawnPoint"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "NPCSpawnPoint", npcSpawn);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired NPCSpawnPoint to '{npcSpawn.name}'");
                }

                var idlePoints = instance.transform.Cast<Transform>()
                    .Where(t => t.name.Contains("EmployeeIdlePoint") || t.name.Contains("IdlePoint"))
                    .ToArray();
                if (idlePoints.Length > 0 && !IsFieldSet(propertyComp, "EmployeeIdlePoints"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "EmployeeIdlePoints", idlePoints);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired EmployeeIdlePoints, found {idlePoints.Length} points");
                }

                var switches = instance.GetComponentsInChildren<S1Misc.ModularSwitch>(true);
                if (switches.Length > 0 && !IsFieldSet(propertyComp, "Switches"))
                {
                    var switchList = new System.Collections.Generic.List<S1Misc.ModularSwitch>(switches);
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "Switches", switchList);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired Switches, found {switchList.Count} switches");
                }

                var toggleables = instance.GetComponentsInChildren<S1Interaction.InteractableToggleable>(true);
                if (toggleables.Length > 0 && !IsFieldSet(propertyComp, "Toggleables"))
                {
                    var toggleableList = new System.Collections.Generic.List<S1Interaction.InteractableToggleable>(toggleables);
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "Toggleables", toggleableList);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired Toggleables, found {toggleableList.Count} toggleables");
                }

                var docks = instance.GetComponentsInChildren<S1Delivery.LoadingDock>(true);
                if (docks.Length > 0 && !IsFieldSet(propertyComp, "LoadingDocks"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "LoadingDocks", docks);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired LoadingDocks, found {docks.Length} docks");
                }

                var disposal = instance.GetComponentInChildren<S1Property.PropertyDisposalArea>(true);
                if (disposal != null && !IsFieldSet(propertyComp, "DisposalArea"))
                {
                    ReflectionUtils.TrySetFieldOrProperty(propertyComp, "DisposalArea", disposal);
                    Logger.Msg($"[PropertyFactory] Fallback: rewired DisposalArea to '{disposal.name}'");
                }

                var boundsCube = instance.transform.Find("BoundingBox/Cube");
                if (boundsCube != null && !IsFieldSet(propertyComp, "propertyBoundsColliders"))
                {
                    var colliders = new UnityEngine.BoxCollider[] { boundsCube.GetComponent<UnityEngine.BoxCollider>() };
                    if (colliders[0] != null)
                    {
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "propertyBoundsColliders", colliders);
                        Logger.Msg($"[PropertyFactory] Fallback: rewired propertyBoundsColliders to '{boundsCube.name}'");
                    }
                }

                var worldspaceUITemplate = GameObject.Find("UI/ManagementWorldspaceCanvas/Docks Warehouse Worldspace UI Container");
                if (worldspaceUITemplate != null && !IsFieldSet(propertyComp, "WorldspaceUIContainer"))
                {
                    var worldspaceUIRoot = GameObject.Find("UI/ManagementWorldspaceCanvas");
                    if (worldspaceUIRoot != null)
                    {
                        var clonedWorldspaceUI = UnityEngine.Object.Instantiate(worldspaceUITemplate);
                        clonedWorldspaceUI.name = $"{instance.name}_WorldspaceUI";
                        clonedWorldspaceUI.transform.SetParent(worldspaceUIRoot.transform, false);
                        clonedWorldspaceUI.transform.localPosition = worldspaceUITemplate.transform.localPosition;
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "WorldspaceUIContainer", clonedWorldspaceUI.transform);
                        Logger.Msg($"[PropertyFactory] Fallback: cloned and wired WorldspaceUIContainer to '{clonedWorldspaceUI.name}'");
                    }
                }

                var listingPosterTemplate = GameObject.Find("Map/Hyland Point/Region_Downtown/RE Office/Interior/Whiteboard/PropertyListing Docks Warehouse");
                if (listingPosterTemplate != null && !IsFieldSet(propertyComp, "ListingPoster"))
                {
                    var whiteboardRoot = GameObject.Find("Map/Hyland Point/Region_Downtown/RE Office/Interior/Whiteboard");
                    if (whiteboardRoot != null)
                    {
                        var clonedListing = UnityEngine.Object.Instantiate(listingPosterTemplate);
                        clonedListing.name = $"{instance.name}_Listing";
                        clonedListing.transform.SetParent(whiteboardRoot.transform, false);
                        clonedListing.transform.localPosition = listingPosterTemplate.transform.localPosition;
                        clonedListing.SetActive(false);
                        ReflectionUtils.TrySetFieldOrProperty(propertyComp, "ListingPoster", clonedListing.transform);
                        Logger.Msg($"[PropertyFactory] Fallback: cloned and wired ListingPoster to '{clonedListing.name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyFactory] Failed to rewire property references: {ex.Message}");
            }
        }

        private static bool IsFieldSet(object target, string fieldName)
        {
            try
            {
                var type = target.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var fi = type.GetField(fieldName, flags);
                if (fi != null)
                {
                    return fi.GetValue(target) != null;
                }
                var pi = type.GetProperty(fieldName, flags);
                if (pi != null && pi.CanRead)
                {
                    return pi.GetValue(target) != null;
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Abstract Methods for Subclasses

        /// <summary>
        /// Configure the property prefab before network registration.
        /// Override to set property name, price, employee capacity, etc.
        /// </summary>
        /// <param name="builder">Builder for configuring the property prefab.</param>
        protected abstract void ConfigurePrefab(PropertyPrefabBuilder builder);

        /// <summary>
        /// Get the property name for this factory.
        /// </summary>
        protected abstract string PropertyName { get; }

        /// <summary>
        /// Get the unique property code.
        /// </summary>
        protected abstract string PropertyCode { get; }

        #endregion
    }
}
