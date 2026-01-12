#if (IL2CPPMELON)
using S1Property = Il2CppScheduleOne.Property;
using S1Tiles = Il2CppScheduleOne.Tiles;
using S1Delivery = Il2CppScheduleOne.Delivery;
using S1Map = Il2CppScheduleOne.Map;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Property = ScheduleOne.Property;
using S1Tiles = ScheduleOne.Tiles;
using S1Delivery = ScheduleOne.Delivery;
using S1Map = ScheduleOne.Map;
#endif
using System;
using S1API.Internal.Utils;
using S1API.Logging;
using UnityEngine;

namespace S1API.Property
{
    /// <summary>
    /// Builder for configuring property prefab settings before network spawn.
    /// Use to set property metadata, grid configuration, and structure details.
    /// </summary>
    /// <remarks>
    /// Configuration must be done in <see cref="PropertyFactory.ConfigurePrefab"/> for proper save/load behavior.
    /// All builder methods return the builder instance for fluent chaining.
    /// </remarks>
    public sealed class PropertyPrefabBuilder
    {
        private static readonly Log Logger = new Log("PropertyPrefabBuilder");
        private readonly GameObject _prefabRoot;
        private readonly Type _ownerType;

        private S1Property.Property? _propertyComponent;

        internal PropertyPrefabBuilder(GameObject prefabRoot, Type ownerType)
        {
            _prefabRoot = prefabRoot;
            _ownerType = ownerType;
        }

        /// <summary>
        /// Reposition a named child object within the prefab.
        /// </summary>
        /// <param name="childName">Name of the child object.</param>
        /// <param name="position">New local position.</param>
        /// <param name="rotation">New local rotation.</param>
        /// <returns>The builder instance.</returns>
        public PropertyPrefabBuilder RepositionChild(string childName, Vector3 position, Quaternion rotation)
        {
            try
            {
                var child = _prefabRoot.transform.Find(childName);
                if (child == null)
                {
                    // search recursively if not found at top level
                    foreach (Transform c in _prefabRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (c.name == childName)
                        {
                            child = c;
                            break;
                        }
                    }
                }

                if (child != null)
                {
                    child.localPosition = position;
                    child.localRotation = rotation;
                    Logger.Msg($"[PropertyPrefabBuilder] Repositioned child '{childName}' to {position}");
                }
                else
                {
                    Logger.Warning($"[PropertyPrefabBuilder] Could not find child '{childName}' to reposition.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Error repositioning child '{childName}': {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Reposition a Loading Dock by index (0-based).
        /// </summary>
        public PropertyPrefabBuilder RepositionLoadingDock(int index, Vector3 position, Quaternion rotation)
        {
            try
            {
                var docks = _prefabRoot.GetComponentsInChildren<S1Delivery.LoadingDock>(true);
                if (docks != null && index >= 0 && index < docks.Length)
                {
                    var dock = docks[index];
                    dock.transform.localPosition = position;
                    dock.transform.localRotation = rotation;
                    Logger.Msg($"[PropertyPrefabBuilder] Repositioned LoadingDock[{index}] to {position}");
                }
                else
                {
                    Logger.Warning($"[PropertyPrefabBuilder] LoadingDock index {index} out of range (Found {docks?.Length ?? 0} docks).");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Error repositioning LoadingDock[{index}]: {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Reposition the Property POI (Intercom/Save Point).
        /// </summary>
        public PropertyPrefabBuilder RepositionPOI(Vector3 position, Quaternion rotation)
        {
            try
            {
                var poi = _prefabRoot.GetComponentInChildren<S1Map.POI>(true);
                if (poi != null)
                {
                    poi.transform.localPosition = position;
                    poi.transform.localRotation = rotation;
                    Logger.Msg($"[PropertyPrefabBuilder] Repositioned POI to {position}");
                }
                else
                {
                    Logger.Warning($"[PropertyPrefabBuilder] No POI found to reposition.");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Error repositioning POI: {ex.Message}");
            }
            return this;
        }

        private S1Property.Property EnsurePropertyComponent()
        {
            if (_propertyComponent == null)
            {
                _propertyComponent = _prefabRoot.GetComponent<S1Property.Property>();
            }
            return _propertyComponent!;
        }

        #region Property Metadata

        /// <summary>
        /// Set the display name for this property.
        /// </summary>
        /// <param name="name">The property name shown in UI.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder WithName(string name)
        {
            try
            {
                var property = EnsurePropertyComponent();
                // Set the backing field 'propertyName' directly, not the getter-only property 'PropertyName'
                ReflectionUtils.TrySetFieldOrProperty(property, "propertyName", name);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to set property name: {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Set the property code identifier.
        /// </summary>
        /// <param name="code">Unique code for the property.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder WithCode(string code)
        {
            try
            {
                var property = EnsurePropertyComponent();
                // Set the backing field 'propertyCode' directly, not the getter-only property 'PropertyCode'
                ReflectionUtils.TrySetFieldOrProperty(property, "propertyCode", code);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to set property code: {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Set the property price.
        /// </summary>
        /// <param name="price">Purchase price.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder WithPrice(float price)
        {
            try
            {
                var property = EnsurePropertyComponent();
                ReflectionUtils.TrySetFieldOrProperty(property, "Price", price);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to set property price: {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Set the employee capacity for this property.
        /// </summary>
        /// <param name="capacity">Maximum number of employees.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder WithEmployeeCapacity(int capacity)
        {
            try
            {
                var property = EnsurePropertyComponent();
                ReflectionUtils.TrySetFieldOrProperty(property, "EmployeeCapacity", capacity);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to set employee capacity: {ex.Message}");
            }
            return this;
        }

        #endregion

        #region Grid Configuration

        /// <summary>
        /// Get the primary grid component on this property for configuration.
        /// Returns null if no grid is found.
        /// </summary>
        public S1Tiles.Grid? GetMainGrid()
        {
            return _prefabRoot.GetComponentInChildren<S1Tiles.Grid>(true);
        }

        #endregion

        #region Structure Integration

        /// <summary>
        /// Add a child GameObject as a structure to this property.
        /// </summary>
        /// <param name="structure">The structure GameObject to add.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder WithStructure(GameObject structure)
        {
            if (structure == null)
            {
                Logger.Warning("[PropertyPrefabBuilder] Cannot add null structure.");
                return this;
            }

            try
            {
                structure.transform.SetParent(_prefabRoot.transform, false);
                Logger.Msg($"[PropertyPrefabBuilder] Integrated structure '{structure.name}' into property prefab.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to integrate structure: {ex.Message}");
            }
            return this;
        }

        #endregion

        #region Spawn Points

        /// <summary>
        /// Add a spawn point for employees/customers.
        /// </summary>
        /// <param name="position">Local position for the spawn point.</param>
        /// <param name="rotation">Local rotation.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder AddSpawnPoint(Vector3 position, Quaternion rotation)
        {
            try
            {
                var spawnGO = new GameObject("SpawnPoint");
                spawnGO.transform.SetParent(_prefabRoot.transform, false);
                spawnGO.transform.localPosition = position;
                spawnGO.transform.localRotation = rotation;

                Logger.Msg($"[PropertyPrefabBuilder] Added spawn point at {position}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to add spawn point: {ex.Message}");
            }
            return this;
        }

        /// <summary>
        /// Add an employee idle point where NPCs will wait when at this property.
        /// </summary>
        /// <param name="position">Local position for the idle point.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public PropertyPrefabBuilder AddEmployeeIdlePoint(Vector3 position)
        {
            try
            {
                var idleGO = new GameObject("EmployeeIdlePoint");
                idleGO.transform.SetParent(_prefabRoot.transform, false);
                idleGO.transform.localPosition = position;

                Logger.Msg($"[PropertyPrefabBuilder] Added employee idle point at {position}");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to add employee idle point: {ex.Message}");
            }
            return this;
        }

        #endregion

        #region Container Configuration

        /// <summary>
        /// Configure the property contents container for storage.
        /// </summary>
        /// <param name="capacity">Maximum number of items.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        /// <remarks>
        /// PropertyContentsContainer components exist on separate GameObjects in the "Property Contents" 
        /// scene container. This method will clone an existing container and wire it to the property.
        /// </remarks>
        public PropertyPrefabBuilder WithContainerCapacity(int capacity)
        {
            try
            {
                // Check if property already has a Container reference
                var propertyComp = _prefabRoot.GetComponent<S1Property.Property>();
                if (propertyComp == null)
                {
                    Logger.Warning("[PropertyPrefabBuilder] No Property component found on prefab root.");
                    return this;
                }

                // Check if there's already a container child (from cloning)
                var existingContainer = _prefabRoot.GetComponentInChildren<S1Property.PropertyContentsContainer>(true);
                
                if (existingContainer == null)
                {
                    // Find Property Contents scene container
                    var propertyContentsRoot = GameObject.Find("Property Contents");
                    if (propertyContentsRoot != null && propertyContentsRoot.transform.childCount > 0)
                    {
                        // Clone the first container as a template
                        var templateContainer = propertyContentsRoot.transform.GetChild(0).gameObject;
                        var clonedContainer = UnityEngine.Object.Instantiate(templateContainer);
                        clonedContainer.name = "Container";
                        
                        // Clear all children (we don't want the template's contents)
                        var childrenToRemove = new System.Collections.Generic.List<Transform>();
                        foreach (Transform child in clonedContainer.transform)
                        {
                            childrenToRemove.Add(child);
                        }
                        foreach (var child in childrenToRemove)
                        {
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                        }
                        
                        // Parent it to the property prefab
                        clonedContainer.transform.SetParent(_prefabRoot.transform, false);
                        clonedContainer.transform.localPosition = UnityEngine.Vector3.zero;
                        
                        existingContainer = clonedContainer.GetComponent<S1Property.PropertyContentsContainer>();
                        
                        // Wire the container to the property
                        if (existingContainer != null)
                        {
                            ReflectionUtils.TrySetFieldOrProperty(propertyComp, "Container", existingContainer);
                            Logger.Msg("[PropertyPrefabBuilder] Cloned and wired PropertyContentsContainer");
                        }
                    }
                    else
                    {
                        Logger.Warning("[PropertyPrefabBuilder] Property Contents scene container not found or empty.");
                    }
                }
                
                // Note: Capacity is not a field on PropertyContentsContainer, it's just a container for BuildableItems
                // The capacity logic is typically handled by the property management system
                Logger.Msg($"[PropertyPrefabBuilder] Container configured (capacity is managed by property system)");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to configure container: {ex.Message}");
            }
            return this;
        }

        #endregion

        #region Grid System

        /// <summary>
        /// Automatically generates a Grid of IndoorTiles with explicit dimensions.
        /// This is the preferred overload when you know your floor dimensions from DefineRoom().
        /// </summary>
        /// <param name="width">Width of the grid area (X dimension).</param>
        /// <param name="depth">Depth of the grid area (Z dimension).</param>
        /// <param name="gridOrigin">World position for the grid origin (center of the floor).</param>
        /// <param name="cellSize">Size of each tile cell (default 0.45f).</param>
        public PropertyPrefabBuilder WithGrid(float width, float depth, Vector3 gridOrigin, float cellSize = 0.45f)
        {
            Logger.Msg($"[PropertyPrefabBuilder] WithGrid called: {width}x{depth} at {gridOrigin}");
            return GenerateGrid(width, depth, gridOrigin, Quaternion.identity, cellSize);
        }

        /// <summary>
        /// Automatically generates a Grid of IndoorTiles based on the bounds of the provided floor object.
        /// </summary>
        /// <param name="floor">The GameObject representing the floor.</param>
        /// <param name="cellSize">Size of each tile cell (default 0.45f).</param>
        /// <param name="yOffset">Vertical offset for the grid (default 0.01f to sit just above floor).</param>
        public PropertyPrefabBuilder WithGridFromFloor(GameObject floor, float cellSize = 0.45f, float yOffset = 0.01f)
        {
            if (floor == null)
            {
                Logger.Warning("[PropertyPrefabBuilder] Cannot generate grid from null floor.");
                return this;
            }

            try
            {
                // Determine bounds from collider or renderer
                Bounds bounds;
                var floorCollider = floor.GetComponent<Collider>();
                var floorRenderer = floor.GetComponent<Renderer>();
                
                if (floorCollider != null)
                {
                    bounds = floorCollider.bounds;
                    Logger.Msg($"[PropertyPrefabBuilder] Using collider bounds: {bounds.size}");
                }
                else if (floorRenderer != null)
                {
                    bounds = floorRenderer.bounds;
                    Logger.Msg($"[PropertyPrefabBuilder] Using renderer bounds: {bounds.size}");
                }
                else
                {
                    Logger.Warning("[PropertyPrefabBuilder] Floor object has no Renderer or Collider to determine bounds.");
                    return this;
                }

                float width = bounds.size.x;
                float depth = bounds.size.z;
                Vector3 gridOrigin = new Vector3(bounds.center.x, bounds.max.y + yOffset, bounds.center.z);
                Quaternion rotation = floor.transform.rotation;

                Logger.Msg($"[PropertyPrefabBuilder] WithGridFromFloor detected: {width:F2}x{depth:F2} at {gridOrigin}");
                return GenerateGrid(width, depth, gridOrigin, rotation, cellSize);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to generate grid from floor: {ex.Message}");
                return this;
            }
        }

        /// <summary>
        /// Internal method to generate the grid with tiles.
        /// </summary>
        private PropertyPrefabBuilder GenerateGrid(float width, float depth, Vector3 gridOrigin, Quaternion rotation, float cellSize)
        {
            try
            {
                // Create Grid container
                var gridGO = new GameObject("Grid");
                gridGO.transform.SetParent(_prefabRoot.transform, false);
                gridGO.transform.position = gridOrigin;
                gridGO.transform.rotation = rotation;

                // Add Grid Component
                var gridComp = gridGO.AddComponent<S1Tiles.Grid>();
                ReflectionUtils.TrySetFieldOrProperty(gridComp, "_parentProperty", EnsurePropertyComponent());

                // Calculate tile counts
                int cols = Mathf.FloorToInt(width / cellSize);
                int rows = Mathf.FloorToInt(depth / cellSize);

                // Offset to start drawing from bottom-left corner relative to center
                float startX = -(width / 2f) + (cellSize / 2f);
                float startZ = -(depth / 2f) + (cellSize / 2f);

                Logger.Msg($"[PropertyPrefabBuilder] Generating Grid: {cols}x{rows} (Size: {width:F2}x{depth:F2})");

                // Find Template Model
                GameObject templateModel = null;
                try 
                {
                    var existingGridTile = GameObject.Find("@Properties/DocksWarehouse/IndustrialShed/MainGrid/Grid [0,0]/Model");
                    if (existingGridTile != null)
                    {
                        templateModel = existingGridTile;
                        Logger.Msg("[PropertyPrefabBuilder] Found existing tile model template.");
                    }
                }
                catch {}

                // Generate Tiles
                for (int x = 0; x < cols; x++)
                {
                    for (int y = 0; y < rows; y++)
                    {
                        string tileName = $"Grid [{x},{y}]";
                        var tileGO = new GameObject(tileName);
                        tileGO.transform.SetParent(gridGO.transform, false);
                        
                        float xPos = startX + (x * cellSize);
                        float zPos = startZ + (y * cellSize);
                        tileGO.transform.localPosition = new Vector3(xPos, 0, zPos);

                        // Add IndoorTile component
                        var tileComp = tileGO.AddComponent<S1Tiles.IndoorTile>();
                        tileComp.x = x;
                        tileComp.y = y;
                        tileComp.OwnerGrid = gridComp;
                        tileComp.AvailableOffset = 1000f;

                        // Visual Model
                        GameObject modelGO;
                        if (templateModel != null)
                        {
                            modelGO = UnityEngine.Object.Instantiate(templateModel, tileGO.transform);
                            modelGO.name = "Model";
                        }
                        else
                        {
                            modelGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            modelGO.name = "Model";
                            modelGO.transform.SetParent(tileGO.transform, false);
                            modelGO.transform.localScale = new Vector3(cellSize, 0.05f, cellSize);
                            if (modelGO.GetComponent<Collider>()) 
                                UnityEngine.Object.DestroyImmediate(modelGO.GetComponent<Collider>());
                        }
                        
                        modelGO.SetActive(false);
                        gridComp.RegisterTile(tileComp);
                    }
                }

                Logger.Msg($"[PropertyPrefabBuilder] Grid generation complete. Created {cols * rows} tiles.");
            }
            catch (Exception ex)
            {
                Logger.Warning($"[PropertyPrefabBuilder] Failed to generate grid: {ex.Message}");
            }

            return this;
        }

        #endregion
    }
}
