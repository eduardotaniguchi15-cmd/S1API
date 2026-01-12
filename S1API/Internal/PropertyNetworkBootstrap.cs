#if (IL2CPPMELON)
using S1Property = Il2CppScheduleOne.Property;
using S1Tiles = Il2CppScheduleOne.Tiles;
using Il2CppFishNet;
using Il2CppFishNet.Managing;
using Il2CppFishNet.Managing.Scened;
using Il2CppFishNet.Transporting;
using Il2CppFishNet.Connection;
using Il2CppFishNet.Managing.Object;
using Il2CppFishNet.Object;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Connection;
using FishNet.Managing.Object;
using FishNet.Object;
using S1Property = ScheduleOne.Property;
using S1Tiles = ScheduleOne.Tiles;
#endif

#if (IL2CPPBEPINEX || IL2CPPMELON)
using Il2CppSystem.Collections.Generic;
#else
using System.Collections.Generic;
#endif

using System;
using System.Collections;
using UnityEngine;
using MelonLoader;
using S1API.Properties;
using S1API.Logging;
using S1API.Property;

namespace S1API.Internal
{
    /// <summary>
    /// INTERNAL: Centralizes network readiness tracking for property spawning and pre-registers property prefabs
    /// on both server and client when the Main scene initializes.
    /// </summary>
    internal static class PropertyNetworkBootstrap
    {
        private static readonly Log Logger = new Log("PropertyNetworkBootstrap");
        private static bool mainSceneInitialized;
        private static bool networkObserved;
        private static bool clientsReady;
        private static bool connectionObjectsReady;
        private static bool prefabsWarmupScheduled;
        private static float mainSceneInitTime;
        private static readonly List<PendingPropertySpawn> PendingSpawns = new List<PendingPropertySpawn>();
        private static bool readinessLogInitialized;
        private static bool lastLogInMain;
        private static bool lastLogServerUp;
        private static bool lastLogClientUp;
        private static bool lastLogRemoteReady;
        private static bool lastLogHasRemote;
        private static bool lastLogClientsReady;
        private static float lastStateLogTime;
        private static bool pendingSpawnBlockedLogged;

        public static bool ClientsReadyToSpawnProperties =>
            mainSceneInitialized &&
            PropertyFactory.PrefabsConfiguredForLocalProcess &&
            connectionObjectsReady &&
            clientsReady;

        public static void ResetFlags()
        {
            mainSceneInitialized = false;
            networkObserved = false;
            clientsReady = false;
            connectionObjectsReady = false;
            prefabsWarmupScheduled = false;
            mainSceneInitTime = 0f;
            PendingSpawns.Clear();
            readinessLogInitialized = false;
            pendingSpawnBlockedLogged = false;
        }

        public static void EnsurePrefabsWarmup()
        {
            if (PropertyFactory.PrefabsConfiguredForLocalProcess)
                return;

            if (prefabsWarmupScheduled)
                return;

            prefabsWarmupScheduled = true;
            MelonCoroutines.Start(PropertyPrefabWarmupCoroutine());
        }

        public static void RegisterPendingNetworkSpawn(CustomProperty property, NetworkObject netObject, float activationDelay, float spawnDelay)
        {
            if (property == null || netObject == null)
                return;

            try
            {
                var nm = InstanceFinder.NetworkManager;
                if (nm != null && !nm.IsServer)
                    return;

                EnsurePrefabsWarmup();

                for (int i = PendingSpawns.Count - 1; i >= 0; i--)
                {
                    var entry = PendingSpawns[i];
                    if (entry == null || entry.NetObject == null || entry.NetObject == netObject)
                        PendingSpawns.RemoveAt(i);
                }

                float now = Time.realtimeSinceStartup;
                float activateAt = now + Math.Max(0f, activationDelay);
                float spawnAt = now + Math.Max(spawnDelay, activationDelay);
                PendingSpawns.Add(new PendingPropertySpawn(property, netObject, activateAt, spawnAt));

                TryProcessPendingSpawns();
            }
            catch (Exception ex)
            {
                Logger.Warning($"[S1API] Failed to register pending property spawn: {ex.Message}");
            }
        }

        public static void OnMainSceneInitialized()
        {
            ResetFlags();
            mainSceneInitialized = true;
            mainSceneInitTime = Time.realtimeSinceStartup;

            try
            {
                var nm = InstanceFinder.NetworkManager;
                var spawnables = nm?.SpawnablePrefabs;
                if (spawnables != null)
                {
                    PropertyFactory.PreRegisterAllPropertyPrefabs();
                }
                else
                {
                    EnsurePrefabsWarmup();
                }
            }
            catch
            {
                EnsurePrefabsWarmup();
            }

            MelonCoroutines.Start(PropertyReadinessMonitor());
        }

        private static IEnumerator PropertyPrefabWarmupCoroutine()
        {
            float start = Time.realtimeSinceStartup;
            float timeout = 20f;

            while (!PropertyFactory.PrefabsConfiguredForLocalProcess && (Time.realtimeSinceStartup - start) < timeout)
            {
                NetworkManager nm = null;
                PrefabObjects spawnables = null;
                try
                {
                    nm = InstanceFinder.NetworkManager;
                    spawnables = nm?.SpawnablePrefabs;
                }
                catch { }

                if (spawnables != null)
                {
                    try
                    {
                        PropertyFactory.PreRegisterAllPropertyPrefabs();
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"[S1API] Property prefab warmup failed: {ex.Message}");
                        break;
                    }
                }

                yield return new WaitForSeconds(0.25f);
            }

            prefabsWarmupScheduled = false;
            TryProcessPendingSpawns();
        }

        private static IEnumerator PropertyReadinessMonitor()
        {
            NetworkManager nm = null;
            float start = Time.realtimeSinceStartup;

            while (nm == null)
            {
                nm = InstanceFinder.NetworkManager;
                if (nm != null) break;
                if (Time.realtimeSinceStartup - start > 10f) yield break;
                yield return new WaitForSeconds(0.1f);
            }

            if (nm != null && !nm.IsServer)
                yield break;

            if (!networkObserved)
            {
                networkObserved = true;
                try
                {
                    var cm = nm.ClientManager;
                    var sm = nm.ServerManager;
                    var scened = nm.SceneManager;

                    MelonCoroutines.Start(PropertyPeriodicEvaluate());
                }
                catch { }
            }

            float maxWait = 12f;
            while (!clientsReady && (Time.realtimeSinceStartup - start) < maxWait)
            {
                EvaluatePropertyReadiness();
                yield return new WaitForSeconds(0.25f);
            }
            EvaluatePropertyReadiness();
            TryProcessPendingSpawns();
        }

        private static IEnumerator PropertyPeriodicEvaluate()
        {
            while (true)
            {
                EvaluatePropertyReadiness();
                TryProcessPendingSpawns();
                yield return new WaitForSeconds(0.25f);
            }
        }

        private static void EvaluatePropertyReadiness()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null)
            {
                clientsReady = false;
                connectionObjectsReady = false;
                return;
            }

            if (!PropertyFactory.PrefabsConfiguredForLocalProcess)
            {
                clientsReady = false;
                connectionObjectsReady = false;
                return;
            }

            bool remoteConnectionsReady = AreRemoteConnectionsReady(nm, out bool hasRemoteClients);
            connectionObjectsReady = remoteConnectionsReady;

            bool inMain = false;
            try { inMain = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Main"; } catch { }

            bool serverUp = nm.IsServer && nm.ServerManager != null && nm.ServerManager.Started;
            bool clientUpIfHost = !nm.IsServer || nm.IsClient || hasRemoteClients;

            clientsReady = inMain && serverUp && clientUpIfHost && remoteConnectionsReady;
            LogPropertyReadinessState(inMain, serverUp, clientUpIfHost, remoteConnectionsReady, hasRemoteClients);
        }

        private static bool AreRemoteConnectionsReady(NetworkManager nm, out bool hasRemoteClients)
        {
            hasRemoteClients = false;

            try
            {
                if (nm == null)
                    return false;

                var serverManager = nm.ServerManager;
                if (serverManager == null)
                    return false;

                var clients = serverManager.Clients;
                if (clients == null)
                    return true;

                foreach (var kvp in clients)
                {
                    var conn = kvp.Value;
                    if (conn == null)
                        continue;
                    if (!conn.IsValid)
                        continue;
                    if (conn.IsLocalClient)
                        continue;

                    hasRemoteClients = true;
                    if (!conn.Authenticated)
                        return false;
                    if (conn.FirstObject == null)
                        return false;
                }

                if (hasRemoteClients)
                {
                    float timeSinceMainScene = Time.realtimeSinceStartup - mainSceneInitTime;
                    if (timeSinceMainScene < 5f)
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryProcessPendingSpawns()
        {
            if (PendingSpawns.Count == 0)
            {
                pendingSpawnBlockedLogged = false;
                return;
            }

            var nm = InstanceFinder.NetworkManager;
            if (nm == null || !nm.IsServer)
                return;

            if (!ClientsReadyToSpawnProperties)
            {
                if (!pendingSpawnBlockedLogged)
                {
                    Logger.Warning($"[PropertyNetworkBootstrap] Spawning blocked! Readiness State: " +
                        $"MainSceneInit={mainSceneInitialized}, " +
                        $"PrefabsConfig={PropertyFactory.PrefabsConfiguredForLocalProcess}, " +
                        $"ConnObjects={connectionObjectsReady}, " +
                        $"ClientsReady={clientsReady}");
                    pendingSpawnBlockedLogged = true;
                }
                return;
            }

            pendingSpawnBlockedLogged = false;

            var serverManager = nm.ServerManager;
            if (serverManager == null)
                return;

            float now = Time.realtimeSinceStartup;

            for (int i = PendingSpawns.Count - 1; i >= 0; i--)
            {
                var pending = PendingSpawns[i];
                if (pending == null)
                {
                    PendingSpawns.RemoveAt(i);
                    continue;
                }

                var netObject = pending.NetObject;
                var property = pending.Property;

                if (netObject == null)
                {
                    Logger.Warning("[PropertyNetworkBootstrap] Found pending spawn with null NetworkObject. Removing.");
                    PendingSpawns.RemoveAt(i);
                    continue;
                }

                var go = netObject.gameObject;
                if (go == null)
                {
                    Logger.Warning("[PropertyNetworkBootstrap] Found pending spawn with destroyed GameObject. Removing.");
                    PendingSpawns.RemoveAt(i);
                    continue;
                }

                if (netObject.IsSpawned)
                {
                    Logger.Msg($"[PropertyNetworkBootstrap] {go.name} is already spawned. Removing from pending.");
                    PendingSpawns.RemoveAt(i);
                    continue;
                }

                if (!pending.ActivationApplied && now >= pending.ActivateAt)
                {
                    pending.ActivationApplied = true;
                    Logger.Msg($"[PropertyNetworkBootstrap] Activation time reached for {go.name} ({now:F2} >= {pending.ActivateAt:F2})");
                    
                    // Activate the GameObject so components initialize
                    if (!go.activeSelf)
                    {
                        go.SetActive(true);
                        Logger.Msg($"[PropertyNetworkBootstrap] Activated property GameObject: {go.name}");
                    }
                }

                if (now < pending.SpawnAt)
                {
                    // Too early to spawn
                    continue;
                }

                Logger.Msg($"[PropertyNetworkBootstrap] Attempting network spawn for {go.name} at {now:F2} (Target Spawn Time: {pending.SpawnAt:F2})");

                try
                {
                    // Ensure the GameObject is active before spawning
                    if (go != null && !go.activeSelf)
                    {
                        go.SetActive(true);
                        Logger.Msg($"[PropertyNetworkBootstrap] Force-activated property before spawn: {go.name}");
                    }
                    
                    serverManager.Spawn(netObject, null, default(UnityEngine.SceneManagement.Scene));
                    Logger.Msg($"[S1API] Successfully spawned property on network: {property?.Name ?? go.name}");
                    
                    // Parent to @Properties container for organization now that it's spawned as root
                    try
                    {
                        var container = GetPropertiesContainer();
                        if (container != null)
                        {
                            go.transform.SetParent(container, true);
                            Logger.Msg($"[PropertyNetworkBootstrap] Parented {go.name} to @Properties after spawn.");
                        }
                    }
                    catch (Exception pex)
                    {
                        Logger.Warning($"[PropertyNetworkBootstrap] Failed to parent property after spawn: {pex.Message}");
                    }

                    // Add to Property registries if not already added
                    var propComp = property?.Property;
                    if (propComp != null)
                    {
                        var registry = S1Property.Property.Properties;
                        if (registry != null && !registry.Contains(propComp))
                        {
                            registry.Add(propComp);
                            Logger.Msg($"[PropertyNetworkBootstrap] Added property to Properties registry: {property.Name}");
                        }
                        
                        var unownedReg = S1Property.Property.UnownedProperties;
                        if (unownedReg != null && !unownedReg.Contains(propComp) && !propComp.IsOwned)
                        {
                            unownedReg.Add(propComp);
                            Logger.Msg($"[PropertyNetworkBootstrap] Added property to UnownedProperties registry: {property.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"[S1API] Failed to spawn pending property '{go.name}': {ex.Message}");
                    Logger.Warning($"[PropertyNetworkBootstrap] Spawn exception stack: {ex.StackTrace}");
                    continue;
                }

                PendingSpawns.RemoveAt(i);
            }
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

        private static void LogPropertyReadinessState(bool inMain, bool serverUp, bool clientUpIfHost, bool remoteConnectionsReady, bool hasRemoteClients)
        {
            float now = Time.realtimeSinceStartup;
            bool stateChanged = !readinessLogInitialized ||
                                lastLogInMain != inMain ||
                                lastLogServerUp != serverUp ||
                                lastLogClientUp != clientUpIfHost ||
                                lastLogRemoteReady != remoteConnectionsReady ||
                                lastLogHasRemote != hasRemoteClients ||
                                lastLogClientsReady != clientsReady;

            if (stateChanged || (!clientsReady && (now - lastStateLogTime) >= 5f))
            {
                readinessLogInitialized = true;
                lastLogInMain = inMain;
                lastLogServerUp = serverUp;
                lastLogClientUp = clientUpIfHost;
                lastLogRemoteReady = remoteConnectionsReady;
                lastLogHasRemote = hasRemoteClients;
                lastLogClientsReady = clientsReady;
                lastStateLogTime = now;
            }
        }

        private sealed class PendingPropertySpawn
        {
            internal readonly CustomProperty Property;
            internal readonly NetworkObject NetObject;
            internal readonly float ActivateAt;
            internal readonly float SpawnAt;
            internal bool ActivationApplied;

            internal PendingPropertySpawn(CustomProperty property, NetworkObject netObject, float activateAt, float spawnAt)
            {
                Property = property;
                NetObject = netObject;
                ActivateAt = activateAt;
                SpawnAt = spawnAt;
                ActivationApplied = false;
            }
        }
    }
}
