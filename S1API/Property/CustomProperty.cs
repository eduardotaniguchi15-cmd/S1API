#if (IL2CPPMELON)
using S1Property = Il2CppScheduleOne.Property;
using S1Tiles = Il2CppScheduleOne.Tiles;
using S1Container = Il2CppScheduleOne.Property.PropertyContentsContainer;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Property = ScheduleOne.Property;
using S1Tiles = ScheduleOne.Tiles;
using S1Container = ScheduleOne.Property.PropertyContentsContainer;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;

#if (IL2CPPBEPINEX || IL2CPPMELON)
using Il2CppSystem.Collections.Generic;
#else
#endif

namespace S1API.Property
{
    /// <summary>
    /// Wrapper class providing modder-friendly access to custom properties.
    /// Created when a property is spawned via <see cref="PropertyFactory.Spawn{TCustomProperty}"/>
    /// </summary>
    /// <remarks>
    /// Use the derived property factory class to spawn instances.
    /// Access the underlying <see cref="GameObject"/> via <see cref="GameObject"/> property for advanced scenarios.
    /// </remarks>
    /// <example>
    /// // Spawning a custom property
    /// var warehouse = CustomWarehouse.Spawn(new Vector3(100f, 0f, 200f));
    ///
    /// // Accessing property data
    /// string name = warehouse.Name;
    /// float price = warehouse.Price;
    /// bool owned = warehouse.IsOwned;
    /// </example>
    public class CustomProperty
    {
        private readonly S1Property.Property? _property;
        private readonly GameObject? _gameObject;

        /// <summary>
        /// Create a new CustomProperty wrapper.
        /// </summary>
        /// <param name="property">The underlying game property component.</param>
        public CustomProperty(S1Property.Property? property)
        {
            _property = property;
            _gameObject = property?.gameObject;
        }

        /// <summary>
        /// Create a CustomProperty wrapper from a GameObject.
        /// </summary>
        /// <param name="gameObject">The property GameObject.</param>
        public CustomProperty(GameObject? gameObject)
        {
            _gameObject = gameObject;
            _property = gameObject?.GetComponent<S1Property.Property>();
        }

        /// <summary>
        /// Get the wrapped GameObject.
        /// </summary>
        public GameObject? GameObject => _gameObject;

        /// <summary>
        /// Get the wrapped property component.
        /// </summary>
        public S1Property.Property? Property => _property;

        /// <summary>
        /// Get the property name.
        /// </summary>
        public string Name
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("PropertyName",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return field?.GetValue(_property)?.ToString() ?? string.Empty;
                    }
                }
                catch { }
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the property code.
        /// </summary>
        public string Code
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("PropertyCode",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return field?.GetValue(_property)?.ToString() ?? string.Empty;
                    }
                }
                catch { }
                return string.Empty;
            }
        }

        /// <summary>
        /// Get the property price.
        /// </summary>
        public float Price
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("Price",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return Convert.ToSingle(field?.GetValue(_property) ?? 0f);
                    }
                }
                catch { }
                return 0f;
            }
        }

        /// <summary>
        /// Get the employee capacity.
        /// </summary>
        public int EmployeeCapacity
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("EmployeeCapacity",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return Convert.ToInt32(field?.GetValue(_property) ?? 0);
                    }
                }
                catch { }
                return 0;
            }
        }

        /// <summary>
        /// Check if the property is owned.
        /// </summary>
        public bool IsOwned
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("isOwned",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return Convert.ToBoolean(field?.GetValue(_property) ?? false);
                    }
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Check if the property is a business.
        /// </summary>
        public bool IsBusiness
        {
            get
            {
                try
                {
                    if (_property != null)
                    {
                        var field = typeof(S1Property.Property).GetField("IsBusiness",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);
                        return Convert.ToBoolean(field?.GetValue(_property) ?? false);
                    }
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Get the transform position.
        /// </summary>
        public Vector3 Position => _gameObject?.transform.position ?? Vector3.zero;

        /// <summary>
        /// Get all grid components on this property.
        /// </summary>
        public List<S1Tiles.Grid> Grids
        {
            get
            {
                var grids = new List<S1Tiles.Grid>();
                if (_gameObject != null)
                {
                    grids.AddRange(_gameObject.GetComponentsInChildren<S1Tiles.Grid>(true));
                }
                return grids;
            }
        }

        /// <summary>
        /// Get the main grid component (first found).
        /// </summary>
        public S1Tiles.Grid? MainGrid
        {
            get
            {
                if (_gameObject != null)
                {
                    return _gameObject.GetComponentInChildren<S1Tiles.Grid>(true);
                }
                return null;
            }
        }

        /// <summary>
        /// Get the property contents container.
        /// </summary>
        public S1Container? ContentsContainer
        {
            get
            {
                if (_gameObject != null)
                {
                    return _gameObject.GetComponentInChildren<S1Container>(true);
                }
                return null;
            }
        }

        /// <summary>
        /// Check if a world point is inside this property's bounds.
        /// </summary>
        /// <param name="point">World point to check.</param>
        /// <returns>True if the point is inside.</returns>
        public bool IsPointInside(Vector3 point)
        {
            try
            {
                if (_property != null)
                {
                    var method = typeof(S1Property.Property).GetMethod("IsPointInside",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    return Convert.ToBoolean(method?.Invoke(_property, new object[] { point }) ?? false);
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Set the property as owned.
        /// </summary>
        public void SetOwned()
        {
            try
            {
                if (_property != null)
                {
                    var method = typeof(S1Property.Property).GetMethod("SetOwned",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    method?.Invoke(_property, null);
                }
            }
            catch { }
        }

        /// <summary>
        /// Get the property wrapper for use with S1API's property management.
        /// </summary>
        public PropertyWrapper? AsWrapper()
        {
            if (_property != null)
            {
                return new PropertyWrapper(_property);
            }
            return null;
        }

        /// <summary>
        /// Check if the underlying property is valid.
        /// </summary>
        public bool IsValid => _property != null && _gameObject != null;

        /// <summary>
        /// Implicit conversion to the underlying property component.
        /// </summary>
        public static implicit operator S1Property.Property?(CustomProperty customProperty)
        {
            return customProperty?._property;
        }

        /// <summary>
        /// Implicit conversion to the underlying GameObject.
        /// </summary>
        public static implicit operator GameObject?(CustomProperty customProperty)
        {
            return customProperty?._gameObject;
        }
    }
}
