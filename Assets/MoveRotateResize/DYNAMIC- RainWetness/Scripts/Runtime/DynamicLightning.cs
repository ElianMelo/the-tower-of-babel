// using UnityEngine;

// public class DynamicLightning : MonoBehaviour
// {
//     [Foldout("Lightning Pool")]public SpawnLightning spawnLightning; 
//     [Foldout("Lightning Pool")][SerializeField] private bool useCollectionChecks = true;
//     [Foldout("Lightning Pool")][SerializeField] private int initialPoolSize = 5;
//     [Foldout("Lightning Pool")][SerializeField] private int maxPoolSize = 10;
//     private IObjectPool<SpawnLightning> poolLightning;



//     public GameObject objectPrefab; // Reference to the prefab you want to spawn
//     public float spawnInterval = 2f; // Interval between spawns
//     public float spawnRadius = 5f; // Radius within which objects will spawn

//     private float timer; // Timer to keep track of spawn intervals


//     void Start()
//     {
//         InitializeInteriorRainAudioSourcePool();
//     }


//     void Update()
//     {
//         // Increment the timer
//         timer += Time.deltaTime;

//         // Check if it's time to spawn a new object
//         if (timer >= spawnInterval)
//         {
//             SpawnObject();
//             timer = 0f; // Reset the timer
//         }
//     }


//     void SpawnObject()
//     {
//         // Generate a random position within the spawn radius
//         Vector3 randomPosition = Random.insideUnitSphere * spawnRadius;
//         randomPosition.y = 0; // Ensure the object spawns at the same height as the spawner

//         // Spawn the object at the random position
//         SpawnLightning newSpawnLightning = poolLightning.Get();
//         newSpawnLightning.transform.position = transform.position + randomPosition;
//         newSpawnLightning.transform.rotation = Quaternion.identity;
//     }


//     // Helper method to allow adjusting spawn parameters during gameplay
//     public void AdjustSpawnSettings(float newInterval, float newRadius)
//     {
//         spawnInterval = newInterval;
//         spawnRadius = newRadius;
//     }

  
//     void InitializeInteriorRainAudioSourcePool()
//     {
//         // Create the object pool
//         poolLightning = new ObjectPool<SpawnLightning>(CreatePooledAudioSource,
//             OnTakeFromPool, OnReturnedToPool, OnDestroyPoolObject,
//             useCollectionChecks, 0, maxPoolSize); // Start with zero instances initially

//         // Populate the pool with initial instances
//         for (int i = 0; i < initialPoolSize; i++)
//         {
//             SpawnLightning spawnLightningInstance = CreatePooledAudioSource();
//             poolLightning.Release(spawnLightningInstance); // Release the instance into the pool
//         }
//     }


//     private SpawnLightning CreatePooledAudioSource()
//     {
//         SpawnLightning newAudioPoolRainInterior = Instantiate(spawnLightning);
//         newAudioPoolRainInterior.transform.SetParent(transform);
//         newAudioPoolRainInterior.gameObject.SetActive(false);        
//         newAudioPoolRainInterior.ObjectPool = poolLightning;

//         return newAudioPoolRainInterior;
//     }


//     void OnTakeFromPool(SpawnLightning pooledObject)
//     {
//         // Set the ObjectPool reference for the pooled object
//         pooledObject.ObjectPool = poolLightning;
//         pooledObject.gameObject.SetActive(true);
//     }


//     void OnReturnedToPool(SpawnLightning pooledObject)
//     {
//         newAudioPoolRainInterior.transform.SetParent(transform);
//         pooledObject.gameObject.SetActive(false);
//     }


//     void OnDestroyPoolObject(SpawnLightning pooledObject)
//     {
//         Destroy(pooledObject.gameObject);
//     }
// }