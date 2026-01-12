#if (IL2CPPMELON)
using S1Product = Il2CppScheduleOne.Product;
using S1ProductPackaging = Il2CppScheduleOne.Product.Packaging;
using S1ItemFramework = Il2CppScheduleOne.ItemFramework;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Product = ScheduleOne.Product;
using S1ProductPackaging = ScheduleOne.Product.Packaging;
using S1ItemFramework = ScheduleOne.ItemFramework;
#endif

using System.Collections.Generic;
using System.Linq;
using S1API.Items;
using S1API.Storages;
using S1API.Internal.Utils;
using S1API.Products.Packaging;
using UnityEngine;

namespace S1API.Products
{
    /// <summary>
    /// Utility methods for populating storage with product instances.
    /// </summary>
    public static class ProductPopulator
    {
        /// <summary>
        /// Gets a packaging definition by its ID.
        /// </summary>
        /// <param name="packagingId">The ID of the packaging (e.g., "baggie", "jar", "brick").</param>
        /// <returns>The packaging definition, or null if not found.</returns>
        public static PackagingDefinition? GetPackaging(string packagingId)
        {
            var packaging = ItemManager.GetItemDefinition(packagingId);

            if (packaging is PackagingDefinition packagingDef)
                return packagingDef;

            Debug.LogWarning($"[ProductPopulator] Could not find packaging with ID '{packagingId}'");

            return null;
        }

        /// <summary>
        /// Gets all available product definitions from the game registry.
        /// </summary>
        /// <returns>A list of product definitions.</returns>
        public static List<ProductDefinition> GetAllProductDefinitions()
        {
            var discoveredProducts = ProductManager.DiscoveredProducts;
            var productDefs = new List<ProductDefinition>();

            foreach (var product in discoveredProducts)
            {
                productDefs.Add(product);
            }
            
            return productDefs;
        }

        /// <summary>
        /// Gets weed product definitions from the game registry.
        /// </summary>
        /// <returns>A list of weed product definitions.</returns>
        public static List<WeedDefinition> GetWeedDefinitions()
        {
            return GetAllProductDefinitions()
                .Where(p => p is WeedDefinition)
                .Cast<WeedDefinition>()
                .ToList();
        }

        /// <summary>
        /// Gets meth product definitions from the game registry.
        /// </summary>
        /// <returns>A list of meth product definitions.</returns>
        public static List<MethDefinition> GetMethDefinitions()
        {
            return GetAllProductDefinitions()
                .Where(p => p is MethDefinition)
                .Cast<MethDefinition>()
                .ToList();
        }

        /// <summary>
        /// Gets cocaine product definitions from the game registry.
        /// </summary>
        /// <returns>A list of cocaine product definitions.</returns>
        public static List<CocaineDefinition> GetCocaineDefinitions()
        {
            return GetAllProductDefinitions()
                .Where(p => p is CocaineDefinition)
                .Cast<CocaineDefinition>()
                .ToList();
        }

        /// <summary>
        /// Gets shroom product definitions from the game registry.
        /// </summary>
        /// <returns>A list of shroom product definitions.</returns>
        public static List<ShroomDefinition> GetShroomDefinitions()
        {
            return GetAllProductDefinitions()
                .Where(p => p is ShroomDefinition)
                .Cast<ShroomDefinition>()
                .ToList();
        }

        /// <summary>
        /// Populates a storage container with packaged products.
        /// </summary>
        /// <param name="storage">The storage instance to populate.</param>
        /// <param name="packagingId">The ID of the packaging to use (e.g., "baggie", "jar", "brick").</param>
        /// <param name="quantityPerItem">The quantity of each product item.</param>
        /// <returns>The number of items successfully added.</returns>
        public static int PopulateWithPackagedProducts(StorageInstance storage, string packagingId, int quantityPerItem = 1)
        {
            if (storage.S1Storage == null)
            {
                Debug.LogWarning("[ProductPopulator] Cannot populate null storage");
                return 0;
            }
            
            var packaging = GetPackaging(packagingId);
            if (packaging == null)
            {
                Debug.LogError($"[ProductPopulator] Failed to get packaging '{packagingId}' - cannot create packaged products");
                return 0;
            }

            var productDefinitions = GetAllProductDefinitions();
            if (productDefinitions.Count == 0)
            {
                Debug.LogWarning("[ProductPopulator] No product definitions found in discovered products");
                return 0;
            }
            
            int addedCount = 0;
            int slotIndex = 0;

            while (slotIndex < storage.SlotCount && addedCount < storage.SlotCount)
            {
                var productDef = productDefinitions[slotIndex % productDefinitions.Count];
                var productInstance = CreatePackagedProduct(productDef, packaging, quantityPerItem);

                if (productInstance == null)
                {
                    Debug.LogWarning($"[ProductPopulator] Failed to create packaged instance for '{productDef.Name}'");
                    slotIndex++;
                    continue;
                }

                if (storage.CanItemFit(productInstance, productInstance.Quantity))
                {
                    storage.AddItem(productInstance);
                    addedCount++;
                }
                else
                {
                    Debug.LogWarning($"[ProductPopulator] Item does not fit in storage: {productDef.Name}");
                    break;
                }

                slotIndex++;
            }

            return addedCount;
        }

        /// <summary>
        /// Populates a storage container with specific packaged products by ID.
        /// </summary>
        /// <param name="storage">The storage instance to populate.</param>
        /// <param name="productIds">List of product IDs to add.</param>
        /// <param name="packagingId">The ID of the packaging to use (e.g., "baggie", "jar", "brick").</param>
        /// <param name="quantityPerProduct">Quantity of each product to add (default 1).</param>
        /// <returns>The number of items successfully added.</returns>
        public static int PopulateWithSpecificPackagedProducts(StorageInstance storage, List<string> productIds, string packagingId, int quantityPerProduct = 1)
        {
            if (storage.S1Storage == null)
            {
                Debug.LogWarning("[ProductPopulator] Cannot populate null storage");
                return 0;
            }

            if (productIds.First() == null || productIds.Count == 0)
            {
                Debug.LogWarning("[ProductPopulator] No product IDs provided");
                return 0;
            }

            var packaging = GetPackaging(packagingId);
            if (packaging == null)
            {
                Debug.LogError($"[ProductPopulator] Failed to get packaging '{packagingId}'");
                return 0;
            }

            int addedCount = 0;

            foreach (var productId in productIds)
            {
                var itemDef = ItemManager.GetItemDefinition(productId);

                if (itemDef is ProductDefinition productDef)
                {
                    var productInstance = CreatePackagedProduct(productDef, packaging, quantityPerProduct);

                    if (productInstance != null && storage.CanItemFit(productInstance, quantityPerProduct))
                    {
                        storage.AddItem(productInstance);
                        addedCount++;
                    }
                }
                else
                {
                    Debug.LogWarning($"[ProductPopulator] Product '{productId}' not found or is not a product");
                }
            }

            return addedCount;
        }

        /// <summary>
        /// Creates a packaged product instance.
        /// </summary>
        /// <param name="productDef">The product definition.</param>
        /// <param name="packaging">The packaging definition.</param>
        /// <param name="quantity">The quantity of the product.</param>
        /// <returns>The created product instance, or null if failed.</returns>
        public static ProductInstance? CreatePackagedProduct(ProductDefinition productDef, PackagingDefinition packaging, int quantity)
        {
            try
            {
                var s1ProductDef = productDef.S1ProductDefinition;
                var s1Packaging = CrossType.As<S1ProductPackaging.PackagingDefinition>(packaging.S1ItemDefinition);

                if (s1Packaging == null)
                {
                    Debug.LogError($"[ProductPopulator] Failed to get S1 packaging definition");
                    return null;
                }

                var s1ProductInstance = new S1Product.ProductItemInstance(
                    s1ProductDef,
                    quantity,
                    S1ItemFramework.EQuality.Standard,
                    s1Packaging
                );
                
                return new ProductInstance(s1ProductInstance);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ProductPopulator] Exception creating packaged product: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Populates a storage container with non-packaged products.
        /// </summary>
        /// <param name="storage">The storage instance to populate.</param>
        /// <param name="quantityPerItem">The quantity of each product item.</param>
        /// <returns>The number of items successfully added.</returns>
        public static int PopulateWithUnpackagedProducts(StorageInstance storage, int quantityPerItem = 1)
        {
            if (storage.S1Storage == null)
            {
                Debug.LogWarning("[ProductPopulator] Cannot populate null storage");
                return 0;
            }

            var productDefinitions = GetAllProductDefinitions();
            if (productDefinitions.Count == 0)
            {
                Debug.LogWarning("[ProductPopulator] No product definitions found in discovered products");
                return 0;
            }

            int addedCount = 0;
            int slotIndex = 0;

            while (slotIndex < storage.SlotCount && addedCount < storage.SlotCount)
            {
                var productDef = productDefinitions[slotIndex % productDefinitions.Count];
                var productInstance = productDef.CreateInstance(quantityPerItem) as ProductInstance;

                if (productInstance == null)
                {
                    Debug.LogWarning($"[ProductPopulator] Failed to create instance for '{productDef.Name}'");
                    slotIndex++;
                    continue;
                }

                if (storage.CanItemFit(productInstance, productInstance.Quantity))
                {
                    storage.AddItem(productInstance);
                    addedCount++;
                }
                else
                {
                    Debug.LogWarning($"[ProductPopulator] Item does not fit in storage: {productDef.Name}");
                    break;
                }

                slotIndex++;
            }
            
            return addedCount;
        }

        /// <summary>
        /// Populates a storage container with specific non-packaged products by ID.
        /// </summary>
        /// <param name="storage">The storage instance to populate.</param>
        /// <param name="productIds">List of product IDs to add.</param>
        /// <param name="quantityPerProduct">Quantity of each product to add (default 1).</param>
        /// <returns>The number of items successfully added.</returns>
        public static int PopulateWithSpecificProducts(StorageInstance storage, List<string> productIds, int quantityPerProduct = 1)
        {
            if (storage.S1Storage == null)
            {
                Debug.LogWarning("[ProductPopulator] Cannot populate null storage");
                return 0;
            }

            if (productIds.First() == null || productIds.Count == 0)
            {
                Debug.LogWarning("[ProductPopulator] No product IDs provided");
                return 0;
            }

            int addedCount = 0;

            foreach (var productId in productIds)
            {
                var itemDef = ItemManager.GetItemDefinition(productId);

                if (itemDef is ProductDefinition productDef)
                {
                    var productInstance = productDef.CreateInstance(quantityPerProduct) as ProductInstance;

                    if (productInstance != null && storage.CanItemFit(productInstance, quantityPerProduct))
                    {
                        storage.AddItem(productInstance);
                        addedCount++;
                    }
                }
                else
                {
                    Debug.LogWarning($"[ProductPopulator] Product '{productId}' not found or is not a product");
                }
            }

            return addedCount;
        }

        /// <summary>
        /// Populates a storage container with packaged weed products in jars.
        /// Fills all available slots with 20 units (4 jars) of each product.
        /// </summary>
        /// <param name="storage">The storage instance to populate.</param>
        /// <returns>The number of items successfully added.</returns>
        public static int PopulateWithWeedProducts(StorageInstance storage)
        {
            return PopulateWithPackagedProducts(storage, "jar", 20);
        }

        /// <summary>
        /// Populates a storage container by finding it from a GameObject.
        /// Fills all slots with packaged products in the specified packaging.
        /// </summary>
        /// <param name="gameObject">The GameObject with a StorageEntity component.</param>
        /// <param name="packagingId">The ID of the packaging to use (e.g., "baggie", "jar", "brick").</param>
        /// <param name="quantityPerItem">The quantity of each product item.</param>
        /// <returns>The number of items successfully added, or -1 if storage not found.</returns>
        public static int PopulateFromGameObject(GameObject gameObject, string packagingId, int quantityPerItem = 1)
        {
            if (gameObject == null)
            {
                Debug.LogWarning("[ProductPopulator] PopulateFromGameObject called with null GameObject");
                return -1;
            }

            var storage = StorageInstance.FromGameObject(gameObject);

            if (storage == null || storage.S1Storage == null)
            {
                Debug.LogWarning($"[ProductPopulator] No StorageEntity found on GameObject '{gameObject?.name}', trying FromGameObjectInChildren...");
                storage = StorageInstance.FromGameObjectInChildren(gameObject);
            }

            if (storage == null)
            {
                Debug.LogError($"[ProductPopulator] Failed to find StorageEntity on '{gameObject?.name}' or its children");
                return -1;
            }

            return PopulateWithPackagedProducts(storage, packagingId, quantityPerItem);
        }
    }
}
